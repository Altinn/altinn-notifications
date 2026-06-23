# Make all database writes in ProcessOrder / ProcessOrderRetry atomic (single DB transaction)

## Summary

`OrderProcessingService.ProcessOrder` and `OrderProcessingService.ProcessOrderRetry`
perform several persistence operations that are currently executed as **independent
writes**, each on its own database connection. Among them, the order-completion path
(`TryCompleteOrderBasedOnNotificationsState`) writes derived records — a **status
feed** record and a **notification log** entry — outside the order status update's
transaction. The notification-log insert is currently best-effort
(`TryInsertNotificationLog` guarded by `try/catch` + `LogWarning`), so it can fail
silently.

Because these writes are not part of a single database transaction, a failure in one
can leave the system **asymmetric / diverged** (e.g. an order marked `Completed` with
no status feed record and/or no notification log entry, or generated notifications
persisted while the order status update is lost).

## Goal

> **All database write operations** performed within `ProcessOrder` and
> `ProcessOrderRetry` must succeed or fail **together**, in a single DB transaction
> owned by the repository layer. If any DB write fails, the whole unit of work rolls
> back.

## Invariant we want to guarantee

> A `Completed` order **always** has exactly one status feed record **and** one
> notification log entry. More generally, no `ProcessOrder` / `ProcessOrderRetry`
> invocation leaves a partially-applied set of DB writes.

---

## Step 1 inventory — what actually writes to the database

### Call-chain map

```
OrderProcessingService.ProcessOrder
  └─ switch (order.NotificationChannel)
	   ├─ Email          → IEmailOrderProcessingService.ProcessOrder
	   ├─ Sms            → ISmsOrderProcessingService.ProcessOrder
	   ├─ EmailPreferred ┐
	   ├─ SmsPreferred   ┼→ IPreferredChannelProcessingService.ProcessOrder
	   └─ EmailAndSms    → IEmailAndSmsOrderProcessingService.ProcessOrder
```

### What each layer does

| Service | External (HTTP) lookup | Persists? | How |
|---|---|---|---|
| `EmailOrderProcessingService` | `IContactPointService` (address lookup) | yes (delegated) | → `IEmailNotificationService.CreateNotification` |
| `SmsOrderProcessingService` | `IContactPointService` | yes (delegated) | → `ISmsNotificationService.CreateNotification` |
| `EmailAndSmsOrderProcessingService` | `IContactPointService` (`AddEmailAndSmsContactPointsAsync`) | yes (delegated) | → both email + sms processing services (`...WithoutAddressLookup`) |
| `PreferredChannelProcessingService` | `IContactPointService` | yes (delegated) | → email or sms processing service depending on availability |

### The actual DB-write sink

```
*OrderProcessingService
  → I{Email,Sms}NotificationService.CreateNotification
	 → EmailNotificationService / SmsNotificationService
		├─ builds `new EmailNotification` / `new SmsNotification` in-memory
		│  (inside a foreach over recipients)
		└─ persists EAGERLY via AddNotification
		   → EmailNotificationRepository / SmsNotificationRepository
			  → opens its OWN connection via _dataSource
			  → NO BeginTransactionAsync  ← each write is autonomous
```

### Key findings

1. **No service references Npgsql** — the service/repository layering constraint
   already holds and must be preserved.
2. The real persistence happens in `AddNotification` on
   `EmailNotificationRepository` / `SmsNotificationRepository`.
3. **Each `AddNotification` opens its own connection from `_dataSource` and does NOT
   use `BeginTransactionAsync`** — so today every generated notification is an
   independent, autonomous write. This is the structural obstacle to atomicity.
4. **`CreateNotification` already constructs the model object in memory**
   (`new EmailNotification` / `new SmsNotification`) before persisting. It returns
   `Task` (nothing) and persists eagerly per recipient. Because the model is already
   materialized, returning it instead of persisting eagerly is a small change.
5. **Each channel service exposes two entry points**, both currently returning
   `Task` (nothing):
   - `ProcessOrder` — performs the `IContactPointService` address lookup, then builds
     + persists.
   - `ProcessOrderWithoutAddressLookup` — assumes recipients are already resolved;
     used by `EmailAndSmsOrderProcessingService` and `PreferredChannelProcessingService`
     after they have done the lookup once.

   Both entry points must change from `Task` to `Task<...Result>` so the combined
   channels can compose their sub-results **without** an intermediate DB write.

### Operations classified

**External / read — must run BEFORE the transaction (no transaction held across HTTP):**
- `IContactPointService` lookups (`AddEmailAndSmsContactPointsAsync`, preferred /
  per-channel address resolution).

**DB writes — must run INSIDE one transaction:**
- `EmailNotificationRepository.AddNotification` (one per email notification).
- `SmsNotificationRepository.AddNotification` (one per sms notification).
- Order processing-status update (`OrderRepository.SetProcessingStatus`).
- `OrderRepository.TryCompleteOrderBasedOnNotificationsState`.
- Status feed insert (today `TryInsertStatusFeedForCompletedOrder`, service-layer best-effort).
- Notification log insert (today `TryInsertNotificationLog`, service-layer best-effort).

**Publish — must run AFTER commit (or via outbox):**
- Azure Service Bus message.

> **Note:** Kafka has been fully replaced by Azure Service Bus; there is no
> Kafka-related logic on these paths. A Service Bus send still cannot enroll in a
> Postgres transaction, so the dual-write boundary below still applies.

---

## Important constraint: not everything is a DB operation

`ProcessOrder` / `ProcessOrderRetry` also perform operations that **cannot**
participate in a Postgres transaction and must be kept **outside** the boundary:

- **Azure Service Bus publishing** — a sent message cannot be rolled back
  (dual-write problem).
- **External lookups** (`IContactPointService` over HTTP) — read-only, but must not
  be performed while a DB transaction is held open (holding a transaction across
  network latency is harmful).

Therefore "same transaction" applies to **DB writes only**. The non-transactional
steps must be ordered safely around the transaction.

---

## Target design — materialize, then persist atomically

Mirror the existing `OrderRequestService → OrderRepository.Create` convention: the
service builds a fully-formed in-memory graph, then a single repository method writes
the whole graph inside one transaction. The repository owns the transaction; the
service stays Npgsql-free.

```
ProcessOrder:
  1. External lookups (HTTP contact points)          ← BEFORE any transaction
  2. switch → BUILD in-memory model only             ← NO DB writes
  3. ONE repository call persists the whole graph     ← single transaction
  4. Publish to Azure Service Bus                     ← AFTER commit (or outbox)
```

### Transaction boundary (design notes)

1. **Read / external lookups first** (contact-point resolution), before opening the
   transaction.
2. **Open one transaction** in the repository layer and perform **all DB writes**
   (generated notifications, processing-status update, order completion, status feed
   insert, notification log insert) bound to the **same** `NpgsqlConnection` +
   `NpgsqlTransaction`.
3. **Commit.**
4. **Publish to Azure Service Bus AFTER commit.** To avoid the dual-write problem,
   prefer a **transactional outbox**: write the outbox row inside the same
   transaction and publish after commit. If an outbox is out of scope, publish
   strictly after a successful commit and document the at-least-once / lost-message
   tradeoff.

---

## Implementation plan

### Step 2 — Define the in-memory result models (the "unit of work")

Each channel service returns an in-memory record of what must be persisted, instead
of writing to the DB. Define a **per-channel** sub-result and a **combined** aggregate.

Per-channel sub-results (returned by the channel services):

```csharp
public sealed record SmsOrderProcessingResult(
	OrderProcessingStatus StatusToSet,
	IReadOnlyList<SmsNotification> SmsNotifications);

public sealed record EmailOrderProcessingResult(
	OrderProcessingStatus StatusToSet,
	IReadOnlyList<EmailNotification> EmailNotifications);
```

Combined aggregate (handed to the single persist call):

```csharp
public sealed record OrderProcessingResult(
	OrderProcessingStatus StatusToSet,
	IReadOnlyList<EmailNotification> EmailNotifications,
	IReadOnlyList<SmsNotification> SmsNotifications,
	bool CompletesOrder,
	StatusFeedEntry? StatusFeed,          // populated iff CompletesOrder
	NotificationLogEntry? NotificationLog); // populated iff CompletesOrder
```

This mirrors how `OrderRequestService` builds a `NotificationOrder` graph before
saving. The per-channel split is required because `EmailAndSmsOrderProcessingService`
and `PreferredChannelProcessingService` compose **two** channel sub-results into one
`OrderProcessingResult` — which is only possible if neither channel persisted eagerly.

### Step 3 — Make the channel + notification services "pure" (responsibility reordering)

- `EmailNotificationService.CreateNotification` /
  `SmsNotificationService.CreateNotification` already build the model in memory inside
  a `foreach` over recipients. Change them to **accumulate and return** the
  materialized notifications **instead of** calling `AddNotification` eagerly:

  ```csharp
  // Before (eager persist, returns Task)
  foreach (var recipient in recipients)
  {
	  var notification = new SmsNotification(...);
	  await _repository.AddNotification(notification, ...); // ← remove
  }

  // After (pure, returns the list)
  var notifications = new List<SmsNotification>();
  foreach (var recipient in recipients)
  {
	  notifications.Add(new SmsNotification(...));
  }
  return notifications;
  ```

- The channel services change **both** entry points to return a sub-result:

  ```csharp
  // Before
  Task ProcessOrder(NotificationOrder order);
  Task ProcessOrderWithoutAddressLookup(NotificationOrder order, ...);

  // After
  Task<SmsOrderProcessingResult> ProcessOrder(NotificationOrder order);
  Task<SmsOrderProcessingResult> ProcessOrderWithoutAddressLookup(NotificationOrder order, ...);
  ```

  (Symmetrically for the email service with `EmailOrderProcessingResult`.)

- `EmailAndSmsOrderProcessingService` / `PreferredChannelProcessingService`:
  - Do the `IContactPointService` lookup **once**, up front (outside the future
	transaction).
  - Call each channel's `ProcessOrderWithoutAddressLookup`, then **merge** the
	sub-results into a single `OrderProcessingResult`:

	```csharp
	var sms   = await _smsProcessingService.ProcessOrderWithoutAddressLookup(order, ...);
	var email = await _emailProcessingService.ProcessOrderWithoutAddressLookup(order, ...);

	return new OrderProcessingResult(
		StatusToSet:        ...,
		EmailNotifications: email.EmailNotifications,
		SmsNotifications:   sms.SmsNotifications,
		CompletesOrder:     ...,
		StatusFeed:         ...,
		NotificationLog:    ...);
	```

Because every case now returns the same aggregate type, the `switch` in
`OrderProcessingService` collapses to "pick the builder, get a model" — the invariant
is enforced centrally, not duplicated per case.

### Step 4 — Add one transactional persist method to the repository

Mirror `OrderRepository.Create`; accept the aggregate and write everything in a single
transaction.

```csharp
public async Task<bool> PersistOrderProcessingResult(
	Guid orderId, OrderProcessingResult result, CancellationToken ct = default)
{
	await using var connection = await _dataSource.OpenConnectionAsync(ct);
	await using var transaction = await connection.BeginTransactionAsync(ct);
	try
	{
		await SetProcessingStatus(connection, transaction, orderId, result.StatusToSet, ct);
		await InsertEmailNotifications(connection, transaction, result.EmailNotifications, ct);
		await InsertSmsNotifications(connection, transaction, result.SmsNotifications, ct);

		bool completed = result.CompletesOrder
			&& await TryCompleteOrder(connection, transaction, orderId, ct);

		if (completed)
		{
			await InsertStatusFeed(connection, transaction, result.StatusFeed!, ct);
			await InsertNotificationLog(connection, transaction, result.NotificationLog!, ct);
		}

		await transaction.CommitAsync(ct);
		return completed;
	}
	catch
	{
		await transaction.RollbackAsync(ct);
		throw;
	}
}
```

Every command binds to the **same** `connection` + `transaction`. All Npgsql stays in
the repository layer.

> **Sub-task:** `EmailNotificationRepository.AddNotification` and
> `SmsNotificationRepository.AddNotification` currently open their own connection and
> have no transaction parameter. Either add internal overloads that accept an existing
> `NpgsqlConnection` + `NpgsqlTransaction`, or move their insert SQL into
> `PersistOrderProcessingResult`. The public `AddNotification` (used elsewhere) can
> remain as a thin wrapper that opens its own transaction.

### Step 5 — Rewrite `ProcessOrder` / `ProcessOrderRetry` orchestration

```csharp
// 1. external lookups (no transaction held)
var enriched = await _contactPointService.AddEmailAndSmsContactPoints(order, ...);

// 2. switch builds in-memory model only
OrderProcessingResult result = order.NotificationChannel switch
{
	/* each case returns a model; no DB writes */
};

// 3. one atomic persist
bool isOrderCompleted = await _orderRepository.PersistOrderProcessingResult(order.Id, result);

// 4. publish AFTER commit (or via outbox)
```

The `if (isOrderCompleted) { await TryInsertStatusFeedForCompletedOrder(...); }` block
is **deleted** — its work now happens inside the transaction in Step 4.

### Step 6 — Remove the best-effort paths

Delete `TryInsertStatusFeedForCompletedOrder`, `TryInsertNotificationLog`, and their
`LogWarning` handlers from the service. No swallowed exceptions remain on these paths.

### Step 7 — Azure Service Bus boundary

Keep the publish strictly **after** commit. Prefer a transactional **outbox** row
written inside the Step 4 transaction (published after commit) to avoid the dual-write
problem; otherwise document the after-commit / at-least-once tradeoff.

### Step 8 — Tests

- Forced failure in **any** DB write (notifications, processing status, completion,
  status feed, log) → full rollback, order not completed, no partial rows.
- Happy path → exactly one status feed + one notification log, order completed.
- No-completion path → no status feed / log written.
- Azure Service Bus publish failure after commit → DB state preserved, handled per the
  documented strategy.
- Architecture guard → the `Core` (service) project references no Npgsql types on
  these flows.

### Sequencing

Step 1 (inventory, done) → 2–3 (records + pure services) → 4 (repository) →
5–6 (service) → 7 (bus) → 8 (tests).

---

## Constraints

- **Transaction ownership stays in the repository layer.** The service layer
  (`Altinn.Notifications.Core`) must **not** reference `NpgsqlConnection`,
  `NpgsqlTransaction`, or `NpgsqlDataSource`.
- Reuse the existing transactional pattern in `NotificationRepositoryBase` /
  `OrderRepository` (`await using var transaction` / `CommitAsync` / `RollbackAsync` /
  `throw;`).

## Acceptance criteria

- [ ] All DB writes in `ProcessOrder` are performed in a single repository-owned
	  transaction; same for `ProcessOrderRetry`.
- [ ] Generated notifications, processing-status update, order completion, status feed
	  insert, and notification log insert occur in that same transaction.
- [ ] If the order is *not* transitioned to `Completed`, no status feed / notification
	  log records are written.
- [ ] Any DB write failure rolls back the whole transaction and propagates the
	  exception (no swallowed exceptions on these paths).
- [ ] `OrderProcessingService` no longer orchestrates DB writes as separate steps; it
	  calls a single repository method per path.
- [ ] No Npgsql types appear in the `Core` (service) project for these flows.
- [ ] Azure Service Bus publishing happens **outside** and **after** the DB
	  transaction (ideally via outbox); the chosen approach and its delivery
	  guarantees are documented.
- [ ] External (HTTP) lookups happen **before** the transaction is opened.
- [ ] Integration test: a forced failure in any DB write leaves **no** partial DB
	  state.
- [ ] Integration test (happy path): completion writes exactly one status feed record
	  and one notification log entry, and the order is `Completed`.

## Out of scope

- The `notificationlog` schema rename (`sent_timestamp` → `last_update_timestamp`,
  nullable `created_timestamp`) and the `get_notification_logs` type-mismatch fix are
  tracked separately.
