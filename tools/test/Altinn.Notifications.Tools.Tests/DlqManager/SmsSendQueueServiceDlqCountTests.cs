using System;
using System.IO;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.DlqManager.Configuration;
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
}
