using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.DlqManager.Configuration;
using Altinn.Notifications.Tools.DlqManager.Models;
using Altinn.Notifications.Tools.DlqManager.Repositories;
using Altinn.Notifications.Tools.DlqManager.Services.Queues;
using Altinn.Notifications.Tools.Tests.Infrastructure;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Options;

using Npgsql;
using NpgsqlTypes;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.DlqManager;

/// <summary>
/// Integration tests for <see cref="SmsSendQueueService"/> against real infrastructure:
/// a live Azure Service Bus emulator (for queue/DLQ interactions) and a live PostgreSQL
/// database (for notification state assertions).
///
/// Each test drives the service via <c>Console.SetIn</c> (mimicking the interactive menu)
/// and asserts both queue state and database state after the operation completes.
/// </summary>
[Collection(nameof(IntegrationContainersCollection))]
public class SmsSendQueueServiceTests(IntegrationContainersFixture fixture) : IAsyncLifetime
{
    private const string _queueName = "altinn.notifications.sms.send";

    private readonly IntegrationContainersFixture _fixture = fixture;
    private readonly List<Guid> _notificationIds = [];
    private readonly List<Guid> _orderIds = [];
    private readonly List<string> _tempFiles = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (_notificationIds.Count > 0 || _orderIds.Count > 0)
        {
            await using var cmd = _fixture.DataSource.CreateCommand(
                "DELETE FROM notifications.smsnotifications WHERE alternateid = ANY(@ids);" +
                "DELETE FROM notifications.orders WHERE alternateid = ANY(@orderIds);");
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = _notificationIds.ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("orderIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = _orderIds.ToArray() });
            await cmd.ExecuteNonQueryAsync();
        }

        await DrainQueueAsync(_queueName);
        await DrainQueueAsync($"{_queueName}/$deadletterqueue");
    }

    // ── Inspect DLQ ───────────────────────────────────────────────────────────
    [Fact]
    public async Task InspectDlq_WhenMessageHasSendingResultAndPastExpiry_ClassifiesAsExpired()
    {
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, pendingPath, otherPath, queueSettings) = CreateTempListFiles();

        await RunMenuAsync(CreateService(queueSettings), "1\n0\n");

        var expiredItems = await ReadListFileAsync(expiredPath);
        Assert.Contains(expiredItems, i => i.NotificationId == notificationId && i.DlqMessageId == msgId);
        Assert.Empty(await ReadListFileAsync(pendingPath));
        Assert.Empty(await ReadListFileAsync(otherPath));
    }

    [Fact]
    public async Task InspectDlq_WhenMessageHasSendingResultAndFutureExpiry_ClassifiesAsPending()
    {
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(1));
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, pendingPath, otherPath, queueSettings) = CreateTempListFiles();

        await RunMenuAsync(CreateService(queueSettings), "1\n0\n");

        var pendingItems = await ReadListFileAsync(pendingPath);
        Assert.Contains(pendingItems, i => i.NotificationId == notificationId && i.DlqMessageId == msgId);
        Assert.Empty(await ReadListFileAsync(expiredPath));
        Assert.Empty(await ReadListFileAsync(otherPath));
    }

    [Fact]
    public async Task InspectDlq_WhenMessageHasNonSendingResult_ClassifiesAsOther()
    {
        var (notificationId, command) = await SeedNotificationAsync("Accepted", DateTime.UtcNow.AddHours(1));
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, pendingPath, otherPath, queueSettings) = CreateTempListFiles();

        await RunMenuAsync(CreateService(queueSettings), "1\n0\n");

        var otherItems = await ReadListFileAsync(otherPath);
        Assert.Contains(otherItems, i => i.NotificationId == notificationId && i.DlqMessageId == msgId);
        Assert.Empty(await ReadListFileAsync(expiredPath));
        Assert.Empty(await ReadListFileAsync(pendingPath));
    }

    // ── Process sending-expired ───────────────────────────────────────────────
    [Fact]
    public async Task ProcessSendingExpired_WhenDbIsSendingAndExpired_UpdatesDbToAcceptedAndPurgesDlqMessage()
    {
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, _, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(expiredPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        await RunMenuAsync(CreateService(queueSettings), "2\n0\n");

        var (result, _, _, resultTime) = await new SmsNotificationRepository(_fixture.DataSource).GetNotificationStateAsync(notificationId);
        Assert.Equal("Accepted", result);
        Assert.NotNull(resultTime);
        Assert.True(await WaitForDlqEmptyAsync(), "DLQ should be empty after processing sending-expired");
    }

    [Fact]
    public async Task ProcessSendingExpired_WhenDbStateAlreadyChanged_PurgesDlqMessageWithoutDbUpdate()
    {
        var (notificationId, command) = await SeedNotificationAsync("Accepted", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, _, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(expiredPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        await RunMenuAsync(CreateService(queueSettings), "2\n0\n");

        var (result, _, _, _) = await new SmsNotificationRepository(_fixture.DataSource).GetNotificationStateAsync(notificationId);
        Assert.Equal("Accepted", result);
        Assert.True(await WaitForDlqEmptyAsync(), "DLQ message should still be purged when DB state has already changed");
    }

    [Fact]
    public async Task ProcessSendingExpired_WhenDbUpdateReturnsZero_AbandonsDlqMessage()
    {
        // Notification is Sending but expirytime is in the future — the UPDATE WHERE predicate fails.
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(1));
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, _, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(expiredPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        await RunMenuAsync(CreateService(queueSettings), "2\n0\n");

        var (result, _, _, _) = await new SmsNotificationRepository(_fixture.DataSource).GetNotificationStateAsync(notificationId);
        Assert.Equal("Sending", result);
        Assert.True(await WaitForDlqMessageAsync(msgId), "DLQ message should remain when the DB update predicate is not met");
    }

    // ── Process sending-pending ───────────────────────────────────────────────
    [Fact]
    public async Task ProcessSendingPending_ResubmitsMessageToMainQueueAndPurgesDlqMessage()
    {
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(1));
        string msgId = await SeedDlqMessageAsync(command);
        var (_, pendingPath, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(pendingPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        await RunMenuAsync(CreateService(queueSettings), "3\n0\n");

        Assert.True(await WaitForMainQueueMessageAsync(notificationId), "A resubmitted SendSmsCommand should appear on the main queue");
        Assert.True(await WaitForDlqEmptyAsync(), "DLQ should be empty after resubmitting the pending message");
    }

    // ── Purge other-status ────────────────────────────────────────────────────
    [Fact]
    public async Task PurgeOtherStatus_WhenUserConfirms_PurgesDlqMessage()
    {
        var (notificationId, command) = await SeedNotificationAsync("Delivered", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (_, _, otherPath, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(otherPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        await RunMenuAsync(CreateService(queueSettings), "4\ny\n0\n");

        Assert.True(await WaitForDlqEmptyAsync(), "DLQ message should be purged when the operator confirms");
    }

    [Fact]
    public async Task PurgeOtherStatus_WhenUserDeclines_DoesNotPurgeDlqMessage()
    {
        var (notificationId, command) = await SeedNotificationAsync("Delivered", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (_, _, otherPath, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(otherPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        await RunMenuAsync(CreateService(queueSettings), "4\nn\n0\n");

        Assert.True(await WaitForDlqMessageAsync(msgId), "DLQ message should NOT be purged when the operator declines");
    }

    // ── Helpers: DB ──────────────────────────────────────────────────────────
    private async Task<(Guid NotificationId, SendSmsCommand Command)> SeedNotificationAsync(
        string result,
        DateTime expiryTime)
    {
        var orderId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        _orderIds.Add(orderId);
        _notificationIds.Add(notificationId);

        await using var insertOrder = _fixture.DataSource.CreateCommand("""
            INSERT INTO notifications.orders
                (alternateid, creatorname, sendersreference, created, requestedsendtime, notificationorder)
            VALUES (@orderId, 'dlq-test', 'dlq-test-ref', NOW(), NOW(), '{}')
            RETURNING _id
            """);
        insertOrder.Parameters.Add(new NpgsqlParameter("orderId", NpgsqlDbType.Uuid) { Value = orderId });
        long orderDbId = (long)(await insertOrder.ExecuteScalarAsync())!;

        await using var insertNotification = _fixture.DataSource.CreateCommand("""
            INSERT INTO notifications.smsnotifications
                (_orderid, alternateid, mobilenumber, result, resulttime, expirytime)
            VALUES (@orderDbId, @notificationId, '+4712345678',
                    @result::smsnotificationresulttype, NOW(), @expiryTime)
            """);
        insertNotification.Parameters.Add(new NpgsqlParameter("orderDbId", NpgsqlDbType.Bigint) { Value = orderDbId });
        insertNotification.Parameters.Add(new NpgsqlParameter("notificationId", NpgsqlDbType.Uuid) { Value = notificationId });
        insertNotification.Parameters.Add(new NpgsqlParameter("result", NpgsqlDbType.Text) { Value = result });
        insertNotification.Parameters.Add(new NpgsqlParameter("expiryTime", NpgsqlDbType.TimestampTz) { Value = expiryTime });
        await insertNotification.ExecuteNonQueryAsync();

        return (notificationId, new SendSmsCommand
        {
            NotificationId = notificationId,
            MobileNumber = "+4712345678",
            Body = "Test message",
            SenderNumber = "Altinn"
        });
    }

    // ── Helpers: ASB ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends <paramref name="command"/> to the main queue then immediately receives it
    /// with PeekLock and dead-letters it. Returns the <c>MessageId</c> that will appear on the DLQ.
    /// </summary>
    private async Task<string> SeedDlqMessageAsync(SendSmsCommand command)
    {
        string messageId = Guid.NewGuid().ToString();

        await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);

        await using var sender = client.CreateSender(_queueName);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString(JsonSerializer.Serialize(command)))
        {
            MessageId = messageId,
            ContentType = "application/json"
        });

        await using var receiver = client.CreateReceiver(_queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(15));
        await receiver.DeadLetterMessageAsync(received, "MaxDeliveryCountExceeded", "Seeded by test");

        return messageId;
    }

    private async Task<bool> WaitForDlqEmptyAsync(int maxAttempts = 20, int delayMs = 300)
    {
        await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
        await using var receiver = client.CreateReceiver(
            _queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        for (int i = 0; i < maxAttempts; i++)
        {
            if (await receiver.PeekMessageAsync() is null)
            {
                return true;
            }

            await Task.Delay(delayMs);
        }

        return false;
    }

    private async Task<bool> WaitForDlqMessageAsync(string messageId, int maxAttempts = 20, int delayMs = 300)
    {
        await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
        await using var receiver = client.CreateReceiver(
            _queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        for (int i = 0; i < maxAttempts; i++)
        {
            var messages = await receiver.PeekMessagesAsync(100);
            if (messages.Any(m => m.MessageId == messageId))
            {
                return true;
            }

            await Task.Delay(delayMs);
        }

        return false;
    }

    private async Task<bool> WaitForMainQueueMessageAsync(Guid notificationId, int maxAttempts = 20, int delayMs = 300)
    {
        await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
        await using var receiver = client.CreateReceiver(_queueName);

        for (int i = 0; i < maxAttempts; i++)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(500));
            if (message is null)
            {
                await Task.Delay(delayMs);
                continue;
            }

            try
            {
                var cmd = JsonSerializer.Deserialize<SendSmsCommand>(message.Body.ToString());
                if (cmd?.NotificationId == notificationId)
                {
                    await receiver.CompleteMessageAsync(message);
                    return true;
                }

                await receiver.AbandonMessageAsync(message);
            }
            catch
            {
                await receiver.AbandonMessageAsync(message);
            }
        }

        return false;
    }

    private async Task DrainQueueAsync(string queueOrDlqName)
    {
        try
        {
            await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
            await using var receiver = client.CreateReceiver(queueOrDlqName);

            while (true)
            {
                var msg = await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(500));
                if (msg is null)
                {
                    break;
                }

                await receiver.CompleteMessageAsync(msg);
            }
        }
        catch
        {
            // Best-effort drain — ignore errors during cleanup.
        }
    }

    // ── Helpers: service factory + list files ─────────────────────────────────
    private SmsSendQueueService CreateService(IOptions<SmsSendQueueSettings> queueSettings)
    {
        return new SmsSendQueueService(
            Options.Create(new AsbSettings
            {
                ConnectionString = _fixture.ServiceBusConnectionString,
                SmsSendQueueName = _queueName
            }),
            queueSettings,
            new SmsNotificationRepository(_fixture.DataSource),
            new ServiceBusClient(_fixture.ServiceBusConnectionString));
    }

    private static async Task RunMenuAsync(SmsSendQueueService service, string consoleInput)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetIn(new StringReader(consoleInput));
            Console.SetOut(TextWriter.Null);
            await service.RunMenuAsync();
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            await service.DisposeAsync();
        }
    }

    private (string ExpiredPath, string PendingPath, string OtherPath, IOptions<SmsSendQueueSettings> Settings)
        CreateTempListFiles()
    {
        string expiredPath = Path.Combine(Path.GetTempPath(), $"dlq-expired-{Guid.NewGuid()}.json");
        string pendingPath = Path.Combine(Path.GetTempPath(), $"dlq-pending-{Guid.NewGuid()}.json");
        string otherPath = Path.Combine(Path.GetTempPath(), $"dlq-other-{Guid.NewGuid()}.json");

        _tempFiles.Add(expiredPath);
        _tempFiles.Add(pendingPath);
        _tempFiles.Add(otherPath);

        return (expiredPath, pendingPath, otherPath, Options.Create(new SmsSendQueueSettings
        {
            SendingExpiredListFilePath = expiredPath,
            SendingPendingListFilePath = pendingPath,
            OtherStatusListFilePath = otherPath
        }));
    }

    private static DlqSmsItem BuildDlqSmsItem(Guid notificationId, SendSmsCommand command, string dlqMessageId) =>
        new DlqSmsItem
        {
            NotificationId = notificationId,
            MobileNumber = command.MobileNumber,
            Body = command.Body,
            SenderNumber = command.SenderNumber,
            DlqMessageId = dlqMessageId,
            DlqEnqueuedTime = DateTime.UtcNow,
            DlqDeadLetterReason = "MaxDeliveryCountExceeded",
            DlqDeadLetterErrorDescription = "Seeded by test"
        };

    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    private static async Task WriteListFileAsync(string filePath, List<DlqSmsItem> items)
    {
        string json = JsonSerializer.Serialize(items, _writeOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static async Task<List<DlqSmsItem>> ReadListFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        string json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<List<DlqSmsItem>>(json) ?? [];
    }
}
