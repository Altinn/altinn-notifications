using System.Net;

using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TriggerController;

public class TriggerControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.TriggerController>>
{
    private const string _basePath = "/notifications/api/v1/trigger";
    private readonly IntegrationTestWebApplicationFactory<Controllers.TriggerController> _factory;

    public TriggerControllerTests(IntegrationTestWebApplicationFactory<Controllers.TriggerController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Trigger_PastDueOrders_OrderProcessingServiceCalled()  
    {
        // Arrange
        Mock<IOrderProcessingService> serviceMock = new();
        serviceMock
            .Setup(e => e.StartProcessingPastDueOrders())
            .Returns(Task.CompletedTask);

        var smsPublishTaskQueueMock = CreateIdleQueueMock();

        var client = GetTestClient(
            orderProcessingService: serviceMock.Object,
            smsPublishTaskQueue: smsPublishTaskQueueMock.Object);

        string url = _basePath + "/pastdueorders";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.Verify(e => e.StartProcessingPastDueOrders(), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendEmailNotifications_EmailNotificationServiceCalled()
    {
        // Arrange
        Mock<IEmailNotificationService> serviceMock = new();
        serviceMock
            .Setup(e => e.SendNotifications())
            .Returns(Task.CompletedTask)
            .Verifiable();

        var smsPublishTaskQueueMock = CreateIdleQueueMock();

        var client = GetTestClient(
            emailNotificationService: serviceMock.Object,
            smsPublishTaskQueue: smsPublishTaskQueueMock.Object);

        string url = _basePath + "/sendemail";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.Verify(e => e.SendNotifications(), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsAnytime_TaskQueued()
    {
        // Arrange
        var smsPublishTaskQueueMock = CreateIdleQueueMock();
        smsPublishTaskQueueMock
            .Setup(e => e.TryEnqueue(SendingTimePolicy.Anytime))
            .Returns(true)
            .Verifiable();

        var client = GetTestClient(smsPublishTaskQueue: smsPublishTaskQueueMock.Object);

        string url = _basePath + "/sendsmsanytime";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        smsPublishTaskQueueMock.Verify(e => e.TryEnqueue(SendingTimePolicy.Anytime), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenAllowed_TaskQueued()
    {
        // Arrange
        var smsPublishTaskQueueMock = CreateIdleQueueMock();
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
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        scheduleServiceMock.Verify(e => e.CanSendSmsNow(), Times.Once);
        smsPublishTaskQueueMock.Verify(e => e.TryEnqueue(SendingTimePolicy.Daytime), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenNotAllowed_TaskNotQueued()
    {
        // Arrange
        var smsPublishTaskQueueMock = CreateIdleQueueMock();

        var scheduleServiceMock = new Mock<INotificationScheduleService>();
        scheduleServiceMock
            .Setup(e => e.CanSendSmsNow())
            .Returns(false)
            .Verifiable();

        var client = GetTestClient(
            smsPublishTaskQueue: smsPublishTaskQueueMock.Object,
            notificationScheduleService: scheduleServiceMock.Object);

        string url = _basePath + "/sendsmsdaytime";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        scheduleServiceMock.Verify(e => e.CanSendSmsNow(), Times.Once);
        smsPublishTaskQueueMock.Verify(e => e.TryEnqueue(It.IsAny<SendingTimePolicy>()), Times.Never);
    }

    private static Mock<ISmsPublishTaskQueue> CreateIdleQueueMock()
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

    private HttpClient GetTestClient(
        IStatusFeedService? statusFeedService = null,
        ISmsPublishTaskQueue? smsPublishTaskQueue = null,
        ISmsNotificationService? smsNotificationService = null,
        IOrderProcessingService? orderProcessingService = null,
        IEmailNotificationService? emailNotificationService = null,
        INotificationScheduleService? notificationScheduleService = null)
    {
        smsPublishTaskQueue ??= CreateIdleQueueMock().Object;
        statusFeedService ??= new Mock<IStatusFeedService>().Object;
        smsNotificationService ??= new Mock<ISmsNotificationService>().Object;
        orderProcessingService ??= new Mock<IOrderProcessingService>().Object;
        emailNotificationService ??= new Mock<IEmailNotificationService>().Object;
        notificationScheduleService ??= new Mock<INotificationScheduleService>().Object;

        return _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(statusFeedService);
                services.AddSingleton(smsPublishTaskQueue);
                services.AddSingleton(smsNotificationService);
                services.AddSingleton(orderProcessingService);
                services.AddSingleton(emailNotificationService);
                services.AddSingleton(notificationScheduleService);
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();
    }
}
