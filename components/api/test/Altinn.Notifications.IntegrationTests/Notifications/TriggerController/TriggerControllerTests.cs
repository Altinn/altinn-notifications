using System.Net;

using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TriggerController;

public class TriggerControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Program>>
{
    private const string _basePath = "/notifications/api/v1/trigger";
    private readonly IntegrationTestWebApplicationFactory<Program> _factory;

    public TriggerControllerTests(IntegrationTestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Trigger_PastDueOrder_OrderProcessingServiceCalled()
    {
        // Arrange
        Mock<IOrderProcessingService> serviceMock = new();

        var smsPublishTaskQueueMock = CreateIdleSmsQueueMock();

        var client = GetTestClient(
            orderProcessingService: serviceMock.Object,
            smsPublishTaskQueue: smsPublishTaskQueueMock.Object);

        string url = _basePath + "/pastdueoneorder";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Trigger_RetryOrder_OrderProcessingServiceCalled()
    {
        // Arrange
        Mock<IOrderProcessingService> serviceMock = new();

        var smsPublishTaskQueueMock = CreateIdleSmsQueueMock();

        var client = GetTestClient(
            orderProcessingService: serviceMock.Object,
            smsPublishTaskQueue: smsPublishTaskQueueMock.Object);

        string url = _basePath + "/retryoneorder";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Trigger_SendEmailNotifications_TaskQueued()
    {
        // Arrange
        var emailPublishTaskQueueMock = CreateIdleEmailQueueMock();
        emailPublishTaskQueueMock
            .Setup(e => e.TryEnqueue())
            .Returns(true)
            .Verifiable();

        var composedEmailPublishSignalMock = CreateIdleComposedEmailSignalMock();
        composedEmailPublishSignalMock
            .Setup(e => e.TryEnqueue())
            .Returns(true)
            .Verifiable();

        var client = GetTestClient(
            emailPublishTaskQueue: emailPublishTaskQueueMock.Object,
            composedEmailPublishSignal: composedEmailPublishSignalMock.Object);

        string url = _basePath + "/sendemail";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        emailPublishTaskQueueMock.Verify(e => e.TryEnqueue(), Times.Once);
        composedEmailPublishSignalMock.Verify(e => e.TryEnqueue(), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsAnytime_TaskQueued()
    {
        // Arrange
        var smsPublishTaskQueueMock = CreateIdleSmsQueueMock();
        smsPublishTaskQueueMock
            .Setup(e => e.TryEnqueue(SendingTimePolicy.Anytime))
            .Returns(true)
            .Verifiable();

        var client = GetTestClient(smsPublishTaskQueue: smsPublishTaskQueueMock.Object);

        string url = _basePath + "/sendsmsanytime";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        smsPublishTaskQueueMock.Verify(e => e.TryEnqueue(SendingTimePolicy.Anytime), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenAllowed_TaskQueued()
    {
        // Arrange
        var smsPublishTaskQueueMock = CreateIdleSmsQueueMock();
        smsPublishTaskQueueMock
            .Setup(e => e.TryEnqueue(SendingTimePolicy.Daytime))
            .Returns(true)
            .Verifiable();

        var scheduleServiceMock = new Mock<INotificationScheduleService>();
        scheduleServiceMock
            .Setup(e => e.CanSendSmsNow())
            .Returns(true)
            .Verifiable();

        var client = GetTestClient(
            smsPublishTaskQueue: smsPublishTaskQueueMock.Object,
            notificationScheduleService: scheduleServiceMock.Object);

        string url = _basePath + "/sendsmsdaytime";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        scheduleServiceMock.Verify(e => e.CanSendSmsNow(), Times.Once);
        smsPublishTaskQueueMock.Verify(e => e.TryEnqueue(SendingTimePolicy.Daytime), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenNotAllowed_TaskNotQueued()
    {
        // Arrange
        var smsPublishTaskQueueMock = CreateIdleSmsQueueMock();

        var scheduleServiceMock = new Mock<INotificationScheduleService>();
        scheduleServiceMock
            .Setup(e => e.CanSendSmsNow())
            .Returns(false)
            .Verifiable();

        var client = GetTestClient(
            smsPublishTaskQueue: smsPublishTaskQueueMock.Object,
            notificationScheduleService: scheduleServiceMock.Object);

        string url = _basePath + "/sendsmsdaytime";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        scheduleServiceMock.Verify(e => e.CanSendSmsNow(), Times.Once);
        smsPublishTaskQueueMock.Verify(e => e.TryEnqueue(It.IsAny<SendingTimePolicy>()), Times.Never);
    }

    private static Mock<ISmsPublishTaskQueue> CreateIdleSmsQueueMock()
    {
        var anytimeTaskCompletionSource = new TaskCompletionSource();
        var daytimeTaskCompletionSource = new TaskCompletionSource();

        var smsPublishTaskQueueMock = new Mock<ISmsPublishTaskQueue>();

        smsPublishTaskQueueMock
            .Setup(e => e.WaitAsync(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()))
            .Returns(anytimeTaskCompletionSource.Task);

        smsPublishTaskQueueMock
            .Setup(e => e.WaitAsync(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()))
            .Returns(daytimeTaskCompletionSource.Task);

        smsPublishTaskQueueMock
            .Setup(e => e.MarkCompleted(It.IsAny<SendingTimePolicy>()));

        return smsPublishTaskQueueMock;
    }

    private static Mock<IEmailPublishTaskQueue> CreateIdleEmailQueueMock()
    {
        var taskCompletionSource = new TaskCompletionSource();
        var emailPublishTaskQueueMock = new Mock<IEmailPublishTaskQueue>();
        emailPublishTaskQueueMock
            .Setup(e => e.WaitAsync(It.IsAny<CancellationToken>()))
            .Returns(taskCompletionSource.Task);
        return emailPublishTaskQueueMock;
    }

    private static Mock<IComposedEmailPublishSignal> CreateIdleComposedEmailSignalMock()
    {
        var taskCompletionSource = new TaskCompletionSource();
        var composedEmailPublishSignalMock = new Mock<IComposedEmailPublishSignal>();
        composedEmailPublishSignalMock
            .Setup(e => e.WaitAsync(It.IsAny<CancellationToken>()))
            .Returns(taskCompletionSource.Task);

        return composedEmailPublishSignalMock;
    }

    private HttpClient GetTestClient(
        IStatusFeedService? statusFeedService = null,
        ISmsPublishTaskQueue? smsPublishTaskQueue = null,
        IEmailPublishTaskQueue? emailPublishTaskQueue = null,
        ISmsNotificationService? smsNotificationService = null,
        IOrderProcessingService? orderProcessingService = null,
        IEmailNotificationService? emailNotificationService = null,
        IComposedEmailPublishSignal? composedEmailPublishSignal = null,
        INotificationScheduleService? notificationScheduleService = null)
    {
        smsPublishTaskQueue ??= CreateIdleSmsQueueMock().Object;
        emailPublishTaskQueue ??= CreateIdleEmailQueueMock().Object;
        statusFeedService ??= new Mock<IStatusFeedService>().Object;
        smsNotificationService ??= new Mock<ISmsNotificationService>().Object;
        orderProcessingService ??= new Mock<IOrderProcessingService>().Object;
        emailNotificationService ??= new Mock<IEmailNotificationService>().Object;
        composedEmailPublishSignal ??= new Mock<IComposedEmailPublishSignal>().Object;
        notificationScheduleService ??= new Mock<INotificationScheduleService>().Object;

        _factory.ResetInstalledMocks();
        _factory.InstallService(statusFeedService);
        _factory.InstallService(smsPublishTaskQueue);
        _factory.InstallService(emailPublishTaskQueue);
        _factory.InstallService(smsNotificationService);
        _factory.InstallService(orderProcessingService);
        _factory.InstallService(emailNotificationService);
        _factory.InstallService(composedEmailPublishSignal);
        _factory.InstallService(notificationScheduleService);

        return _factory.SharedClient;
    }
}
