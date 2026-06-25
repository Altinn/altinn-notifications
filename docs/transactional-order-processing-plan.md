# Make all database writes in ProcessOrder / ProcessOrderRetry atomic (single DB transaction)

## Summary

`OrderProcessingService.ProcessOrder` and `OrderProcessingService.ProcessOrderRetry`
previously performed several persistence operations as **independent writes**, each on
its own database connection. The order-completion path
(`TryCompleteOrderBasedOnNotificationsState`) wrote derived records — a **status feed**
record and a **notification log** entry — outside the order status update's transaction.
The status-feed insert was best-effort (guarded by `try/catch` + `LogWarning`), so it
could fail silently, leaving the system asymmetric.

This document describes the changes made and the remaining work to achieve full
atomicity.

---

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

## Step 1 — Inventory: what writes to the database

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

**External / read — run BEFORE the transaction (no transaction held across HTTP):**
- `IContactPointService` lookups (`AddEmailAndSmsContactPointsAsync`, preferred /
  per-channel address resolution).

**DB writes — must run INSIDE one transaction:**
- `EmailNotificationRepository.AddNotification` (one per email notification).
- `SmsNotificationRepository.AddNotification` (one per SMS notification).
- Order processing-status update.
- Order completion check and status transition.
- Status feed insert.
- Notification log insert.

**Publish — must run AFTER commit (or via outbox):**
- Azure Service Bus message.

---

## Important constraint: not everything is a DB operation

`ProcessOrder` / `ProcessOrderRetry` also perform operations that **cannot**
participate in a Postgres transaction and must be kept **outside** the boundary:

- **Azure Service Bus publishing** — a sent message cannot be rolled back
  (dual-write problem).
- **External lookups** (`IContactPointService` over HTTP) — read-only, but must not
  be performed while a DB transaction is held open (holding a transaction across
  network latency is harmful).

Therefore "same transaction" applies to **DB writes only**.

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

### Step 2 — Define the in-memory result models ✅ Done

Per-channel sub-results returned by the channel processing services:

```csharp
public sealed record SmsOrderProcessingResult(
	OrderProcessingStatus StatusToSet,
	IReadOnlyList<SmsNotification> SmsNotifications);

public sealed record EmailOrderProcessingResult(
	OrderProcessingStatus StatusToSet,
	IReadOnlyList<EmailNotification> EmailNotifications);
```

Combined aggregate wrapping both sub-results, handed to the single repository persist call:

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

### Step 3 — Make channel and notification services pure ✅ Done

- `EmailNotificationService.CreateNotification` and `SmsNotificationService.CreateNotification`
  now **accumulate and return** materialized notifications instead of calling
  `AddNotification` eagerly. Each returns `IReadOnlyList<EmailNotification>` /
  `IReadOnlyList<SmsNotification>`.

- A fresh recipient copy is created per address point in both services, preventing shared
  mutable reference bugs across multiple notifications for the same recipient.

- All channel service entry points (`ProcessOrder`, `ProcessOrderRetry`,
  `ProcessOrderWithoutAddressLookup`, `ProcessOrderRetryWithoutAddressLookup`) now return
  typed result objects instead of `Task`.

- `EmailAndSmsOrderProcessingService` and `PreferredChannelProcessingService` perform the
  `IContactPointService` lookup once up front, then merge the two sub-results into an
  `OrderProcessingResult`. The `default` branch in `PreferredChannelProcessingService`
  throws `ArgumentOutOfRangeException` for unsupported channel values.

- The retry path in both `EmailOrderProcessingService` and `SmsOrderProcessingService`
  now checks registration per address (not just the first address), matching the
  behaviour of the normal processing path.

### Step 4 — Add atomic persist methods to the repository ⏳ Pending

Two new methods on `IOrderRepository` replace the old piecemeal calls:

````````markdown
/// <summary>
/// Persist a fully-formed order processing result in a single transaction:
/// 1. Generated notifications (email + SMS).
/// 2. Order processing status.
/// 3. Order completion check and status transition.
/// 4. Status feed insert (optional, if completes order).
/// 5. Notification log insert (optional, if completes order).
/// </summary>
Task PersistProcessingResultAsync(OrderProcessingResult result, IOrderContext transactionContext, CancellationToken cancellationToken = default);
````````

Implementation sketch for `PersistProcessingResultAsync`:

````````csharp
public async Task PersistProcessingResultAsync(OrderProcessingResult result, IOrderContext transactionContext, CancellationToken cancellationToken = default)
{
	// 1. Generated notifications (email + SMS).
	foreach (var sms in result.SmsNotifications)
		await _smsNotificationRepository.AddNotificationAsync(sms, transactionContext, cancellationToken);

	foreach (var email in result.EmailNotifications)
		await _emailNotificationRepository.AddNotificationAsync(email, transactionContext, cancellationToken);

	// 2. Order processing status.
	order.ProcessingStatus = result.StatusToSet;

	// 3. Order completion check and status transition.
	if (result.CompletesOrder)
	{
		// transition logic
	}

	// 4. Status feed insert (optional, if completes order).
	if (result.StatusFeed is { } feed)
		await _statusFeedRepository.AddStatusFeedEntryAsync(feed, transactionContext, cancellationToken);

	// 5. Notification log insert (optional, if completes order).
	if (result.NotificationLog is { } log)
		await _notificationLogRepository.AddNotificationLogEntryAsync(log, transactionContext, cancellationToken);
}
````````

### Step 5 — Rewrite `ProcessOrder` / `ProcessOrderRetry` orchestration ✅ Done

The `switch` in both methods now captures typed results from the channel services and
calls the new repository methods:

````````csharp
switch (order.NotificationChannel)
{
	case NotificationChannel.Email:
		var emailResult = await _emailOrderProcessingService.ProcessOrder(request, cancellationToken);
		return await PersistOrderProcessingResult(request, emailResult, cancellationToken);

	case NotificationChannel.Sms:
		var smsResult = await _smsOrderProcessingService.ProcessOrder(request, cancellationToken);
		return await PersistOrderProcessingResult(request, smsResult, cancellationToken);

	case NotificationChannel.EmailPreferred:
		var emailPreferredResult = await _preferredChannelProcessingService.ProcessOrder(request, cancellationToken);
		return await PersistOrderProcessingResult(request, emailPreferredResult, cancellationToken);

	case NotificationChannel.SmsPreferred:
		var smsPreferredResult = await _preferredChannelProcessingService.ProcessOrder(request, cancellationToken);
		return await PersistOrderProcessingResult(request, smsPreferredResult, cancellationToken);

	case NotificationChannel.EmailAndSms:
		var emailAndSmsResult = await _emailAndSmsOrderProcessingService.ProcessOrder(request, cancellationToken);
		return await PersistOrderProcessingResult(request, emailAndSmsResult, cancellationToken);

	default:
		throw new ArgumentOutOfRangeException($"Unsupported notification channel: {order.NotificationChannel}");
}
````````

### Step 6 — Remove the best-effort paths ✅ Done

The old `TryInsertStatusFeedForCompletedOrder` wrapper with its swallowed exception is
deleted. `TryCompleteOrderBasedOnNotificationsState` is no longer called from the
service layer.

### Step 7 — Azure Service Bus boundary

Keep the publish strictly **after** commit. Prefer a transactional **outbox** row
written inside the Step 4 transaction (published after commit) to avoid the dual-write
problem; otherwise document the after-commit / at-least-once tradeoff.

### Step 8 — Tests ⏳ Pending

- Forced failure in **any** DB write → full rollback, no partial rows.
- Happy path → exactly one status feed + one notification log, order `Completed`.
- No-completion path → no status feed / log written.
- Send-condition-not-met path → status feed and notification log written atomically.
- Azure Service Bus publish failure after commit → DB state preserved.
- Architecture guard → the `Core` project references no Npgsql types on these flows.

### Sequencing

Steps 1–3, 5–6 ✅ → **Step 4** (repository implementation) → Step 7 (bus) → Step 8 (tests).

---

## Constraints

- **Transaction ownership stays in the repository layer.** The service layer
  (`Altinn.Notifications.Core`) must **not** reference `NpgsqlConnection`,
  `NpgsqlTransaction`, or `NpgsqlDataSource`.
- Reuse the existing transactional pattern in `NotificationRepositoryBase` /
  `OrderRepository` (`await using var transaction` / `CommitAsync` / `RollbackAsync` /
  `throw;`).

---

## Acceptance criteria

- [ ] All DB writes in `ProcessOrder` are performed in a single repository-owned
      transaction; same for `ProcessOrderRetry`.
- [ ] Generated notifications, processing-status update, order completion, status feed
      insert, and notification log insert occur in that same transaction.
- [ ] If the order is *not* transitioned to `Completed`, no status feed / notification
      log records are written for the completion path.
- [ ] Send-condition-not-met writes status feed and notification log atomically.
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
