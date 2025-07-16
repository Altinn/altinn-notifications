using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.ShortMessageService;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
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

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();

        var controller = new InstantOrdersController(
            Mock.Of<IDateTimeService>(),
            Options.Create(new NotificationConfig { DefaultSmsSenderNumber = "Altinn" }),
            Mock.Of<IShortMessageServiceClient>(),
            orderRequestServiceMock.Object,
            validatorMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContextMock.Object }
        };

        // Act
        var result = await controller.Post(request);

        // Assert
        Assert.IsType<ForbidResult>(result);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_WithExistingOrderIdempotency_ReturnsOkWithTrackingInformation()
    {
        // Arrange
        var creator = "ttd";
        var shipmentId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var idempotencyId = "D1112C2C-80B9-430F-9500-A09B76AE8221";
        var sendersReference = "D76AEEF1-C530-4A22-BA9F-9DC4CFF9D9D7";

        var trackingInfo = new InstantNotificationOrderTracking
        {
            OrderChainId = orderChainId,
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = shipmentId,
                SendersReference = sendersReference
            }
        };

        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,
            SendersReference = sendersReference,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    TimeToLiveInSeconds = 360,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = "Test message",
                        Sender = "Test sender"
                    }
                }
            }
        };

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(creator, idempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(instantOrderRequestService: orderRequestServiceMock.Object, validator: validatorMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(creator, scope: "altinn:serviceowner/notifications.create"));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<InstantNotificationOrderResponseExt>(responseContent, _options);

        Assert.NotNull(result);
        Assert.Equal(trackingInfo.OrderChainId, result.OrderChainId);
        Assert.Equal(trackingInfo.Notification.ShipmentId, result.Notification.ShipmentId);
        Assert.Equal(trackingInfo.Notification.SendersReference, result.Notification.SendersReference);

        validatorMock.Verify(e => e.Validate(It.Is<InstantNotificationOrderRequestExt>(e => e == request)), Times.Once);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(creator, idempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_WithRegistrationFailure_ReturnsInternalServerErrorWithProblemDetails()
    {
        // Arrange
        var creator = "ttd";
        var idempotencyId = Guid.NewGuid().ToString();
        var sendersReference = "A1B2C3D4-E5F6-7890-1234-56789ABCDEF0";

        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,
            SendersReference = sendersReference,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    TimeToLiveInSeconds = 360,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = "Test message",
                        Sender = "Test sender"
                    }
                }
            }
        };

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(creator, idempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var shortMessageServiceClientMock = new Mock<IShortMessageServiceClient>();

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            instantOrderRequestService: orderRequestServiceMock.Object,
            shortMessageServiceClient: shortMessageServiceClientMock.Object,
            validator: validatorMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(creator, scope: "altinn:serviceowner/notifications.create"));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(responseContent, _options);

        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);
        Assert.Equal("Registration failed", problem.Title);
        Assert.Contains("Failed to register the instant notification order", problem.Detail);

        validatorMock.Verify(e => e.Validate(request), Times.Once);
        shortMessageServiceClientMock.Verify(e => e.SendAsync(It.IsAny<ShortMessage>()), Times.Never);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(creator, idempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_WithValidRequest_RegistrationAndSmsSendingSucceeds_ReturnsCreatedWithTrackingInformation()
    {
        // Arrange
        var creator = "ttd";
        var shipmentId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var idempotencyId = Guid.NewGuid().ToString();
        var sendersReference = "A1B2C3D4-E5F6-7890-1234-56789ABCDEF0";

        var trackingInfo = new InstantNotificationOrderTracking
        {
            OrderChainId = orderChainId,
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = shipmentId,
                SendersReference = sendersReference
            }
        };

        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,
            SendersReference = sendersReference,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    TimeToLiveInSeconds = 360,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = "Test message",
                        Sender = "Test sender"
                    }
                }
            }
        };

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(creator, idempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        var shortMessageServiceClientMock = new Mock<IShortMessageServiceClient>();
        shortMessageServiceClientMock
            .Setup(e => e.SendAsync(It.IsAny<ShortMessage>()))
            .ReturnsAsync(new ShortMessageSendResult
            {
                Success = true,
                ErrorDetails = null,
                StatusCode = HttpStatusCode.OK
            });

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            instantOrderRequestService: orderRequestServiceMock.Object,
            shortMessageServiceClient: shortMessageServiceClientMock.Object,
            validator: validatorMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(creator, scope: "altinn:serviceowner/notifications.create"));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<InstantNotificationOrderResponseExt>(responseContent, _options);

        Assert.NotNull(result);
        Assert.Equal(trackingInfo.OrderChainId, result.OrderChainId);
        Assert.Equal(trackingInfo.Notification.ShipmentId, result.Notification.ShipmentId);
        Assert.Equal(trackingInfo.Notification.SendersReference, result.Notification.SendersReference);

        validatorMock.Verify(e => e.Validate(request), Times.Once);
        shortMessageServiceClientMock.Verify(e => e.SendAsync(It.IsAny<ShortMessage>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(creator, idempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("+4712345678", 60)]
    [InlineData("+4799999999", 172801)]
    public async Task Post_WithInvalidPhoneNumberOrTimeToLive_ModelValidationFails_ReturnsBadRequest(string phoneNumber, int timeToLiveInSeconds)
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = "7D9B1679-C6DA-4279-89C3-6BC9C5969842",

            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new()
                {
                    PhoneNumber = phoneNumber,
                    TimeToLiveInSeconds = timeToLiveInSeconds,
                    ShortMessageContent = new()
                    {
                        Sender = "Altinn",
                        Body = "Test message"
                    }
                }
            }
        };

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock
            .Setup(v => v.Validate(request))
            .Returns(new ValidationResult(new List<ValidationFailure>
            {
                new(nameof(ShortMessageDeliveryDetailsExt.PhoneNumber), "Recipient phone number is not a valid mobile number."),
                new(nameof(ShortMessageDeliveryDetailsExt.TimeToLiveInSeconds), "Time-to-live must be between 60 and 172800 seconds (48 hours).")
            }));

        var client = GetTestClient(validator: validatorMock.Object);

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

    [Theory]
    [InlineData("", "+4799999999", "test body", 60)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", "", "test body", 60)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", "+4799999999", "", 60)]
    public async Task Post_WithMissingRequiredInformation_RequestDeserializationFails_ReturnsBadRequest(string idempotencyId, string phoneNumber, string message, int timeToLiveInSeconds)
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

    private HttpClient GetTestClient(IInstantOrderRequestService? instantOrderRequestService = null, IShortMessageServiceClient? shortMessageServiceClient = null, IValidator<InstantNotificationOrderRequestExt>? validator = null)
    {
        shortMessageServiceClient ??= Mock.Of<IShortMessageServiceClient>();
        instantOrderRequestService ??= Mock.Of<IInstantOrderRequestService>();
        validator ??= Mock.Of<IValidator<InstantNotificationOrderRequestExt>>();

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(validator);
                services.AddSingleton(shortMessageServiceClient);
                services.AddSingleton(instantOrderRequestService);
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();
    }
}
