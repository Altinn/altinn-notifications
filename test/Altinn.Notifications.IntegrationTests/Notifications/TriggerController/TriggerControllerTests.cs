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
        Mock<IOrderProcessingService> serviceMock = new();
        serviceMock.Setup(e => e.StartProcessingPastDueOrders());

        var client = GetTestClient(orderProcessingService: serviceMock.Object);

        var response = await client.PostAsync(_basePath + "/pastdueorders", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Trigger_SendEmailNotifications_EmailNotificationServiceCalled()
    {
        Mock<IEmailNotificationService> serviceMock = new();
        serviceMock.Setup(e => e.SendNotifications());

        var client = GetTestClient(emailNotificationService: serviceMock.Object);

        var response = await client.PostAsync(_basePath + "/sendemail", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsAnytime_TaskQueued()
    {
        var queueMock = new Mock<ISmsPublishTaskQueue>();
        queueMock
            .Setup(e => e.TryEnqueue(SendingTimePolicy.Anytime))
            .Returns(true)
            .Verifiable();

        var client = GetTestClient(smsPublishTaskQueue: queueMock.Object);

        var response = await client.PostAsync(_basePath + "/sendsmsanytime", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        queueMock.Verify(e => e.TryEnqueue(SendingTimePolicy.Anytime), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenAllowed_TaskQueued()
    {
        var queueMock = new Mock<ISmsPublishTaskQueue>();
        queueMock
            .Setup(e => e.TryEnqueue(SendingTimePolicy.Daytime))
            .Returns(true)
            .Verifiable();

        var scheduleServiceMock = new Mock<INotificationScheduleService>();
        scheduleServiceMock
            .Setup(e => e.CanSendSmsNow())
            .Returns(true)
            .Verifiable();

        var client = GetTestClient(
            smsPublishTaskQueue: queueMock.Object,
            notificationScheduleService: scheduleServiceMock.Object);

        var response = await client.PostAsync(_basePath + "/sendsmsdaytime", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        scheduleServiceMock.Verify(e => e.CanSendSmsNow(), Times.Once);
        queueMock.Verify(e => e.TryEnqueue(SendingTimePolicy.Daytime), Times.Once);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_WhenNotAllowed_TaskNotQueued()
    {
        var queueMock = new Mock<ISmsPublishTaskQueue>();

        var scheduleServiceMock = new Mock<INotificationScheduleService>();
        scheduleServiceMock
            .Setup(e => e.CanSendSmsNow())
            .Returns(false)
            .Verifiable();

        var client = GetTestClient(
            smsPublishTaskQueue: queueMock.Object,
            notificationScheduleService: scheduleServiceMock.Object);

        var response = await client.PostAsync(_basePath + "/sendsmsdaytime", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        scheduleServiceMock.Verify(e => e.CanSendSmsNow(), Times.Once);
        queueMock.Verify(e => e.TryEnqueue(It.IsAny<SendingTimePolicy>()), Times.Never);
    }

    private HttpClient GetTestClient(
        IStatusFeedService? statusFeedService = null,
        ISmsPublishTaskQueue? smsPublishTaskQueue = null,
        ISmsNotificationService? smsNotificationService = null,
        IOrderProcessingService? orderProcessingService = null,
        IEmailNotificationService? emailNotificationService = null,
        INotificationScheduleService? notificationScheduleService = null)
    {
        statusFeedService ??= new Mock<IStatusFeedService>().Object;
        smsPublishTaskQueue ??= new Mock<ISmsPublishTaskQueue>().Object;
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
