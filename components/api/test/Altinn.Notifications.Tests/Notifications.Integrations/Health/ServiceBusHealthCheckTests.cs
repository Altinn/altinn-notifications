using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Health;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Health;

public class ServiceBusHealthCheckTests
{
    private static readonly WolverineSettings _settings = new()
    {
        EmailSendResultQueueName = "listen-1",
        SmsSendResultQueueName = "listen-2",
        EmailDeliveryReportQueueName = "listen-3",
        SmsDeliveryReportQueueName = "listen-4",
        EmailServiceRateLimitQueueName = "listen-5",
        EmailSendQueueName = "send-1",
        SendSmsQueueName = "send-2",
        ComposedEmailSendQueueName = "send-3"
    };

    [Fact]
    public async Task CheckHealthAsync_AllQueuesReachable_ReturnsHealthy()
    {
        var client = CreateClient(failingListen: [], failingSend: []);

        var result = await Check(client);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ListenQueueFails_ReturnsUnhealthyNamingQueueAndDirection()
    {
        var client = CreateClient(failingListen: ["listen-2"], failingSend: []);

        var result = await Check(client);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("listen-2 (listen)", result.Description);
        Assert.DoesNotContain("listen-1", result.Description);
        Assert.DoesNotContain("(send)", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_SendQueueFails_ReturnsUnhealthyNamingQueueAndDirection()
    {
        var client = CreateClient(failingListen: [], failingSend: ["send-3"]);

        var result = await Check(client);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("send-3 (send)", result.Description);
        Assert.DoesNotContain("(listen)", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_ListenAndSendFail_ReportsBoth()
    {
        var client = CreateClient(failingListen: ["listen-1"], failingSend: ["send-1"]);

        var result = await Check(client);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("listen-1 (listen)", result.Description);
        Assert.Contains("send-1 (send)", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_Cancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var receiver = new Mock<ServiceBusReceiver>();
        receiver
            .Setup(r => r.PeekMessageAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .Returns<long?, CancellationToken>((_, token) =>
            {
                cts.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult<ServiceBusReceivedMessage>(null!);
            });

        var client = new Mock<ServiceBusClient>();
        client.Setup(c => c.CreateReceiver(It.IsAny<string>())).Returns(receiver.Object);
        client.Setup(c => c.CreateSender(It.IsAny<string>())).Returns(CreateSender(fails: false));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CheckWithToken(client, cts.Token));
    }

    [Fact]
    public async Task DisposeAsync_DisposesClient()
    {
        var client = new Mock<ServiceBusClient>();

        await new ServiceBusHealthCheck(client.Object, _settings).DisposeAsync();

        client.Verify(c => c.DisposeAsync(), Times.Once);
    }

    private static Task<HealthCheckResult> Check(Mock<ServiceBusClient> client)
    {
        return CheckWithToken(client, TestContext.Current.CancellationToken);
    }

    private static Task<HealthCheckResult> CheckWithToken(Mock<ServiceBusClient> client, CancellationToken cancellationToken)
    {
        return new ServiceBusHealthCheck(client.Object, _settings).CheckHealthAsync(new HealthCheckContext(), cancellationToken);
    }

    private static ServiceBusSender CreateSender(bool fails)
    {
        var sender = new Mock<ServiceBusSender>();
        var setup = sender.Setup(s => s.CreateMessageBatchAsync(It.IsAny<CancellationToken>()));
        if (fails)
        {
            setup.ThrowsAsync(new ServiceBusException("fail", ServiceBusFailureReason.ServiceCommunicationProblem));
        }
        else
        {
            setup.ReturnsAsync(ServiceBusModelFactory.ServiceBusMessageBatch(1024, []));
        }

        return sender.Object;
    }

    private static Mock<ServiceBusClient> CreateClient(string[] failingListen, string[] failingSend)
    {
        var client = new Mock<ServiceBusClient>();

        client.Setup(c => c.CreateReceiver(It.IsAny<string>())).Returns((string queue) =>
        {
            var receiver = new Mock<ServiceBusReceiver>();
            var setup = receiver.Setup(r => r.PeekMessageAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()));
            if (failingListen.Contains(queue))
            {
                setup.ThrowsAsync(new ServiceBusException("fail", ServiceBusFailureReason.MessagingEntityNotFound));
            }
            else
            {
                setup.ReturnsAsync((ServiceBusReceivedMessage)null!);
            }

            return receiver.Object;
        });

        client.Setup(c => c.CreateSender(It.IsAny<string>())).Returns((string queue) => CreateSender(failingSend.Contains(queue)));

        return client;
    }
}
