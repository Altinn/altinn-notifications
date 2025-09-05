using System.Net;

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
        Mock<IOrderProcessingService> serviceMock = new();
        serviceMock
            .Setup(s => s.StartProcessingPastDueOrders());

        var client = GetTestClient(orderProcessingService: serviceMock.Object);

        string url = _basePath + "/pastdueorders";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Trigger_SendEmailNotifications_EmailNotificationServiceCalled()
    {
        Mock<IEmailNotificationService> serviceMock = new();
        serviceMock
            .Setup(s => s.SendNotifications());

        var client = GetTestClient(emailNotificationService: serviceMock.Object);

        string url = _basePath + "/sendemail";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsAnytime_SmsNotificationServiceCalled()
    {
        // Arrange
        Mock<ISmsNotificationService> serviceMock = new();
        serviceMock
            .Setup(e => e.SendNotifications(It.IsAny<CancellationToken>(), SendingTimePolicy.Anytime))
            .Returns(Task.CompletedTask);

        var client = GetTestClient(smsNotificationService: serviceMock.Object);

        string url = _basePath + "/sendsmsanytime";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenAllowed_SmsNotificationServiceCalled()
    {
        // Arrange
        Mock<ISmsNotificationService> smsServiceMock = new();
        smsServiceMock
            .Setup(e => e.SendNotifications(It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .Returns(Task.CompletedTask);

        Mock<INotificationScheduleService> scheduleServiceMock = new();
        scheduleServiceMock
            .Setup(e => e.CanSendSmsNow())
            .Returns(true);

        var client = GetTestClient(
            smsNotificationService: smsServiceMock.Object,
            notificationScheduleService: scheduleServiceMock.Object);

        string url = _basePath + "/sendsmsdaytime";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        smsServiceMock.VerifyAll();
        scheduleServiceMock.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenNotAllowed_SmsNotificationServiceNotCalled()
    {
        // Arrange
        Mock<ISmsNotificationService> smsServiceMock = new();
        Mock<INotificationScheduleService> scheduleServiceMock = new();
        scheduleServiceMock
            .Setup(e => e.CanSendSmsNow())
            .Returns(false);

        var client = GetTestClient(
            smsNotificationService: smsServiceMock.Object,
            notificationScheduleService: scheduleServiceMock.Object);

        string url = _basePath + "/sendsmsdaytime";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        smsServiceMock.Verify(s => s.SendNotifications(It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()), Times.Never);
        scheduleServiceMock.VerifyAll();
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsAnytime_CancellationDuringProcessing_PropagatedToService()
    {
        // Arrange
        using var tokenSource = new CancellationTokenSource();

        var operationStartedTcs = new TaskCompletionSource<bool>();

        Mock<ISmsNotificationService> serviceMock = new();
        serviceMock
            .Setup(s => s.SendNotifications(It.IsAny<CancellationToken>(), SendingTimePolicy.Anytime))
            .Callback<CancellationToken, SendingTimePolicy>((ct, _) =>
            {
                // Signal that the operation has started
                operationStartedTcs.SetResult(true);

                // Block until cancellation to simulate long-running work
                ct.WaitHandle.WaitOne();
            })
            .Returns(Task.CompletedTask);

        var client = GetTestClient(smsNotificationService: serviceMock.Object);

        string url = _basePath + "/sendsmsanytime";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        var requestTask = client.SendAsync(httpRequestMessage, tokenSource.Token);

        // Wait for the service method to start running
        await operationStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Cancel the operation
        await tokenSource.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => requestTask);
        serviceMock.Verify(e => e.SendNotifications(It.IsAny<CancellationToken>(), SendingTimePolicy.Anytime), Times.Once);
    }

    private HttpClient GetTestClient(
    IStatusFeedService? statusFeedService = null,
    ISmsNotificationService? smsNotificationService = null,
    IOrderProcessingService? orderProcessingService = null,
    IEmailNotificationService? emailNotificationService = null,
    INotificationScheduleService? notificationScheduleService = null)
    {
        // Create default mock services if not provided
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
                // Register all service mocks
                services.AddSingleton(statusFeedService);
                services.AddSingleton(smsNotificationService);
                services.AddSingleton(orderProcessingService);
                services.AddSingleton(emailNotificationService);
                services.AddSingleton(notificationScheduleService);

                // Set up mock authentication and authorization
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();
    }
}
