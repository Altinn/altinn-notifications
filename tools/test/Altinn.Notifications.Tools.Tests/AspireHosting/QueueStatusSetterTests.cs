using Altinn.Notifications.Aspire.Hosting;

using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.AspireHosting;

/// <summary>
/// The dashboard's Set queue status command, against a mocked administration client.
/// </summary>
public class QueueStatusSetterTests
{
    private const string Queue = "altinn.notifications.sms.send";

    private readonly Mock<ServiceBusAdministrationClient> _admin = new();

    [Fact]
    public async Task Set_UpdatesTheQueue_AndReportsTheStatusBefore()
    {
        var properties = QueueWithStatus(EntityStatus.Active);
        _admin.Setup(a => a.GetQueueAsync(Queue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(properties, Mock.Of<Response>()));

        var result = await new QueueStatusSetter(_admin.Object).SetAsync(Queue, "ReceiveDisabled", TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Message);
        Assert.Equal($"{Queue}: Active → ReceiveDisabled", result.Message);
        _admin.Verify(a => a.UpdateQueueAsync(It.Is<QueueProperties>(p => p.Status == EntityStatus.ReceiveDisabled), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Paused")]
    [InlineData(null)]
    public async Task Set_UnknownStatus_FailsWithoutCallingTheEmulator(string? status)
    {
        var result = await new QueueStatusSetter(_admin.Object).SetAsync(Queue, status, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("Active, ReceiveDisabled, SendDisabled, Disabled", result.Message);
        _admin.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Set_QueueTheEmulatorDoesNotHave_Fails()
    {
        _admin.Setup(a => a.GetQueueAsync("unknown", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceBusException("The messaging entity could not be found.", ServiceBusFailureReason.MessagingEntityNotFound));

        var result = await new QueueStatusSetter(_admin.Object).SetAsync("unknown", "Disabled", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.StartsWith("Could not set the status of unknown: The messaging entity could not be found.", result.Message);
    }

    [Fact]
    public async Task Set_EmulatorAnswersWithAnError_Fails()
    {
        _admin.Setup(a => a.GetQueueAsync(Queue, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "Service unavailable"));

        var result = await new QueueStatusSetter(_admin.Object).SetAsync(Queue, "Active", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal($"Could not set the status of {Queue}: Service unavailable", result.Message);
    }

    [Fact]
    public void ReadQueues_ListsTheQueuesInTheEmulatorsConfig()
    {
        string config = """
            { "UserConfig": { "Namespaces": [ { "Name": "sbemulatorns", "Queues": [ { "Name": "first" }, { "Name": "second" } ] } ] } }
            """;

        Assert.Equal(["first", "second"], QueueStatusSetter.ReadQueues(config));
    }

    [Fact]
    public void ForEmulator_TalksToTheEmulatorOnItsHttpPort()
    {
        Assert.NotNull(QueueStatusSetter.ForEmulator(5300));
    }

    private static QueueProperties QueueWithStatus(EntityStatus status) =>
        ServiceBusModelFactory.QueueProperties(
            name: Queue,
            lockDuration: TimeSpan.FromMinutes(1),
            maxSizeInMegabytes: 1024,
            requiresDuplicateDetection: false,
            requiresSession: false,
            defaultMessageTimeToLive: TimeSpan.FromHours(1),
            autoDeleteOnIdle: TimeSpan.MaxValue,
            deadLetteringOnMessageExpiration: false,
            duplicateDetectionHistoryTimeWindow: TimeSpan.FromMinutes(5),
            maxDeliveryCount: 10,
            enableBatchedOperations: true,
            status: status,
            forwardTo: string.Empty,
            forwardDeadLetteredMessagesTo: string.Empty,
            userMetadata: string.Empty);
}
