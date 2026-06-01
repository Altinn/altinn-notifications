using System.Text;
using System.Text.Json;
using Altinn.Notifications.Tools.DlqManager.Configuration;
using Altinn.Notifications.Tools.DlqManager.Models;
using Altinn.Notifications.Tools.DlqManager.Repositories;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.DlqManager.Services.Queues;

/// <summary>
/// Implements the DLQ operations for <c>altinn.notifications.sms.send</c>:
/// <list type="number">
///   <item>Inspect — peek all DLQ messages, classify into three list files.</item>
///   <item>Process sending-expired — mark DB <c>Accepted</c> + purge DLQ messages.</item>
///   <item>Process sending-pending — resubmit to main queue + purge DLQ messages.</item>
///   <item>Purge other-status — purge DLQ messages only, no DB change or re-queue.</item>
///   <item>Query DB state — print current database state for a list file.</item>
/// </list>
/// </summary>
public sealed class SmsSendQueueService : ISmsSendQueueService, IAsyncDisposable
{
    private readonly AsbSettings _asbSettings;
    private readonly SmsSendQueueSettings _queueSettings;
    private readonly ISmsNotificationRepository _repository;
    private readonly ServiceBusClient _sbClient;

    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    private const string NotFound = "NOT FOUND";

    public SmsSendQueueService(
        IOptions<AsbSettings> asbSettings,
        IOptions<SmsSendQueueSettings> queueSettings,
        ISmsNotificationRepository repository,
        ServiceBusClient sbClient)
    {
        _asbSettings = asbSettings.Value;
        _queueSettings = queueSettings.Value;
        _repository = repository;
        _sbClient = sbClient;
    }

    // ── Top-level sub-menu loop ────────────────────────────────────────────────

    public async Task RunMenuAsync()
    {
        while (true)
        {
            long dlqCount = await GetDlqCountAsync();
            string dlqCountDisplay = dlqCount >= 0 ? dlqCount.ToString() : "N/A";

            Console.WriteLine();
            Console.WriteLine("=== SMS Send Queue — DLQ Manager ===");
            Console.WriteLine($"Queue    : {_asbSettings.SmsSendQueueName}");
            Console.WriteLine($"DLQ count: {dlqCountDisplay}");
            Console.WriteLine();
            Console.WriteLine("  1. Inspect DLQ — classify all messages and save to list files");
            Console.WriteLine("  2. Process sending-expired list — mark DB 'Accepted' + purge DLQ messages");
            Console.WriteLine("  3. Process sending-pending list — resubmit to main queue + purge DLQ messages");
            Console.WriteLine("  4. Purge other-status list — purge DLQ messages only (no DB change)");
            Console.WriteLine("  5. Query DB state for a list file");
            Console.WriteLine("  0. Back");
            Console.WriteLine();
            Console.Write("Enter choice (0-5): ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1": await InspectDlqAsync();                break;
                case "2": await ProcessSendingExpiredListAsync(); break;
                case "3": await ProcessSendingPendingListAsync(); break;
                case "4": await PurgeOtherStatusListAsync();      break;
                case "5": await QueryDbStateAsync();              break;
                case "0": return;
                default:
                    Console.WriteLine("Invalid choice. Please enter 0-5.");
                    break;
            }
        }
    }

    // ── Operation 1: Inspect ──────────────────────────────────────────────────

    private async Task InspectDlqAsync()
    {
        Console.WriteLine();
        Console.WriteLine($"Inspecting DLQ for {_asbSettings.SmsSendQueueName}...");

        await using var receiver = _sbClient.CreateReceiver(
            _asbSettings.SmsSendQueueName,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        // Peek in pages — does not lock the messages.
        var allMessages = new List<ServiceBusReceivedMessage>();
        long fromSeq = 0;

        while (true)
        {
            var page = await receiver.PeekMessagesAsync(
                maxMessages: 100,
                fromSequenceNumber: fromSeq);

            if (page.Count == 0)
                break;

            allMessages.AddRange(page);
            fromSeq = page[^1].SequenceNumber + 1;
        }

        Console.WriteLine($"Found {allMessages.Count} message(s).");
        Console.WriteLine();

        var sendingExpired = new List<DlqSmsItem>();
        var sendingPending = new List<DlqSmsItem>();
        var other          = new List<DlqSmsItem>();

        PrintTableHeader();

        foreach (var msg in allMessages)
        {
            SendSmsCommand? cmd = TryDeserializeCommand(msg);
            if (cmd is null)
            {
                Console.WriteLine($"  [WARN] Could not deserialize message {msg.MessageId} — skipping.");
                continue;
            }

            var (result, expiryTime, isExpired, _) = await _repository.GetNotificationStateAsync(cmd.NotificationId);

            bool isSending = result == "Sending";

            string statusLabel = (isSending, isExpired) switch
            {
                (true,  true)  => "SENDING/EXPIRED",
                (true,  false) => "SENDING/PENDING",
                _              => $"OTHER ({result ?? NotFound})"
            };

            Console.WriteLine(
                $"  {cmd.NotificationId,-38} {result ?? NotFound,-28} " +
                $"{FormatUtc(expiryTime),-22} {statusLabel}");

            var item = new DlqSmsItem
            {
                NotificationId                = cmd.NotificationId,
                MobileNumber                  = cmd.MobileNumber,
                Body                          = cmd.Body,
                SenderNumber                  = cmd.SenderNumber,
                CurrentDbResult               = result,
                ExpiryTime                    = expiryTime,
                DlqMessageId                  = msg.MessageId,
                DlqEnqueuedTime               = msg.EnqueuedTime.UtcDateTime,
                DlqDeadLetterReason           = msg.DeadLetterReason,
                DlqDeadLetterErrorDescription = msg.DeadLetterErrorDescription
            };

            if (isSending && isExpired)  sendingExpired.Add(item);
            else if (isSending)          sendingPending.Add(item);
            else                         other.Add(item);
        }

        Console.WriteLine();
        Console.WriteLine($"Sending/expired : {sendingExpired.Count}");
        Console.WriteLine($"Sending/pending : {sendingPending.Count}");
        Console.WriteLine($"Other status    : {other.Count}");

        await WriteListFileAsync(_queueSettings.SendingExpiredListFilePath, sendingExpired);
        await WriteListFileAsync(_queueSettings.SendingPendingListFilePath, sendingPending);
        await WriteListFileAsync(_queueSettings.OtherStatusListFilePath,    other);

        Console.WriteLine();
        Console.WriteLine($"Saved: {_queueSettings.SendingExpiredListFilePath} ({sendingExpired.Count} item(s))");
        Console.WriteLine($"Saved: {_queueSettings.SendingPendingListFilePath} ({sendingPending.Count} item(s))");
        Console.WriteLine($"Saved: {_queueSettings.OtherStatusListFilePath} ({other.Count} item(s))");
    }

    // ── Operation 2: Process sending-expired ──────────────────────────────────

    private async Task ProcessSendingExpiredListAsync()
    {
        var items = await ReadListFileAsync(_queueSettings.SendingExpiredListFilePath);
        if (items.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"Processing sending-expired list ({_queueSettings.SendingExpiredListFilePath})...");
        Console.WriteLine($"{items.Count} item(s) to process.");
        Console.WriteLine();

        await ProcessDlqItemsAsync(
            items,
            async (msg, item, rcv) =>
            {
                var (currentResult, _, _, _) = await _repository.GetNotificationStateAsync(item.NotificationId);

                if (currentResult != "Sending")
                {
                    await rcv.CompleteMessageAsync(msg);
                    return (true, $"DB result is '{currentResult ?? NotFound}' (not Sending) — DLQ message purged without DB change.");
                }

                int rows = await _repository.UpdateResultToAcceptedAsync(item.NotificationId);
                if (rows > 0)
                {
                    await rcv.CompleteMessageAsync(msg);
                    return (true, "DB updated to 'Accepted'. DLQ message purged.");
                }

                await rcv.AbandonMessageAsync(msg);
                return (false, "DB update returned 0 rows (expirytime may not be in the past yet). DLQ message NOT purged.");
            });
    }

    // ── Operation 3: Process sending-pending ──────────────────────────────────

    private async Task ProcessSendingPendingListAsync()
    {
        var items = await ReadListFileAsync(_queueSettings.SendingPendingListFilePath);
        if (items.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"Processing sending-pending list ({_queueSettings.SendingPendingListFilePath})...");
        Console.WriteLine($"{items.Count} item(s) to process.");
        Console.WriteLine();

        await using var sender = _sbClient.CreateSender(_asbSettings.SmsSendQueueName);

        await ProcessDlqItemsAsync(
            items,
            async (msg, item, rcv) =>
            {
                var command = new SendSmsCommand
                {
                    NotificationId = item.NotificationId,
                    MobileNumber   = item.MobileNumber,
                    Body           = item.Body,
                    SenderNumber   = item.SenderNumber
                };

                var newMsg = new ServiceBusMessage(
                    BinaryData.FromString(JsonSerializer.Serialize(command)))
                {
                    ContentType = "application/json",
                    Subject     = msg.Subject,
                    // Fresh MessageId so the duplicate-detection window on the main
                    // queue does not silently drop the re-queued message (the original
                    // was sent recently enough to still be inside the window).
                    MessageId = Guid.NewGuid().ToString()
                };

                // Wolverine requires its envelope application properties (at minimum
                // the message-type header) to route and deserialize the message.
                // Copy them from the original DLQ message — they survive DLQ intact.
                // The ASB delivery count resets automatically on the new message, so
                // the retry policy gets a clean slate regardless.
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
            });
    }

    // ── Operation 4: Purge other-status ───────────────────────────────────────

    private async Task PurgeOtherStatusListAsync()
    {
        var items = await ReadListFileAsync(_queueSettings.OtherStatusListFilePath);
        if (items.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"Purging other-status list ({_queueSettings.OtherStatusListFilePath})...");
        Console.WriteLine($"{items.Count} item(s) to purge.");
        Console.WriteLine();
        Console.WriteLine(
            "  These messages have a DB status other than 'Sending'. No DB changes will be made.");
        Console.WriteLine(
            "  Edit the list file first to remove any items you do not want purged yet.");
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
                return (true, $"DLQ message purged (DB result: {item.CurrentDbResult ?? NotFound}).");
            });
    }

    // ── Operation 5: Query DB state ───────────────────────────────────────────

    private async Task QueryDbStateAsync()
    {
        Console.WriteLine();
        Console.WriteLine("Query DB state — which list file?");
        Console.WriteLine($"  1. Sending-expired ({_queueSettings.SendingExpiredListFilePath})");
        Console.WriteLine($"  2. Sending-pending ({_queueSettings.SendingPendingListFilePath})");
        Console.WriteLine($"  3. Other status    ({_queueSettings.OtherStatusListFilePath})");
        Console.WriteLine();
        Console.Write("Enter choice (1-3): ");

        string filePath = Console.ReadLine()?.Trim() switch
        {
            "1" => _queueSettings.SendingExpiredListFilePath,
            "2" => _queueSettings.SendingPendingListFilePath,
            "3" => _queueSettings.OtherStatusListFilePath,
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
            $"  {"NotificationId",-38} {"DbResult",-28} {"ExpiryTime",-22} ResultTime");
        Console.WriteLine(
            $"  {new string('-', 38)} {new string('-', 28)} {new string('-', 22)} {new string('-', 22)}");

        // Batch all DB queries in parallel instead of sequential per-item awaits.
        var states = await Task.WhenAll(
            items.Select(item => _repository.GetNotificationStateAsync(item.NotificationId)));

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var (result, expiryTime, _, resultTime) = states[i];

            Console.WriteLine(
                $"  {item.NotificationId,-38} {result ?? NotFound,-28} " +
                $"{FormatUtc(expiryTime),-22} {FormatUtc(resultTime)}");
        }
    }

    // ── Shared processing helper ──────────────────────────────────────────────

    /// <summary>
    /// Receives the current DLQ snapshot in one batch, processes messages that match
    /// <paramref name="items"/> (by <c>MessageId</c>), and abandons any non-target messages.
    /// </summary>
    private async Task ProcessDlqItemsAsync(
        List<DlqSmsItem> items,
        Func<ServiceBusReceivedMessage, DlqSmsItem, ServiceBusReceiver, Task<(bool Success, string Message)>> processItem)
    {
        var targetByMessageId = items.ToDictionary(i => i.DlqMessageId);

        int batchSize = GetDlqBatchSize(items.Count);

        await using var receiver = _sbClient.CreateReceiver(
            _asbSettings.SmsSendQueueName,
            new ServiceBusReceiverOptions
            {
                SubQueue    = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        // Receive the current DLQ snapshot. ReceiveMessagesAsync returns when it reaches
        // maxMessages OR when maxWaitTime expires. The 2-minute timeout gives it enough
        // time to drain large DLQs (hundreds to low thousands of messages) in one call.
        var snapshot = await receiver.ReceiveMessagesAsync(
            maxMessages: batchSize,
            maxWaitTime: TimeSpan.FromMinutes(2));

        int succeeded = 0, failed = 0, index = 0;
        var foundIds = new HashSet<string>(snapshot.Count);

        foreach (var msg in snapshot)
        {
            foundIds.Add(msg.MessageId);

            if (!targetByMessageId.TryGetValue(msg.MessageId, out var item))
            {
                // Not in our target set — release the lock immediately.
                await receiver.AbandonMessageAsync(msg);
                continue;
            }

            index++;
            Console.Write($"  [{index}/{items.Count}] {item.NotificationId}... ");

            try
            {
                var (success, message) = await processItem(msg, item, receiver);
                Console.WriteLine($"→ {message} {(success ? "✓" : "✗")}");

                if (success) succeeded++;
                else         failed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"→ Unexpected error: {ex.Message} ✗");
                try { await receiver.AbandonMessageAsync(msg); } catch { /* best-effort */ }
                failed++;
            }
        }

        // Report items from the file that were not present in the DLQ snapshot.
        int notFound = items.Count(i => !foundIds.Contains(i.DlqMessageId));
        if (notFound > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  [WARN] {notFound} item(s) from the list were not found in the DLQ snapshot " +
                "(they may have already been processed or expired from the DLQ).");
        }

        Console.WriteLine();
        Console.WriteLine($"Done. {succeeded} succeeded, {failed} failed, {notFound} not found in DLQ.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Counts DLQ messages via AMQP peek — works on both the local ASB emulator and
    /// real Azure Service Bus without needing the REST management endpoint.
    /// Returns <c>-1</c> if the peek fails.
    /// </summary>
    private Task<long> GetDlqCountAsync() => PeekCountDlqAsync();

    /// <summary>
    /// Counts DLQ messages by paging through them with <c>PeekMessagesAsync</c>.
    /// Does not lock or consume any messages.
    /// </summary>
    private async Task<long> PeekCountDlqAsync()
    {
        try
        {
            await using var receiver = _sbClient.CreateReceiver(
                _asbSettings.SmsSendQueueName,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

            long count = 0;
            long fromSeq = 0;

            while (true)
            {
                var page = await receiver.PeekMessagesAsync(
                    maxMessages: 100,
                    fromSequenceNumber: fromSeq);

                if (page.Count == 0)
                    break;

                count += page.Count;
                fromSeq = page[^1].SequenceNumber + 1;
            }

            return count;
        }
        catch
        {
            return -1;
        }
    }

    private static int GetDlqBatchSize(int minSize)
    {
        // Generous fixed buffer above the file's item count so we capture any
        // messages that arrived in the DLQ after the file was written.
        // Clamped to 4095 — the Azure Service Bus premium receiver maximum.
        return Math.Min(minSize + 50, 4095);
    }

    private static SendSmsCommand? TryDeserializeCommand(ServiceBusReceivedMessage msg)
    {
        try
        {
            return JsonSerializer.Deserialize<SendSmsCommand>(msg.Body.ToString());
        }
        catch
        {
            return null;
        }
    }

    private static void PrintTableHeader()
    {
        Console.WriteLine(
            $"  {"NotificationId",-38} {"DbResult",-28} {"ExpiryTime",-22} Status");
        Console.WriteLine(
            $"  {new string('-', 38)} {new string('-', 28)} {new string('-', 22)} -------");
    }

    private static string FormatUtc(DateTime? dt) =>
        dt.HasValue ? dt.Value.ToString("u") : "N/A";

    private static async Task WriteListFileAsync(string filePath, List<DlqSmsItem> items)
    {
        var json = JsonSerializer.Serialize(items, _writeOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    private static async Task<List<DlqSmsItem>> ReadListFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}. Run 'Inspect DLQ' first.");
            return [];
        }

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<List<DlqSmsItem>>(json)
            ?? throw new InvalidOperationException(
                $"File '{filePath}' contains invalid JSON (deserialized to null). " +
                "Ensure it was produced by the 'Inspect DLQ' operation.");
    }

    public async ValueTask DisposeAsync()
    {
        await _sbClient.DisposeAsync();
    }
}
