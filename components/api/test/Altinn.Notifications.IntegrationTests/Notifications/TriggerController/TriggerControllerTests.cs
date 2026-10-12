using System.Net;

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

        var client = GetTestClient(
            orderProcessingService: serviceMock.Object);

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

        var client = GetTestClient(
            orderProcessingService: serviceMock.Object);

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
        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();
        emailNotificationServiceMock
            .Setup(e => e.SendNotification(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        emailNotificationServiceMock
            .Setup(e => e.SendComposedNotification(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var client = GetTestClient(
            emailNotificationService: emailNotificationServiceMock.Object);

        string url = _basePath + "/sendemail";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        emailNotificationServiceMock.Verify(e => e.SendNotification(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        emailNotificationServiceMock.Verify(e => e.SendComposedNotification(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsAnytime_TaskQueued()
    {
        // Arrange
        var smsNotificationServiceMock = new Mock<ISmsNotificationService>();
        smsNotificationServiceMock
            .Setup(e => e.SendNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var client = GetTestClient(smsNotificationService: smsNotificationServiceMock.Object);

        string url = _basePath + "/sendsmsanytime";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        smsNotificationServiceMock.Verify(e => e.SendNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenAllowed_TaskQueued()
    {
        // Arrange
        var smsNotificationServiceMock = new Mock<ISmsNotificationService>();
        smsNotificationServiceMock
            .Setup(e => e.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var scheduleServiceMock = new Mock<INotificationScheduleService>();
        scheduleServiceMock
            .Setup(e => e.CanSendSmsNow())
            .Returns(true)
            .Verifiable();

        var client = GetTestClient(
            smsNotificationService: smsNotificationServiceMock.Object,
            notificationScheduleService: scheduleServiceMock.Object);

        string url = _basePath + "/sendsmsdaytime";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        scheduleServiceMock.Verify(e => e.CanSendSmsNow(), Times.Once);
        smsNotificationServiceMock.Verify(e => e.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenNotAllowed_TaskNotQueued()
    {
        // Arrange
        var smsNotificationServiceMock = new Mock<ISmsNotificationService>();

        var scheduleServiceMock = new Mock<INotificationScheduleService>();
        scheduleServiceMock
            .Setup(e => e.CanSendSmsNow())
            .Returns(false)
            .Verifiable();

        var client = GetTestClient(
            smsNotificationService: smsNotificationServiceMock.Object,
            notificationScheduleService: scheduleServiceMock.Object);

        string url = _basePath + "/sendsmsdaytime";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        scheduleServiceMock.Verify(e => e.CanSendSmsNow(), Times.Once);
        smsNotificationServiceMock.Verify(e => e.SendNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private HttpClient GetTestClient(
        IStatusFeedService? statusFeedService = null,
        ISmsNotificationService? smsNotificationService = null,
        IOrderProcessingService? orderProcessingService = null,
        IEmailNotificationService? emailNotificationService = null,
        INotificationScheduleService? notificationScheduleService = null)
    {
        statusFeedService ??= new Mock<IStatusFeedService>().Object;
        smsNotificationService ??= new Mock<ISmsNotificationService>().Object;
        orderProcessingService ??= new Mock<IOrderProcessingService>().Object;
        emailNotificationService ??= new Mock<IEmailNotificationService>().Object;
        notificationScheduleService ??= new Mock<INotificationScheduleService>().Object;

        _factory.ResetInstalledMocks();
        _factory.InstallService(statusFeedService);
        _factory.InstallService(smsNotificationService);
        _factory.InstallService(orderProcessingService);
        _factory.InstallService(emailNotificationService);
        _factory.InstallService(notificationScheduleService);

        return _factory.SharedClient;
    }
}
