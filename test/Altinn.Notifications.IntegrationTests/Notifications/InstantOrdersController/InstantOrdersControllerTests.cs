using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.ShortMessageService;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TestingControllers;

/// <summary>
/// Integration tests for the <see cref="InstantOrdersController"/>.
/// </summary>
public class InstantOrdersControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<InstantOrdersController>>
{
    private const string BasePath = "/notifications/api/v1/orders/instant";

    private readonly JsonSerializerOptions _options;
    private readonly IntegrationTestWebApplicationFactory<InstantOrdersController> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrdersControllerTests"/> class.
    /// </summary>
    /// <param name="factory">The test web application factory.</param>
    public InstantOrdersControllerTests(IntegrationTestWebApplicationFactory<InstantOrdersController> factory)
    {
        _factory = factory;
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task Post_WithMissingCreatorShortName_ReturnsForbidden()
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            SendersReference = "89F4BE02-2722-4E77-87AC-23081CC6365D",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new()
                {
                    TimeToLiveInSeconds = 360,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new()
                    {
                        Body = "Test message",
                        Sender = "Test sender"
                    }
                }
            }
        };

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(It.IsAny<InstantNotificationOrderRequestExt>())).Returns(new ValidationResult());

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(e => e.Items).Returns(new Dictionary<object, object?> { { "Org", null } });

        var orderRequestServiceMock = new Mock<IOrderRequestService>();

        var controller = new InstantOrdersController(
            Options.Create(new NotificationConfig { DefaultSmsSenderNumber = "Altinn" }),
            orderRequestServiceMock.Object,
            Mock.Of<ISmsOrderProcessingService>(),
            Mock.Of<IShortMessageServiceClient>(),
            validatorMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContextMock.Object }
        };

        // Act
        var result = await controller.Post(request);

        // Assert
        Assert.IsType<ForbidResult>(result);
        orderRequestServiceMock.Verify(e => e.RegisterInstantOrder(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_WithFailedSmsSending_ReturnsInternalServerError()
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            SendersReference = "F9D7B2DD-2436-49E3-A1AF-3967C640B94F",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new()
                {
                    TimeToLiveInSeconds = 360,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new()
                    {
                        Sender = "Altinn",
                        Body = "Test message"
                    }
                }
            }
        };

        var registeredOrder = new NotificationOrder
        {
            Id = Guid.NewGuid(),
            RequestedSendTime = DateTime.UtcNow,
            Recipients =
            [
                new Recipient()
                {
                    AddressInfo = [new SmsAddressPoint("+4799999999")]
                }
            ],

            Templates =
            [
                new SmsTemplate()
                {
                    Body = "Test message",
                    SenderNumber = "Altinn"
                }
            ],

            SendersReference = "F9D7B2DD-2436-49E3-A1AF-3967C640B94F"
        };

        var orderRequestServiceMock = new Mock<IOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RegisterInstantOrder(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registeredOrder);

        var smsOrderProcessingService = new Mock<ISmsOrderProcessingService>();
        smsOrderProcessingService
            .Setup(e => e.ProcessInstantOrder(It.IsAny<NotificationOrder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var smsClientMock = new Mock<IShortMessageServiceClient>();
        smsClientMock
            .Setup(s => s.SendAsync(It.IsAny<ShortMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShortMessageSendResult { Success = false, StatusCode = (HttpStatusCode)500, ErrorDetails = "SMS sending failed" });

        HttpClient client = GetTestClient(
            smsClient: smsClientMock.Object,
            orderRequestService: orderRequestServiceMock.Object,
            smsOrderProcessingService: smsOrderProcessingService.Object);

        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await client.PostAsync(BasePath, requestContent);
        var responseString = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseString, _options);

        // Assert
        Assert.NotNull(problemDetails);
        Assert.Equal("SMS Sending Failed", problemDetails.Title);
        Assert.Contains("Failed to send the SMS", problemDetails.Detail);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithFailedRegistration_ReturnsInternalServerError()
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            SendersReference = "89994D19-2AC3-4CBB-9994-213FD5DACC56",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new()
                {
                    TimeToLiveInSeconds = 360,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new()
                    {
                        Sender = "Altinn",
                        Body = "Test message"
                    }
                }
            }
        };

        var orderRequestServiceMock = new Mock<IOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RegisterInstantOrder(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceError(500, "Failed to create the instant notification order"));

        HttpClient client = GetTestClient(orderRequestService: orderRequestServiceMock.Object);
        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await client.PostAsync(BasePath, requestContent);
        var responseString = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(responseString, _options);

        // Assert
        Assert.NotNull(problem);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Instant Notification Registration Failed", problem.Title);
        Assert.Equal("Failed to register the instant notification order.", problem.Detail);
    }

    [Fact]
    public async Task Post_WithExistingOrder_ReturnsOkWithOrderTrackingDetails()
    {
        // Arrange
        var idempotencyId = Guid.NewGuid().ToString();
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,
            SendersReference = "B0A6022E-2C08-4E2B-A6E3-3303AE7FFE49",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new()
                {
                    TimeToLiveInSeconds = 360,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new()
                    {
                        Body = "Test message",
                        Sender = "Test sender"
                    }
                }
            }
        };

        var existingTracking = new InstantNotificationOrderTracking
        {
            OrderChainId = Guid.NewGuid(),
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "B0A6022E-2C08-4E2B-A6E3-3303AE7FFE49"
            }
        };

        var orderRequestServiceMock = new Mock<IOrderRequestService>();
        orderRequestServiceMock.Setup(e => e.RetrieveInstantOrderTracking("ttd", idempotencyId, It.IsAny<CancellationToken>())).ReturnsAsync(existingTracking);

        HttpClient client = GetTestClient(orderRequestService: orderRequestServiceMock.Object);
        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await client.PostAsync(BasePath, requestContent);
        var responseString = await response.Content.ReadAsStringAsync();
        var responseObject = JsonSerializer.Deserialize<InstantNotificationOrderResponseExt>(responseString, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(responseObject);
        Assert.Equal(existingTracking.OrderChainId, responseObject.OrderChainId);
        Assert.Equal(existingTracking.Notification.SendersReference, responseObject.Notification.SendersReference);
    }

    [Theory]
    [InlineData("", "+4799999999", "test body", 60)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", "", "test body", 60)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", "+4799999999", "", 60)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", "+4799999999", "test body", 50)]
    public async Task Post_WithInvalidRequest_ReturnsBadRequest(string idempotencyId, string phoneNumber, string message, int timeToLiveInSeconds)
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new()
                {
                    PhoneNumber = phoneNumber,
                    TimeToLiveInSeconds = timeToLiveInSeconds,
                    ShortMessageContent = new()
                    {
                        Body = message,
                        Sender = "Altinn"
                    }
                }
            }
        };

        HttpClient client = GetTestClient();
        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await client.PostAsync(BasePath, requestContent);
        string responseContent = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent, _options);

        // Assert
        Assert.NotNull(problem);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient GetTestClient(IOrderRequestService? orderRequestService = null, IShortMessageServiceClient? smsClient = null, ISmsOrderProcessingService? smsOrderProcessingService = null)
    {
        smsClient ??= Mock.Of<IShortMessageServiceClient>();
        orderRequestService ??= Mock.Of<IOrderRequestService>();

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(smsClient);
                services.AddSingleton(orderRequestService);
                services.AddSingleton(smsOrderProcessingService);
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();
    }
}
