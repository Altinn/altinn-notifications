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

using Moq;

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
        await DrainQueueAsync(_queueName, SubQueue.DeadLetter);
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

    [Fact]
    public async Task ProcessSendingPending_WhenNotificationNotInDatabase_PurgesDlqWithoutRequeue()
    {
        // Arrange — orphan notification (not in DB): GetNotificationStateAsync returns null,
        // which != "Sending", so the not-Sending branch fires and the ?? NotFound path is taken.
        var orphanId = Guid.NewGuid();
        var command = new SendSmsCommand
        {
            NotificationId = orphanId,
            MobileNumber = "+4712345678",
            Body = "Orphan pending test",
            SenderNumber = "Altinn"
        };
        string msgId = await SeedDlqMessageAsync(command);
        var (_, pendingPath, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(pendingPath, [BuildDlqSmsItem(orphanId, command, msgId)]);

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "3\n0\n", output);

        Assert.True(await WaitForDlqEmptyAsync(), "DLQ message should be purged when notification is not in DB");
        Assert.Contains("NOT FOUND", output.ToString());
    }

    [Fact]
    public async Task ProcessSendingPending_WhenDbResultIsNotSending_PurgesDlqWithoutRequeue()
    {
        // Arrange — notification is no longer 'Sending' (e.g. already Delivered) by the
        // time the pending list is processed. ResubmitPendingItemAsync must purge the DLQ
        // message without touching the DB or requeuing the command.
        var (notificationId, command) = await SeedNotificationAsync("Delivered", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (_, pendingPath, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(pendingPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "3\n0\n", output);

        Assert.True(await WaitForDlqEmptyAsync(), "DLQ message should be purged when DB result is not Sending");
        Assert.Contains("not Sending", output.ToString());

        var (result, _, _, _) = await new SmsNotificationRepository(_fixture.DataSource).GetNotificationStateAsync(notificationId);
        Assert.Equal("Delivered", result);
    }

    [Fact]
    public async Task ProcessSendingPending_WhenItemExpiredSinceInspection_UpdatesDbAndPurgesDlq()
    {
        // Arrange — notification was 'Sending' when Inspect ran, but has since expired
        // (expirytime is in the past). It ends up on the sending-pending list rather than
        // the expired list. ResubmitPendingItemAsync must detect isExpired=true and route
        // to the expired path: update DB to 'Accepted' and purge the DLQ message.
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (_, pendingPath, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(pendingPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "3\n0\n", output);

        Assert.True(await WaitForDlqEmptyAsync(), "DLQ message should be purged when item expired since inspection");
        Assert.Contains("expired since inspection", output.ToString());

        var (result, _, _, _) = await new SmsNotificationRepository(_fixture.DataSource).GetNotificationStateAsync(notificationId);
        Assert.Equal("Accepted", result);
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

    // ── Query DB state ────────────────────────────────────────────────────────
    [Fact]
    public async Task QueryDbState_WithListFile_PrintsCurrentDbState()
    {
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(1));
        string msgId = await SeedDlqMessageAsync(command);
        var (_, pendingPath, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(pendingPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "5\n2\n0\n", output);

        Assert.Contains(notificationId.ToString(), output.ToString());
    }

    [Fact]
    public async Task QueryDbState_WithInvalidSubChoice_PrintsInvalidAndReturns()
    {
        var (_, _, _, queueSettings) = CreateTempListFiles();

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "5\n9\n0\n", output);

        Assert.Contains("Invalid choice", output.ToString());
    }

    // ── Empty list file early-exit ────────────────────────────────────────────
    [Fact]
    public async Task ProcessSendingExpired_WhenListFileDoesNotExist_ReportsFileNotFound()
    {
        var (_, _, _, queueSettings) = CreateTempListFiles();

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "2\n0\n", output);

        Assert.Contains("File not found", output.ToString());
    }

    [Fact]
    public async Task ProcessSendingPending_WhenListFileDoesNotExist_ReportsFileNotFound()
    {
        var (_, _, _, queueSettings) = CreateTempListFiles();

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "3\n0\n", output);

        Assert.Contains("File not found", output.ToString());
    }

    [Fact]
    public async Task PurgeOtherStatus_WhenListFileDoesNotExist_ReportsFileNotFound()
    {
        var (_, _, _, queueSettings) = CreateTempListFiles();

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "4\n0\n", output);

        Assert.Contains("File not found", output.ToString());
    }

    // ── Non-target DLQ messages are abandoned ────────────────────────────────
    [Fact]
    public async Task ProcessSendingExpired_WhenDlqContainsExtraMessages_AbandonsNonTargetMessages()
    {
        // Arrange — two notifications on the DLQ, but only one is in the list file.
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));
        var (_, extraCommand) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));

        string targetMsgId = await SeedDlqMessageAsync(command);
        string extraMsgId = await SeedDlqMessageAsync(extraCommand);

        var (expiredPath, _, _, queueSettings) = CreateTempListFiles();

        // Only the first notification is in the list — the extra message should be abandoned.
        await WriteListFileAsync(expiredPath, [BuildDlqSmsItem(notificationId, command, targetMsgId)]);

        await RunMenuAsync(CreateService(queueSettings), "2\n0\n");

        // Target message was processed and purged.
        Assert.True(await WaitForDlqMessageAsync(extraMsgId), "The extra DLQ message should have been abandoned (still present)");
        var (result, _, _, _) = await new SmsNotificationRepository(_fixture.DataSource).GetNotificationStateAsync(notificationId);
        Assert.Equal("Accepted", result);
    }

    // ── [WARN] block: list items not present in DLQ snapshot ─────────────────
    [Fact]
    public async Task ProcessSendingExpired_WhenSomeListItemsAbsentFromDlq_PrintsWarnForMissingOnes()
    {
        // Arrange — one real DLQ message + one fake message ID (not on DLQ).
        // PeekDlqMatchCountAsync finds 1 match → does not early-exit.
        // After receive, the fake ID is absent from the snapshot → [WARN] fires.
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));
        string realMsgId = await SeedDlqMessageAsync(command);
        string fakeMsgId = Guid.NewGuid().ToString();

        var (expiredPath, _, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(expiredPath, [
            BuildDlqSmsItem(notificationId, command, realMsgId),
            BuildDlqSmsItem(Guid.NewGuid(), command, fakeMsgId)
        ]);

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "2\n0\n", output);

        Assert.Contains("[WARN]", output.ToString());
        Assert.Contains("not found in the DLQ snapshot", output.ToString());
    }

    // ── SmsSendQueueService sub-menu: invalid input + stream end ─────────────
    [Fact]
    public async Task SmsSendQueueMenu_WhenInputStreamEnds_ExitsGracefully()
    {
        var (_, _, _, queueSettings) = CreateTempListFiles();

        var output = new StringWriter();

        // Empty stream → ReadLine returns null → service exits back to caller
        await RunMenuAsync(CreateService(queueSettings), string.Empty, output);

        // Menu header is printed before the null ReadLine causes exit
        Assert.Contains("SMS Send Queue", output.ToString());
    }

    [Fact]
    public async Task SmsSendQueueMenu_InvalidInput_PrintsErrorAndContinues()
    {
        var (_, _, _, queueSettings) = CreateTempListFiles();

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "xyz\n0\n", output);

        Assert.Contains("Invalid choice", output.ToString());
    }

    // ── Inspect DLQ: notification not in DB ──────────────────────────────────
    [Fact]
    public async Task InspectDlq_WhenNotificationNotInDatabase_ClassifiesAsOther()
    {
        // Arrange — put a message on the DLQ for a notification that does NOT exist in DB.
        var orphanId = Guid.NewGuid();
        var command = new SendSmsCommand
        {
            NotificationId = orphanId,
            MobileNumber = "+4712345678",
            Body = "Orphan message",
            SenderNumber = "Altinn"
        };
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, pendingPath, otherPath, queueSettings) = CreateTempListFiles();

        await RunMenuAsync(CreateService(queueSettings), "1\n0\n");

        // No DB row → result is null → classified as "other"
        var otherItems = await ReadListFileAsync(otherPath);
        Assert.Contains(otherItems, i => i.NotificationId == orphanId && i.DlqMessageId == msgId);
        Assert.Empty(await ReadListFileAsync(expiredPath));
        Assert.Empty(await ReadListFileAsync(pendingPath));
    }

    // ── Inspect DLQ: malformed message body ──────────────────────────────────
    [Fact]
    public async Task InspectDlq_WhenMessageBodyIsNotValidJson_SkipsMessageWithWarning()
    {
        // Arrange — put a message with a non-JSON body on the DLQ.
        await SeedRawDlqMessageAsync("not-valid-json");
        var (expiredPath, pendingPath, otherPath, queueSettings) = CreateTempListFiles();

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "1\n0\n", output);

        // Message is skipped — warn is printed and nothing appears in any list file.
        Assert.Contains("[WARN] Could not deserialize message", output.ToString());
        Assert.Empty(await ReadListFileAsync(expiredPath));
        Assert.Empty(await ReadListFileAsync(pendingPath));
        Assert.Empty(await ReadListFileAsync(otherPath));
    }

    // ── ProcessSendingExpired: notification not in DB ─────────────────────────
    [Fact]
    public async Task ProcessSendingExpired_WhenNotificationNotInDatabase_PurgesDlqMessageWithoutDbUpdate()
    {
        // Arrange — a DLQ message for a notification that no longer exists in DB (currentResult is null).
        var orphanId = Guid.NewGuid();
        var command = new SendSmsCommand
        {
            NotificationId = orphanId,
            MobileNumber = "+4712345678",
            Body = "Orphan message",
            SenderNumber = "Altinn"
        };
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, _, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(expiredPath, [BuildDlqSmsItem(orphanId, command, msgId)]);

        await RunMenuAsync(CreateService(queueSettings), "2\n0\n");

        // DLQ message was completed (purged) even though no DB row existed.
        Assert.True(await WaitForDlqEmptyAsync(), "DLQ message should be purged even when notification is not in DB");
    }

    // ── ProcessSendingExpired: item in list not found in DLQ ─────────────────
    [Fact]
    public async Task ProcessSendingExpired_WhenListItemNotPresentInDlq_ReportsNotFoundCount()
    {
        // Arrange — list file references a DLQ message ID that doesn't exist on the DLQ.
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));
        var (expiredPath, _, _, queueSettings) = CreateTempListFiles();

        // Use a fake message ID — no corresponding message on the DLQ.
        string fakeMsgId = Guid.NewGuid().ToString();
        await WriteListFileAsync(expiredPath, [BuildDlqSmsItem(notificationId, command, fakeMsgId)]);

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "2\n0\n", output);

        // PeekDlqMatchCountAsync finds 0 matches → early exit before receive.
        Assert.Contains("0 of 1 item(s) from list found on DLQ", output.ToString());

        // DB state is unchanged — no message was received to process.
        var (result, _, _, _) = await new SmsNotificationRepository(_fixture.DataSource).GetNotificationStateAsync(notificationId);
        Assert.Equal("Sending", result);
    }

    // ── QueryDbState: sub-choices 1 and 3 ────────────────────────────────────
    [Fact]
    public async Task QueryDbState_SubChoiceOne_PrintsCurrentDbState()
    {
        var (notificationId, command) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, _, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(expiredPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "5\n1\n0\n", output);

        Assert.Contains(notificationId.ToString(), output.ToString());
    }

    [Fact]
    public async Task QueryDbState_SubChoiceThree_PrintsCurrentDbState()
    {
        var (notificationId, command) = await SeedNotificationAsync("Accepted", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (_, _, otherPath, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(otherPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "5\n3\n0\n", output);

        Assert.Contains(notificationId.ToString(), output.ToString());
    }

    [Fact]
    public async Task QueryDbState_WhenListFileDoesNotExist_ReportsFileNotFound()
    {
        var (_, _, _, queueSettings) = CreateTempListFiles();

        var output = new StringWriter();

        // Pending file was never written — file doesn't exist → items.Count == 0 early return
        await RunMenuAsync(CreateService(queueSettings), "5\n2\n0\n", output);

        Assert.Contains("File not found", output.ToString());
    }

    // ── Null ReadLine in PurgeOtherStatus confirmation prompt (line 295) ────────
    [Fact]
    public async Task PurgeOtherStatus_WhenConfirmationStreamEnds_Aborts()
    {
        // Arrange — list file must be non-empty so the method reaches the confirmation prompt.
        var (notificationId, command) = await SeedNotificationAsync("Delivered", DateTime.UtcNow.AddHours(-1));
        string msgId = await SeedDlqMessageAsync(command);
        var (_, _, otherPath, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(otherPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        var output = new StringWriter();

        // Input "4\n" only — stream ends before the "Proceed? (y/N): " confirmation.
        // Console.ReadLine() returns null → null branch of ?. is covered → method aborts.
        await RunMenuAsync(CreateService(queueSettings), "4\n", output);

        Assert.Contains("Aborted", output.ToString());

        // DLQ message was NOT purged (operator never confirmed).
        Assert.True(await WaitForDlqMessageAsync(msgId), "DLQ message should remain after aborted purge");
    }

    // ── Null ReadLine in QueryDbState sub-choice prompt (line 324) ────────────
    [Fact]
    public async Task QueryDbState_WhenSubChoiceStreamEnds_PrintsInvalidAndReturns()
    {
        var (_, _, _, queueSettings) = CreateTempListFiles();

        var output = new StringWriter();

        // Input "5\n" only — stream ends before sub-choice is read.
        // Console.ReadLine() returns null → null → matches _ arm → "Invalid choice."
        await RunMenuAsync(CreateService(queueSettings), "5\n", output);

        Assert.Contains("Invalid choice", output.ToString());
    }

    // ── QueryDbState: notification not in DB (line 358 null paths) ────────────
    [Fact]
    public async Task QueryDbState_WhenNotificationNotInDatabase_PrintsNotFound()
    {
        // Arrange — list file with an orphan notification that has no DB row.
        var orphanId = Guid.NewGuid();
        var command = new SendSmsCommand
        {
            NotificationId = orphanId,
            MobileNumber = "+4712345678",
            Body = "Orphan",
            SenderNumber = "Altinn"
        };
        var (_, pendingPath, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(pendingPath, [BuildDlqSmsItem(orphanId, command, Guid.NewGuid().ToString())]);

        var output = new StringWriter();
        await RunMenuAsync(CreateService(queueSettings), "5\n2\n0\n", output);

        // result ?? NotFound → "NOT FOUND"; FormatUtc(null) → "N/A"
        Assert.Contains("NOT FOUND", output.ToString());
    }

    // ── ProcessDlqItems exception catch (lines 418-422) ──────────────────────
    [Fact]
    public async Task ProcessSendingExpired_WhenRepositoryThrows_HitsExceptionCatchAndAbandonsDlqMessage()
    {
        // Arrange — seed a real DLQ message; mock the repository to throw.
        var notificationId = Guid.NewGuid();
        var command = new SendSmsCommand
        {
            NotificationId = notificationId,
            MobileNumber = "+4712345678",
            Body = "Throw test",
            SenderNumber = "Altinn"
        };
        string msgId = await SeedDlqMessageAsync(command);
        var (expiredPath, _, _, queueSettings) = CreateTempListFiles();
        await WriteListFileAsync(expiredPath, [BuildDlqSmsItem(notificationId, command, msgId)]);

        var mockRepo = new Mock<ISmsNotificationRepository>();
        mockRepo.Setup(r => r.GetNotificationStateAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Simulated DB failure"));

        var service = new SmsSendQueueService(
            Options.Create(new AsbSettings
            {
                ConnectionString = _fixture.ServiceBusConnectionString,
                SmsSendQueueName = _queueName
            }),
            queueSettings,
            mockRepo.Object,
            new ServiceBusClient(_fixture.ServiceBusConnectionString));

        var output = new StringWriter();
        await RunMenuAsync(service, "2\n0\n", output);

        Assert.Contains("Unexpected error", output.ToString());
        Assert.True(await WaitForDlqMessageAsync(msgId), "DLQ message should be abandoned after exception");
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
    private Task<string> SeedDlqMessageAsync(SendSmsCommand command) =>
        SeedRawDlqMessageAsync(JsonSerializer.Serialize(command));

    private async Task<string> SeedRawDlqMessageAsync(string rawBody)
    {
        string messageId = Guid.NewGuid().ToString();

        await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);

        await using var sender = client.CreateSender(_queueName);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString(rawBody))
        {
            MessageId = messageId,
            ContentType = "application/json"
        });

        await using var receiver = client.CreateReceiver(_queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

        ServiceBusReceivedMessage? received = null;
        for (int attempt = 0; attempt < 30 && received is null; attempt++)
        {
            var msg = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
            if (msg is null)
            {
                await Task.Delay(200);
                continue;
            }

            if (msg.MessageId == messageId)
            {
                received = msg;
            }
            else
            {
                await receiver.AbandonMessageAsync(msg);
            }
        }

        if (received is null)
        {
            throw new InvalidOperationException($"Message {messageId} was not received within the timeout.");
        }

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

    private async Task DrainQueueAsync(string queueName, SubQueue subQueue = SubQueue.None)
    {
        try
        {
            await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
            await using var receiver = client.CreateReceiver(
                queueName,
                new ServiceBusReceiverOptions { SubQueue = subQueue });

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

    private static async Task RunMenuAsync(SmsSendQueueService service, string consoleInput, TextWriter? output = null)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetIn(new StringReader(consoleInput));
            Console.SetOut(output ?? TextWriter.Null);
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
        new()
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
