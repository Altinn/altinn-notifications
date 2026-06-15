using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Notifications.Tools.DlqManager.Configuration;
using Altinn.Notifications.Tools.DlqManager.Models;
using Altinn.Notifications.Tools.DlqManager.Repositories;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.DlqManager.Services.Queues;

/// <summary>
/// Implements the DLQ operations for <c>altinn.notifications.orders.pastdue</c>:
/// <list type="number">
///   <item>Inspect — peek all DLQ messages, classify into six list files.</item>
///   <item>Resubmit registered-pending — resend to main queue + purge DLQ.</item>
///   <item>Resubmit processing-pending — resend to main queue + purge DLQ (operator must confirm).</item>
///   <item>Purge registered-expired — purge DLQ only.</item>
///   <item>Purge processing-expired — purge DLQ only.</item>
///   <item>Purge has-notifications — purge DLQ only (notifications already exist; resubmit would duplicate).</item>
///   <item>Purge other-status — purge DLQ only.</item>
///   <item>Query DB state — print current database state for a list file.</item>
/// </list>
/// </summary>
public sealed class PastDueOrdersQueueService(
    IOptions<AsbSettings> asbSettings,
    IOptions<PastDueOrdersQueueSettings> queueSettings,
    IOrderRepository repository,
    ServiceBusClient sbClient) : IPastDueOrdersQueueService, IAsyncDisposable
{
    private readonly AsbSettings _asbSettings = asbSettings.Value;
    private readonly PastDueOrdersQueueSettings _queueSettings = queueSettings.Value;
    private readonly IOrderRepository _repository = repository;
    private readonly ServiceBusClient _sbClient = sbClient;

    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    // Wolverine's default serializer uses camelCase + PropertyNameCaseInsensitive.
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string _notFound = "NOT FOUND";

    // ── Top-level sub-menu loop ────────────────────────────────────────────────

    public async Task RunMenuAsync()
    {
        while (true)
        {
            long dlqCount = await GetDlqCountAsync();
            string dlqCountDisplay = dlqCount >= 0 ? dlqCount.ToString() : "N/A";

            Console.WriteLine();
            Console.WriteLine("=== Past Due Orders Queue — DLQ Manager ===");
            Console.WriteLine($"Queue    : {_asbSettings.PastDueOrdersQueueName}");
            Console.WriteLine($"DLQ count: {dlqCountDisplay}");
            Console.WriteLine();
            Console.WriteLine("  1. Inspect DLQ — classify all messages and save to list files");
            Console.WriteLine("  2. Resubmit registered-pending list — send to main queue + purge DLQ");
            Console.WriteLine("  3. Resubmit processing-pending list — send to main queue + purge DLQ  [caution: potential concurrent processing]");
            Console.WriteLine("  4. Purge registered-expired list — purge DLQ only");
            Console.WriteLine("  5. Purge processing-expired list — purge DLQ only");
            Console.WriteLine("  6. Purge has-notifications list — purge DLQ only (notifications already exist)");
            Console.WriteLine("  7. Purge other-status list — purge DLQ only");
            Console.WriteLine("  8. Query DB state for a list file");
            Console.WriteLine("  0. Back");
            Console.WriteLine();
            Console.Write("Enter choice (0-8): ");

            var choice = Console.ReadLine()?.Trim();
            if (choice is null) return;

            switch (choice)
            {
                case "1": await InspectDlqAsync();                              break;
                case "2": await ResubmitListAsync(_queueSettings.RegisteredPendingListFilePath,  expectedStatus: "Registered"); break;
                case "3": await ResubmitProcessingPendingListAsync();           break;
                case "4": await PurgeListAsync(_queueSettings.RegisteredExpiredListFilePath,  "registered-expired");  break;
                case "5": await PurgeListAsync(_queueSettings.ProcessingExpiredListFilePath, "processing-expired"); break;
                case "6": await PurgeListAsync(_queueSettings.HasNotificationsListFilePath,  "has-notifications");   break;
                case "7": await PurgeListAsync(_queueSettings.OtherStatusListFilePath,       "other-status");        break;
                case "8": await QueryDbStateAsync();                            break;
                case "0": return;
                default:
                    Console.WriteLine("Invalid choice. Please enter 0-8.");
                    break;
            }
        }
    }

    // ── Operation 1: Inspect ──────────────────────────────────────────────────

    private async Task InspectDlqAsync()
    {
        Console.WriteLine();
        Console.WriteLine($"Inspecting DLQ for {_asbSettings.PastDueOrdersQueueName}...");

        await using var receiver = _sbClient.CreateReceiver(
            _asbSettings.PastDueOrdersQueueName,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var allMessages = new List<ServiceBusReceivedMessage>();
        long fromSeq = 0;

        while (true)
        {
            var page = await receiver.PeekMessagesAsync(maxMessages: 100, fromSequenceNumber: fromSeq);
            if (page.Count == 0)
                break;

            allMessages.AddRange(page);
            fromSeq = page[^1].SequenceNumber + 1;
        }

        Console.WriteLine($"Found {allMessages.Count} message(s).");

        // Deserialize all messages upfront so we can do a single bulk DB query.
        var parsed = new List<(ServiceBusReceivedMessage Msg, CommandDto Cmd)>();

        foreach (var msg in allMessages)
        {
            var cmd = TryDeserializeCommand(msg);
            if (cmd is null || cmd.Order is null)
            {
                Console.WriteLine($"  [WARN] Could not deserialize message {msg.MessageId} — skipping.");
                continue;
            }

            parsed.Add((msg, cmd));
        }

        // Bulk DB lookup — one round trip for all orders.
        var orderIds = parsed.Select(p => p.Cmd.Order!.Id).Distinct().ToList();
        var dbStates = orderIds.Count > 0
            ? await _repository.GetOrderStatesAsync(orderIds)
            : [];

        var registeredPending  = new List<DlqPastDueOrderItem>();
        var registeredExpired  = new List<DlqPastDueOrderItem>();
        var processingPending  = new List<DlqPastDueOrderItem>();
        var processingExpired  = new List<DlqPastDueOrderItem>();
        var hasNotifications   = new List<DlqPastDueOrderItem>();
        var otherStatus        = new List<DlqPastDueOrderItem>();

        Console.WriteLine();
        PrintTableHeader();

        foreach (var (msg, cmd) in parsed)
        {
            var order = cmd.Order!;

            dbStates.TryGetValue(order.Id, out var state);
            var (dbStatus, notifCount, expiryTime, isExpired) = state;

            string category = Categorize(dbStatus, notifCount, isExpired);

            Console.WriteLine(
                $"  {order.Id,-38} {dbStatus ?? _notFound,-22} {notifCount,5}  " +
                $"{FormatUtc(expiryTime),-22} {category}");

            var item = new DlqPastDueOrderItem
            {
                OrderId                       = order.Id,
                SendersReference              = order.SendersReference,
                ExpiryTime                    = expiryTime,
                NotificationChannel           = ChannelName(order.NotificationChannel),
                IsProcessOrderRetry           = cmd.IsProcessOrderRetry,
                CurrentDbStatus               = dbStatus,
                NotificationCount             = notifCount,
                DlqMessageId                  = msg.MessageId,
                DlqEnqueuedTime               = msg.EnqueuedTime.UtcDateTime,
                DlqDeadLetterReason           = msg.DeadLetterReason,
                DlqDeadLetterErrorDescription = msg.DeadLetterErrorDescription
            };

            switch (category)
            {
                case "REGISTERED/PENDING":   registeredPending.Add(item);  break;
                case "REGISTERED/EXPIRED":   registeredExpired.Add(item);  break;
                case "PROCESSING/PENDING":   processingPending.Add(item);  break;
                case "PROCESSING/EXPIRED":   processingExpired.Add(item);  break;
                case "HAS-NOTIFICATIONS":    hasNotifications.Add(item);   break;
                default:                     otherStatus.Add(item);        break;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Registered/pending   : {registeredPending.Count}");
        Console.WriteLine($"Registered/expired   : {registeredExpired.Count}");
        Console.WriteLine($"Processing/pending   : {processingPending.Count}");
        Console.WriteLine($"Processing/expired   : {processingExpired.Count}");
        Console.WriteLine($"Has notifications    : {hasNotifications.Count}");
        Console.WriteLine($"Other status         : {otherStatus.Count}");

        await WriteListFileAsync(_queueSettings.RegisteredPendingListFilePath,  registeredPending);
        await WriteListFileAsync(_queueSettings.RegisteredExpiredListFilePath,  registeredExpired);
        await WriteListFileAsync(_queueSettings.ProcessingPendingListFilePath,  processingPending);
        await WriteListFileAsync(_queueSettings.ProcessingExpiredListFilePath,  processingExpired);
        await WriteListFileAsync(_queueSettings.HasNotificationsListFilePath,   hasNotifications);
        await WriteListFileAsync(_queueSettings.OtherStatusListFilePath,        otherStatus);

        Console.WriteLine();
        Console.WriteLine($"Saved: {_queueSettings.RegisteredPendingListFilePath} ({registeredPending.Count})");
        Console.WriteLine($"Saved: {_queueSettings.RegisteredExpiredListFilePath} ({registeredExpired.Count})");
        Console.WriteLine($"Saved: {_queueSettings.ProcessingPendingListFilePath} ({processingPending.Count})");
        Console.WriteLine($"Saved: {_queueSettings.ProcessingExpiredListFilePath} ({processingExpired.Count})");
        Console.WriteLine($"Saved: {_queueSettings.HasNotificationsListFilePath} ({hasNotifications.Count})");
        Console.WriteLine($"Saved: {_queueSettings.OtherStatusListFilePath} ({otherStatus.Count})");
    }

    private static string Categorize(string? dbStatus, long notifCount, bool isExpired)
    {
        if (notifCount > 0) return "HAS-NOTIFICATIONS";

        return (dbStatus, isExpired) switch
        {
            ("Registered", false) => "REGISTERED/PENDING",
            ("Registered", true)  => "REGISTERED/EXPIRED",
            ("Processing",  false) => "PROCESSING/PENDING",
            ("Processing",  true)  => "PROCESSING/EXPIRED",
            _                     => $"OTHER ({dbStatus ?? _notFound})"
        };
    }

    // ── Operation 2: Resubmit registered-pending ──────────────────────────────

    private async Task ResubmitListAsync(string filePath, string expectedStatus)
    {
        var items = await ReadListFileAsync(filePath);
        if (items.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"Resubmitting {filePath} ({items.Count} item(s)) to {_asbSettings.PastDueOrdersQueueName}...");
        Console.WriteLine();

        await using var sender = _sbClient.CreateSender(_asbSettings.PastDueOrdersQueueName);

        await ProcessDlqItemsAsync(
            items,
            (msg, item, rcv) => ResubmitItemAsync(msg, item, rcv, sender, expectedStatus));
    }

    // ── Operation 3: Resubmit processing-pending ──────────────────────────────

    private async Task ResubmitProcessingPendingListAsync()
    {
        var items = await ReadListFileAsync(_queueSettings.ProcessingPendingListFilePath);
        if (items.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"Processing-pending list: {items.Count} item(s) in {_queueSettings.ProcessingPendingListFilePath}.");
        Console.WriteLine();
        Console.WriteLine("  WARNING: These orders have processedstatus = 'Processing' in the database.");
        Console.WriteLine("  A live API instance may still be processing them. Resubmitting could cause");
        Console.WriteLine("  duplicate notifications if processing completes concurrently.");
        Console.WriteLine();
        Console.WriteLine("  Only proceed if you are certain no API instance is currently processing these orders.");
        Console.WriteLine();
        Console.Write("  Proceed? (y/N): ");

        if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Aborted.");
            return;
        }

        Console.WriteLine();

        await using var sender = _sbClient.CreateSender(_asbSettings.PastDueOrdersQueueName);

        await ProcessDlqItemsAsync(
            items,
            (msg, item, rcv) => ResubmitItemAsync(msg, item, rcv, sender, expectedStatus: "Processing"));
    }

    // ── Operation 4-7: Purge lists ────────────────────────────────────────────

    private async Task PurgeListAsync(string filePath, string label)
    {
        var items = await ReadListFileAsync(filePath);
        if (items.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"Purging {label} list ({filePath})...");
        Console.WriteLine($"{items.Count} item(s) to purge. No DB changes will be made.");
        Console.WriteLine();
        Console.WriteLine("  Edit the list file first to remove any items you do not want purged yet.");
        Console.WriteLine();
        Console.Write("  Proceed? (y/N): ");

        if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Aborted.");
            return;
        }

        Console.WriteLine();

        await ProcessDlqItemsAsync(
            items,
            async (msg, item, rcv) =>
            {
                await rcv.CompleteMessageAsync(msg);
                return (true, $"DLQ message purged (DB status: {item.CurrentDbStatus ?? _notFound}, notifications: {item.NotificationCount}).");
            });
    }

    // ── Operation 8: Query DB state ───────────────────────────────────────────

    private async Task QueryDbStateAsync()
    {
        Console.WriteLine();
        Console.WriteLine("Query DB state — which list file?");
        Console.WriteLine($"  1. Registered-pending  ({_queueSettings.RegisteredPendingListFilePath})");
        Console.WriteLine($"  2. Registered-expired  ({_queueSettings.RegisteredExpiredListFilePath})");
        Console.WriteLine($"  3. Processing-pending  ({_queueSettings.ProcessingPendingListFilePath})");
        Console.WriteLine($"  4. Processing-expired  ({_queueSettings.ProcessingExpiredListFilePath})");
        Console.WriteLine($"  5. Has-notifications   ({_queueSettings.HasNotificationsListFilePath})");
        Console.WriteLine($"  6. Other-status        ({_queueSettings.OtherStatusListFilePath})");
        Console.WriteLine();
        Console.Write("Enter choice (1-6): ");

        string filePath = Console.ReadLine()?.Trim() switch
        {
            "1" => _queueSettings.RegisteredPendingListFilePath,
            "2" => _queueSettings.RegisteredExpiredListFilePath,
            "3" => _queueSettings.ProcessingPendingListFilePath,
            "4" => _queueSettings.ProcessingExpiredListFilePath,
            "5" => _queueSettings.HasNotificationsListFilePath,
            "6" => _queueSettings.OtherStatusListFilePath,
            _   => string.Empty
        };

        if (string.IsNullOrEmpty(filePath))
        {
            Console.WriteLine("Invalid choice.");
            return;
        }

        var items = await ReadListFileAsync(filePath);
        if (items.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"DB state for {filePath} ({items.Count} item(s)):");
        Console.WriteLine();
        Console.WriteLine(
            $"  {"OrderId",-38} {"DbStatus",-22} {"Notifs",6}  ExpiryTime");
        Console.WriteLine(
            $"  {new string('-', 38)} {new string('-', 22)} {new string('-', 6)}  {new string('-', 22)}");

        var states = await _repository.GetOrderStatesAsync(
            items.Select(i => i.OrderId).ToList());

        foreach (var item in items)
        {
            states.TryGetValue(item.OrderId, out var state);
            var (status, count, expiryTime, _) = state;

            Console.WriteLine(
                $"  {item.OrderId,-38} {status ?? _notFound,-22} {count,6}  {FormatUtc(expiryTime)}");
        }
    }

    // ── Resubmit helper ───────────────────────────────────────────────────────

    /// <summary>
    /// Re-checks DB state before resubmitting. Guards against duplicates and status changes
    /// since the list file was produced by Inspect.
    /// </summary>
    private async Task<(bool Success, string Message)> ResubmitItemAsync(
        ServiceBusReceivedMessage msg,
        DlqPastDueOrderItem item,
        ServiceBusReceiver rcv,
        ServiceBusSender sender,
        string expectedStatus)
    {
        var (currentStatus, notifCount, _, isExpired) = await _repository.GetOrderStateAsync(item.OrderId);

        if (notifCount > 0)
        {
            await rcv.CompleteMessageAsync(msg);
            return (true, $"Notifications already exist ({notifCount}) — DLQ message purged without resubmit.");
        }

        if (isExpired)
        {
            await rcv.CompleteMessageAsync(msg);
            return (true, "Order expired since inspection — DLQ message purged without resubmit.");
        }

        if (currentStatus != expectedStatus)
        {
            await rcv.CompleteMessageAsync(msg);
            return (true, $"DB status is '{currentStatus ?? _notFound}' (expected '{expectedStatus}') — DLQ message purged without resubmit.");
        }

        // Use the original DLQ message body verbatim so the full NotificationOrder
        // (including templates, recipients, etc.) is preserved without re-serialization.
        var newMsg = new ServiceBusMessage(msg.Body)
        {
            ContentType = msg.ContentType,
            Subject     = msg.Subject,
            // Fresh MessageId so ASB duplicate-detection does not silently drop the message.
            MessageId = Guid.NewGuid().ToString()
        };

        // Copy Wolverine envelope application properties (message-type header, correlation,
        // etc.) so the handler can route and deserialize the message correctly.
        foreach (var (key, value) in msg.ApplicationProperties)
        {
            newMsg.ApplicationProperties[key] = value;
        }

        try
        {
            await sender.SendMessageAsync(newMsg);
            await rcv.CompleteMessageAsync(msg);
            return (true, "Sent to main queue. DLQ message purged.");
        }
        catch (Exception ex)
        {
            await rcv.AbandonMessageAsync(msg);
            return (false, $"Send failed ({ex.GetType().Name}: {ex.Message}). DLQ message NOT purged.");
        }
    }

    // ── Shared processing helper ──────────────────────────────────────────────

    private async Task ProcessDlqItemsAsync(
        List<DlqPastDueOrderItem> items,
        Func<ServiceBusReceivedMessage, DlqPastDueOrderItem, ServiceBusReceiver, Task<(bool Success, string Message)>> processItem)
    {
        var targetByMessageId = items.ToDictionary(i => i.DlqMessageId);

        Console.WriteLine("  Scanning DLQ for matches...");
        int remainingOnDlq = await PeekDlqMatchCountAsync(targetByMessageId.Keys.ToHashSet());

        if (remainingOnDlq == 0)
        {
            Console.WriteLine($"  0 of {items.Count} item(s) from list found on DLQ — nothing to process.");
            return;
        }

        Console.WriteLine($"  {remainingOnDlq} of {items.Count} item(s) from list still on DLQ.");
        Console.WriteLine();

        int batchSize = GetDlqBatchSize(items.Count);

        await using var receiver = _sbClient.CreateReceiver(
            _asbSettings.PastDueOrdersQueueName,
            new ServiceBusReceiverOptions
            {
                SubQueue    = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        int succeeded = 0, failed = 0, index = 0, matchedCount = 0;

        while (remainingOnDlq < 0 || matchedCount < remainingOnDlq)
        {
            var snapshot = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromMinutes(2));
            if (snapshot.Count == 0)
                break;

            foreach (var msg in snapshot)
            {
                bool? result = await ProcessOneMessageAsync(
                    msg, targetByMessageId, index + 1, remainingOnDlq, receiver, processItem);

                if (result is null)
                    continue;

                matchedCount++;
                index++;
                if (result.Value) succeeded++; else failed++;
            }
        }

        int notFound = items.Count - matchedCount;
        if (notFound > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  [WARN] {notFound} item(s) from the list were not found in the DLQ snapshot " +
                "(they may have already been processed or expired from the DLQ).");
        }

        int stillOnDlq = Math.Max(0, remainingOnDlq - succeeded);
        Console.WriteLine();
        Console.WriteLine($"Done. {succeeded} succeeded, {failed} failed, {notFound} not found in DLQ. {stillOnDlq} still on DLQ from this list.");
    }

    private static async Task<bool?> ProcessOneMessageAsync(
        ServiceBusReceivedMessage msg,
        Dictionary<string, DlqPastDueOrderItem> targetByMessageId,
        int displayIndex,
        int totalExpected,
        ServiceBusReceiver receiver,
        Func<ServiceBusReceivedMessage, DlqPastDueOrderItem, ServiceBusReceiver, Task<(bool Success, string Message)>> processItem)
    {
        if (!targetByMessageId.TryGetValue(msg.MessageId, out var item))
        {
            await receiver.AbandonMessageAsync(msg);
            return null;
        }

        Console.Write($"  [{displayIndex}/{totalExpected}] {item.OrderId}... ");

        try
        {
            var (success, message) = await processItem(msg, item, receiver);
            Console.WriteLine($"→ {message} {(success ? "✓" : "✗")}");
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"→ Unexpected error: {ex.Message} ✗");
            try { await receiver.AbandonMessageAsync(msg); } catch { /* best-effort */ }
            return false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> PeekDlqMatchCountAsync(HashSet<string> targetMessageIds)
    {
        try
        {
            return await CountDlqMatchesAsync(targetMessageIds);
        }
        catch
        {
            return -1;
        }
    }

    private async Task<int> CountDlqMatchesAsync(HashSet<string> targetMessageIds)
    {
        await using var receiver = _sbClient.CreateReceiver(
            _asbSettings.PastDueOrdersQueueName,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        int matchCount = 0;
        long fromSeq = 0;

        while (true)
        {
            var page = await receiver.PeekMessagesAsync(maxMessages: 100, fromSequenceNumber: fromSeq);
            if (page.Count == 0)
                break;

            matchCount += page.Count(m => targetMessageIds.Contains(m.MessageId));
            fromSeq = page[^1].SequenceNumber + 1;
        }

        return matchCount;
    }

    private Task<long> GetDlqCountAsync() => PeekCountDlqAsync();

    private async Task<long> PeekCountDlqAsync()
    {
        try
        {
            return await CountDlqMessagesAsync();
        }
        catch
        {
            return -1;
        }
    }

    private async Task<long> CountDlqMessagesAsync()
    {
        await using var receiver = _sbClient.CreateReceiver(
            _asbSettings.PastDueOrdersQueueName,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        long count = 0;
        long fromSeq = 0;

        while (true)
        {
            var page = await receiver.PeekMessagesAsync(maxMessages: 100, fromSequenceNumber: fromSeq);
            if (page.Count == 0)
                break;

            count += page.Count;
            fromSeq = page[^1].SequenceNumber + 1;
        }

        return count;
    }

    private static int GetDlqBatchSize(int minSize) =>
        Math.Min(minSize + 50, 4095);

    private static CommandDto? TryDeserializeCommand(ServiceBusReceivedMessage msg)
    {
        try
        {
            return JsonSerializer.Deserialize<CommandDto>(msg.Body.ToString(), _readOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void PrintTableHeader()
    {
        Console.WriteLine(
            $"  {"OrderId",-38} {"DbStatus",-22} {"Notifs",5}  {"ExpiryTime",-22} Category");
        Console.WriteLine(
            $"  {new string('-', 38)} {new string('-', 22)} {new string('-', 5)}  {new string('-', 22)} --------");
    }

    private static string FormatUtc(DateTime? dt) =>
        dt.HasValue ? dt.Value.ToString("u") : "N/A";

    private static string ChannelName(int channel) => channel switch
    {   
        0 => "Email",
        1 => "Sms",
        2 => "EmailPreferred",
        3 => "SmsPreferred",
        4 => "EmailAndSms",
        _ => $"Unknown({channel})"
    };

    private static async Task WriteListFileAsync(string filePath, List<DlqPastDueOrderItem> items)
    {
        var json = JsonSerializer.Serialize(items, _writeOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    private static async Task<List<DlqPastDueOrderItem>> ReadListFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}. Run 'Inspect DLQ' first.");
            return [];
        }

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<List<DlqPastDueOrderItem>>(json)
            ?? throw new InvalidOperationException(
                $"File '{filePath}' contains invalid JSON (deserialized to null). " +
                "Ensure it was produced by the 'Inspect DLQ' operation.");
    }

    public async ValueTask DisposeAsync()
    {
        await _sbClient.DisposeAsync();
    }

    // ── Local deserialization DTOs ─────────────────────────────────────────────
    // ProcessPastDueOrderCommand and NotificationOrder live in the main API project
    // which is not referenced by this tool. These minimal private nested DTOs capture
    // only the fields needed for inspection and categorization. Wolverine serializes
    // with camelCase and integer-valued enums; PropertyNameCaseInsensitive handles both.

    private sealed class CommandDto
    {
        [JsonPropertyName("order")]
        public OrderDto? Order { get; init; }

        [JsonPropertyName("isProcessOrderRetry")]
        public bool IsProcessOrderRetry { get; init; }
    }

    private sealed class OrderDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; init; }

        [JsonPropertyName("requestedSendTime")]
        public DateTime RequestedSendTime { get; init; }

        /// <summary>
        /// Integer representation of NotificationChannel enum:
        /// 0 = Email, 1 = Sms, 2 = EmailPreferred, 3 = SmsPreferred, 4 = EmailAndSms.
        /// </summary>
        [JsonPropertyName("notificationChannel")]
        public int NotificationChannel { get; init; }
    }
}
