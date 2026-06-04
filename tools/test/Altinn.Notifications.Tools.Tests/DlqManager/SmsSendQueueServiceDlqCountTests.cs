using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.DlqManager.Configuration;
using Altinn.Notifications.Tools.DlqManager.Models;
using Altinn.Notifications.Tools.DlqManager.Repositories;
using Altinn.Notifications.Tools.DlqManager.Services.Queues;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.DlqManager;

/// <summary>
/// Unit tests for the DLQ-count display in <see cref="SmsSendQueueService.RunMenuAsync"/>.
/// Uses a mocked <see cref="ServiceBusClient"/> to force <c>PeekCountDlqAsync</c> to
/// throw, which exercises:
/// <list type="bullet">
///   <item>The <c>catch { return -1; }</c> block in <c>PeekCountDlqAsync</c> (lines 478-481).</item>
///   <item>The <c>"N/A"</c> branch of the ternary on line 51.</item>
/// </list>
/// These do not require running containers.
/// </summary>
public class SmsSendQueueServiceDlqCountTests
{
    [Fact]
    public async Task RunMenuAsync_WhenDlqCountThrows_ShowsNotAvailableOnMenu()
    {
        // Arrange — mock ServiceBusClient whose receiver always throws on PeekMessagesAsync.
        var mockClient = new Mock<ServiceBusClient>();
        var mockReceiver = new Mock<ServiceBusReceiver>();

        mockClient
            .Setup(c => c.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
            .Returns(mockReceiver.Object);

        mockReceiver
            .Setup(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), default))
            .ThrowsAsync(new ServiceBusException("Simulated peek failure", ServiceBusFailureReason.ServiceBusy));

        mockReceiver.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockClient.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var service = new SmsSendQueueService(
            Options.Create(new AsbSettings { ConnectionString = string.Empty, SmsSendQueueName = "test.queue" }),
            Options.Create(new SmsSendQueueSettings()),
            new Mock<ISmsNotificationRepository>().Object,
            mockClient.Object);

        var output = new StringWriter();
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetIn(new StringReader("0\n"));
            Console.SetOut(output);

            await service.RunMenuAsync();
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            await service.DisposeAsync();
        }

        // PeekCountDlqAsync threw → catch returned -1 → menu shows "N/A"
        Assert.Contains("N/A", output.ToString());
    }

    [Fact]
    public async Task RunMenuAsync_WhenDlqHasMessages_ShowsCountOnMenu()
    {
        // Exercises the normal exit path of PeekCountDlqAsync (line 478):
        //   first peek returns messages → count increments → second peek returns empty → return count.
        var fakeMsg = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: Guid.NewGuid().ToString(),
            sequenceNumber: 1);

        var mockClient = new Mock<ServiceBusClient>();
        var mockReceiver = new Mock<ServiceBusReceiver>();

        mockClient
            .Setup(c => c.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
            .Returns(mockReceiver.Object);

        // First peek (fromSeq=0): one message.  Second peek (fromSeq=2): empty → breaks loop.
        mockReceiver
            .SetupSequence(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), default))
            .ReturnsAsync([fakeMsg])
            .ReturnsAsync([]);

        mockReceiver.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockClient.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var service = new SmsSendQueueService(
            Options.Create(new AsbSettings { ConnectionString = string.Empty, SmsSendQueueName = "test.queue" }),
            Options.Create(new SmsSendQueueSettings()),
            new Mock<ISmsNotificationRepository>().Object,
            mockClient.Object);

        var output = new StringWriter();
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetIn(new StringReader("0\n"));
            Console.SetOut(output);

            await service.RunMenuAsync();
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            await service.DisposeAsync();
        }

        Assert.Contains("DLQ count: 1", output.ToString());
    }

    [Fact]
    public async Task ProcessSendingPending_WhenSendMessageThrows_AbandonsDlqMessageAndReportsFailure()
    {
        // Arrange — build a fake DLQ message matching the list file entry.
        var notificationId = Guid.NewGuid();
        var dlqMsgId = Guid.NewGuid().ToString();
        var command = new SendSmsCommand
        {
            NotificationId = notificationId,
            MobileNumber = "+4712345678",
            Body = "Send-fail test",
            SenderNumber = "Altinn"
        };

        var fakeMsg = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(JsonSerializer.Serialize(command)),
            messageId: dlqMsgId,
            sequenceNumber: 1);

        var mockClient = new Mock<ServiceBusClient>();
        var mockReceiver = new Mock<ServiceBusReceiver>();
        var mockSender = new Mock<ServiceBusSender>();

        mockClient
            .Setup(c => c.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
            .Returns(mockReceiver.Object);
        mockClient
            .Setup(c => c.CreateSender(It.IsAny<string>()))
            .Returns(mockSender.Object);

        // Count peek returns empty (DLQ count = 0 for the menu display).
        mockReceiver
            .Setup(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), default))
            .ReturnsAsync(new List<ServiceBusReceivedMessage>());

        // ProcessDlqItemsAsync receives the fake message.
        mockReceiver
            .Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), default))
            .ReturnsAsync(new List<ServiceBusReceivedMessage> { fakeMsg });

        // Sender throws → triggers lines 271-272.
        mockSender
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
            .ThrowsAsync(new ServiceBusException("Simulated send failure", ServiceBusFailureReason.ServiceBusy));

        mockReceiver.Setup(r => r.AbandonMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, default))
            .Returns(Task.CompletedTask);
        mockReceiver.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockSender.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockClient.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        // Write the pending list file with a matching item.
        string pendingPath = Path.Combine(Path.GetTempPath(), $"dlq-pending-{Guid.NewGuid()}.json");
        try
        {
            var item = new DlqSmsItem
            {
                NotificationId = notificationId,
                MobileNumber = command.MobileNumber,
                Body = command.Body,
                SenderNumber = command.SenderNumber,
                DlqMessageId = dlqMsgId,
                DlqEnqueuedTime = DateTime.UtcNow
            };
            string json = JsonSerializer.Serialize(new List<DlqSmsItem> { item });
            await File.WriteAllTextAsync(pendingPath, json);

            var service = new SmsSendQueueService(
                Options.Create(new AsbSettings { ConnectionString = string.Empty, SmsSendQueueName = "test.queue" }),
                Options.Create(new SmsSendQueueSettings { SendingPendingListFilePath = pendingPath }),
                new Mock<ISmsNotificationRepository>().Object,
                mockClient.Object);

            var output = new StringWriter();
            var originalIn = Console.In;
            var originalOut = Console.Out;
            try
            {
                Console.SetIn(new StringReader("3\n0\n"));
                Console.SetOut(output);

                await service.RunMenuAsync();
            }
            finally
            {
                Console.SetIn(originalIn);
                Console.SetOut(originalOut);
                await service.DisposeAsync();
            }

            // Catch block fired → "Send failed" in output; message was abandoned.
            Assert.Contains("Send failed", output.ToString());
            mockReceiver.Verify(r => r.AbandonMessageAsync(fakeMsg, null, default), Times.Once);
        }
        finally
        {
            if (File.Exists(pendingPath))
            {
                File.Delete(pendingPath);
            }
        }
    }

    [Fact]
    public async Task ReadListFile_WhenFileContainsJsonNull_ThrowsInvalidOperation()
    {
        // "null" is valid JSON but deserialises to null for a List type,
        // hitting the ?? throw on line 531.
        string expiredPath = Path.Combine(Path.GetTempPath(), $"dlq-expired-{Guid.NewGuid()}.json");
        try
        {
            await File.WriteAllTextAsync(expiredPath, "null");

            var mockClient = new Mock<ServiceBusClient>();
            var mockReceiver = new Mock<ServiceBusReceiver>();

            mockClient
                .Setup(c => c.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
                .Returns(mockReceiver.Object);
            mockReceiver
                .Setup(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), default))
                .ReturnsAsync([]);
            mockReceiver.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);
            mockClient.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

            var service = new SmsSendQueueService(
                Options.Create(new AsbSettings { ConnectionString = string.Empty, SmsSendQueueName = "test.queue" }),
                Options.Create(new SmsSendQueueSettings { SendingExpiredListFilePath = expiredPath }),
                new Mock<ISmsNotificationRepository>().Object,
                mockClient.Object);

            var originalIn = Console.In;
            var originalOut = Console.Out;
            try
            {
                Console.SetIn(new StringReader("2\n"));
                Console.SetOut(TextWriter.Null);

                await Assert.ThrowsAsync<InvalidOperationException>(service.RunMenuAsync);
            }
            finally
            {
                Console.SetIn(originalIn);
                Console.SetOut(originalOut);
                await service.DisposeAsync();
            }
        }
        finally
        {
            if (File.Exists(expiredPath))
            {
                File.Delete(expiredPath);
            }
        }
    }
}
