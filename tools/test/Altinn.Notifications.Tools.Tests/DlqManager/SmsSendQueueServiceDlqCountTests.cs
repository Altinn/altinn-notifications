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

        // Peek call sequence:
        //   1. Menu DLQ count display  → empty (count = 0)
        //   2. PeekDlqMatchCountAsync  → returns fakeMsg (1 match found)
        //   3. PeekDlqMatchCountAsync loop end → empty
        mockReceiver
            .SetupSequence(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), default))
            .ReturnsAsync(new List<ServiceBusReceivedMessage>())
            .ReturnsAsync(new List<ServiceBusReceivedMessage> { fakeMsg })
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

            var mockRepo = new Mock<ISmsNotificationRepository>();
            mockRepo
                .Setup(r => r.GetNotificationStateAsync(notificationId))
                .ReturnsAsync(("Sending", DateTime.UtcNow.AddHours(1), false, (DateTime?)null));

            var service = new SmsSendQueueService(
                Options.Create(new AsbSettings { ConnectionString = string.Empty, SmsSendQueueName = "test.queue" }),
                Options.Create(new SmsSendQueueSettings { SendingPendingListFilePath = pendingPath }),
                mockRepo.Object,
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
    public async Task ProcessSendingExpired_WhenPeekDlqMatchCountThrows_CatchesAndProceeds()
    {
        // Arrange — peek throws during PeekDlqMatchCountAsync → catch returns -1.
        // -1 != 0 so the early-exit is skipped; processing continues to ReceiveMessagesAsync.
        var dlqMsgId = Guid.NewGuid().ToString();
        var notificationId = Guid.NewGuid();
        var command = new SendSmsCommand
        {
            NotificationId = notificationId,
            MobileNumber = "+4712345678",
            Body = "Peek-fail test",
            SenderNumber = "Altinn"
        };

        var mockClient = new Mock<ServiceBusClient>();
        var mockReceiver = new Mock<ServiceBusReceiver>();

        mockClient
            .Setup(c => c.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
            .Returns(mockReceiver.Object);

        // Peek call sequence:
        //   1. Menu DLQ count display         → empty (count = 0)
        //   2. PeekDlqMatchCountAsync page 1   → throws → catch returns -1
        mockReceiver
            .SetupSequence(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), default))
            .ReturnsAsync(new List<ServiceBusReceivedMessage>())
            .ThrowsAsync(new ServiceBusException("Simulated peek failure", ServiceBusFailureReason.ServiceBusy));

        // Processing continues with an empty snapshot — nothing to receive.
        mockReceiver
            .Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), default))
            .ReturnsAsync(new List<ServiceBusReceivedMessage>());

        mockReceiver.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockClient.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        string expiredPath = Path.Combine(Path.GetTempPath(), $"dlq-expired-{Guid.NewGuid()}.json");
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
            await File.WriteAllTextAsync(expiredPath, JsonSerializer.Serialize(new List<DlqSmsItem> { item }));

            var service = new SmsSendQueueService(
                Options.Create(new AsbSettings { ConnectionString = string.Empty, SmsSendQueueName = "test.queue" }),
                Options.Create(new SmsSendQueueSettings { SendingExpiredListFilePath = expiredPath }),
                new Mock<ISmsNotificationRepository>().Object,
                mockClient.Object);

            var output = new StringWriter();
            var originalIn = Console.In;
            var originalOut = Console.Out;
            try
            {
                Console.SetIn(new StringReader("2\n0\n"));
                Console.SetOut(output);
                await service.RunMenuAsync();
            }
            finally
            {
                Console.SetIn(originalIn);
                Console.SetOut(originalOut);
                await service.DisposeAsync();
            }

            // Peek threw → catch returned -1 → processing continued to receive.
            Assert.Contains("Done.", output.ToString());
        }
        finally
        {
            if (File.Exists(expiredPath))
            {
                File.Delete(expiredPath);
            }
        }
    }

    [Fact]
    public async Task ProcessSendingPending_WhenItemExpiredButDbUpdateReturnsZeroRows_AbandonsDlqMessage()
    {
        // Arrange — DB re-check returns (Sending, isExpired=true) but UpdateResultToAcceptedAsync
        // returns 0 rows (race: another process already updated the row between the two calls).
        // ResubmitPendingItemAsync must abandon the DLQ message and report the 0-rows failure.
        var notificationId = Guid.NewGuid();
        var dlqMsgId = Guid.NewGuid().ToString();
        var command = new SendSmsCommand
        {
            NotificationId = notificationId,
            MobileNumber = "+4712345678",
            Body = "Expired zero-rows test",
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

        // Peek sequence:
        //   1. Menu DLQ count display     → empty (count = 0)
        //   2. PeekDlqMatchCountAsync p1  → fakeMsg found
        //   3. PeekDlqMatchCountAsync p2  → empty (loop ends)
        mockReceiver
            .SetupSequence(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), default))
            .ReturnsAsync(new List<ServiceBusReceivedMessage>())
            .ReturnsAsync(new List<ServiceBusReceivedMessage> { fakeMsg })
            .ReturnsAsync(new List<ServiceBusReceivedMessage>());

        mockReceiver
            .Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), default))
            .ReturnsAsync(new List<ServiceBusReceivedMessage> { fakeMsg });

        // DB: Sending + expired, but UPDATE returns 0 rows (concurrent modification).
        var mockRepo = new Mock<ISmsNotificationRepository>();
        mockRepo
            .Setup(r => r.GetNotificationStateAsync(notificationId))
            .ReturnsAsync(("Sending", DateTime.UtcNow.AddHours(-1), true, (DateTime?)null));
        mockRepo
            .Setup(r => r.UpdateResultToAcceptedAsync(notificationId))
            .ReturnsAsync(0);

        mockReceiver.Setup(r => r.AbandonMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, default))
            .Returns(Task.CompletedTask);
        mockReceiver.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockSender.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockClient.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

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
            await File.WriteAllTextAsync(pendingPath, JsonSerializer.Serialize(new List<DlqSmsItem> { item }));

            var service = new SmsSendQueueService(
                Options.Create(new AsbSettings { ConnectionString = string.Empty, SmsSendQueueName = "test.queue" }),
                Options.Create(new SmsSendQueueSettings { SendingPendingListFilePath = pendingPath }),
                mockRepo.Object,
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

            // isExpired=true but rows=0 → abandon (not complete); output reports 0-rows failure.
            Assert.Contains("0 rows", output.ToString());
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
    public async Task ProcessOneMessage_WhenProcessItemThrowsAndAbandonAlsoThrows_SwallowsInnerException()
    {
        // Arrange — GetNotificationStateAsync throws, so processItem propagates the exception
        // to ProcessOneMessageAsync's outer catch (line 421). The best-effort inner AbandonMessageAsync
        // (line 424) also throws, but the inner catch swallows it. The method must return false
        // without propagating either exception, so RunMenuAsync prints "Done." normally.
        var notificationId = Guid.NewGuid();
        var dlqMsgId = Guid.NewGuid().ToString();
        var command = new SendSmsCommand
        {
            NotificationId = notificationId,
            MobileNumber = "+4712345678",
            Body = "Double-throw test",
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

        // Peek sequence:
        //   1. Menu DLQ count display     → empty (count = 0)
        //   2. PeekDlqMatchCountAsync p1  → fakeMsg found
        //   3. PeekDlqMatchCountAsync p2  → empty (loop ends)
        mockReceiver
            .SetupSequence(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), default))
            .ReturnsAsync(new List<ServiceBusReceivedMessage>())
            .ReturnsAsync(new List<ServiceBusReceivedMessage> { fakeMsg })
            .ReturnsAsync(new List<ServiceBusReceivedMessage>());

        mockReceiver
            .Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), default))
            .ReturnsAsync(new List<ServiceBusReceivedMessage> { fakeMsg });

        // processItem throws via GetNotificationStateAsync → outer catch fires.
        var mockRepo = new Mock<ISmsNotificationRepository>();
        mockRepo
            .Setup(r => r.GetNotificationStateAsync(notificationId))
            .ThrowsAsync(new InvalidOperationException("Simulated repository failure"));

        // Best-effort AbandonMessageAsync inside the outer catch also throws → inner catch fires (line 424).
        mockReceiver
            .Setup(r => r.AbandonMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, default))
            .ThrowsAsync(new ServiceBusException("Simulated abandon failure", ServiceBusFailureReason.ServiceBusy));

        mockReceiver.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockSender.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockClient.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

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
            await File.WriteAllTextAsync(pendingPath, JsonSerializer.Serialize(new List<DlqSmsItem> { item }));

            var service = new SmsSendQueueService(
                Options.Create(new AsbSettings { ConnectionString = string.Empty, SmsSendQueueName = "test.queue" }),
                Options.Create(new SmsSendQueueSettings { SendingPendingListFilePath = pendingPath }),
                mockRepo.Object,
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

            // Outer catch logged the processItem error; inner catch swallowed the abandon error.
            // RunMenuAsync continues and prints "Done." — neither exception propagated.
            Assert.Contains("Unexpected error", output.ToString());
            Assert.Contains("Done.", output.ToString());
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
