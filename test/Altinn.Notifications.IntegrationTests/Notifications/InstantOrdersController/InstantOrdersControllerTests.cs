using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Controllers;
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
    private const string BasePath = "/notifications/api/v1/future/orders/instant";
    private const string NotificationCreationScope = "altinn:serviceowner/notifications.create";

    private const int ValidTimeToLive = 360;
    private const int MinimumTimeToLive = 60;
    private const int MaximumTimeToLive = 17280;

    private const string CreatorShortName = "ttd";
    private const string SenderIdentifier = "Altinn";
    private const string ValidPhoneNumber = "+4799999999";
    private const string ValidMessageBody = "Test message";
    private const string InvalidPhoneNumber = "+4712345678";
    private const string IdempotencyId = "D1112C2C-80B9-430F-9500-A09B76AE8221";
    private const string SendersReference = "89F4BE02-2722-4E77-87AC-23081CC6365D";

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
            SendersReference = SendersReference,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new()
                {
                    PhoneNumber = ValidPhoneNumber,
                    TimeToLiveInSeconds = ValidTimeToLive,

                    ShortMessageContent = new()
                    {
                        Body = ValidMessageBody,
                        Sender = SenderIdentifier
                    }
                }
            }
        };

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(It.IsAny<InstantNotificationOrderRequestExt>())).Returns(new ValidationResult());

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();

        var client = GetTestClient(instantOrderRequestService: orderRequestServiceMock.Object, validator: validatorMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(string.Empty, scope: NotificationCreationScope));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_WithExistingOrderIdempotency_ReturnsOkWithTrackingInformation()
    {
        // Arrange
        var trackingInfo = new InstantNotificationOrderTracking
        {
            OrderChainId = Guid.NewGuid(),
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = SendersReference
            }
        };

        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = IdempotencyId,
            SendersReference = SendersReference,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = ValidPhoneNumber,
                    TimeToLiveInSeconds = ValidTimeToLive,
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = ValidMessageBody,
                        Sender = SenderIdentifier
                    }
                }
            }
        };

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(CreatorShortName, IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(instantOrderRequestService: orderRequestServiceMock.Object, validator: validatorMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

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

        validatorMock.Verify(e => e.Validate(request), Times.Once);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, IdempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_WithRegistrationFailure_ReturnsUnprocessableEntityWithProblemDetails()
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = IdempotencyId,
            SendersReference = SendersReference,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = ValidPhoneNumber,
                    TimeToLiveInSeconds = ValidTimeToLive,
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = ValidMessageBody,
                        Sender = SenderIdentifier
                    }
                }
            }
        };

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(CreatorShortName, IdempotencyId, It.IsAny<CancellationToken>()))
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

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(responseContent, _options);

        Assert.NotNull(problem);
        Assert.Equal(422, problem.Status);

        validatorMock.Verify(e => e.Validate(request), Times.Once);
        shortMessageServiceClientMock.Verify(e => e.SendAsync(It.IsAny<ShortMessage>()), Times.Never);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, IdempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_WithValidRequest_RegistrationAndSmsSendingSucceeds_ReturnsCreatedWithTrackingInformation()
    {
        // Arrange
        var trackingInfo = new InstantNotificationOrderTracking
        {
            OrderChainId = Guid.NewGuid(),
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = SendersReference
            }
        };

        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = IdempotencyId,
            SendersReference = SendersReference,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = ValidPhoneNumber,
                    TimeToLiveInSeconds = ValidTimeToLive,
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = ValidMessageBody,
                        Sender = SenderIdentifier
                    }
                }
            }
        };

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(CreatorShortName, IdempotencyId, It.IsAny<CancellationToken>()))
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

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

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
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, IdempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_WhenPersistInstantSmsNotificationAsyncThrowsOperationCanceledException_Returns499RequestTerminated()
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = ValidPhoneNumber,
                    TimeToLiveInSeconds = ValidTimeToLive,
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = ValidMessageBody,
                        Sender = SenderIdentifier
                    }
                }
            }
        };

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock
            .Setup(v => v.Validate(It.IsAny<InstantNotificationOrderRequestExt>()))
            .Returns(new ValidationResult());

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();

        orderRequestServiceMock
            .Setup(s => s.RetrieveTrackingInformation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(s => s.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var client = GetTestClient(instantOrderRequestService: orderRequestServiceMock.Object, validator: validatorMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, requestContent);
        var responseContent = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(responseContent, _options);

        // Assert
        Assert.Equal(499, (int)response.StatusCode);

        Assert.NotNull(problem);
        Assert.Equal(499, problem.Status);
    }

    [Fact]
    public async Task Post_WhenPersistInstantSmsNotificationAsyncThrowsInvalidOperationException_Returns400InvalidNotificationOrderRequest()
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = IdempotencyId,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = ValidPhoneNumber,
                    TimeToLiveInSeconds = ValidTimeToLive,
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = ValidMessageBody,
                        Sender = SenderIdentifier
                    }
                }
            }
        };

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock
            .Setup(v => v.Validate(It.IsAny<InstantNotificationOrderRequestExt>()))
            .Returns(new ValidationResult());

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();

        orderRequestServiceMock
            .Setup(s => s.RetrieveTrackingInformation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(s => s.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated invalid operation."));

        var client = GetTestClient(instantOrderRequestService: orderRequestServiceMock.Object, validator: validatorMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, requestContent);
        var responseContent = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(responseContent, _options);

        // Assert
        Assert.Equal(400, (int)response.StatusCode);

        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    [Theory]
    [InlineData(ValidPhoneNumber, MaximumTimeToLive + 20)]
    [InlineData(InvalidPhoneNumber, MinimumTimeToLive)]
    public async Task Post_WithInvalidPhoneNumberOrTimeToLive_ModelValidationFails_ReturnsBadRequest(string phoneNumber, int timeToLiveInSeconds)
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = IdempotencyId,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new()
                {
                    PhoneNumber = phoneNumber,
                    TimeToLiveInSeconds = timeToLiveInSeconds,

                    ShortMessageContent = new()
                    {
                        Body = ValidMessageBody,
                        Sender = SenderIdentifier
                    }
                }
            }
        };

        var validatorMock = new Mock<IValidator<InstantNotificationOrderRequestExt>>();
        validatorMock
        .Setup(v => v.Validate(It.IsAny<InstantNotificationOrderRequestExt>()))
        .Returns<InstantNotificationOrderRequestExt>(request =>
        {
            var failures = new List<ValidationFailure>();

            if (request.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds > MaximumTimeToLive)
            {
                failures.Add(new ValidationFailure(nameof(ShortMessageDeliveryDetailsExt.TimeToLiveInSeconds), "Time-to-live must be between 60 and 172800 seconds (48 hours)."));
            }

            if (request.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber == InvalidPhoneNumber)
            {
                failures.Add(new ValidationFailure(nameof(ShortMessageDeliveryDetailsExt.PhoneNumber), "Recipient phone number is not a valid mobile number."));
            }

            return new ValidationResult(failures);
        });

        var client = GetTestClient(validator: validatorMock.Object);

        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        // Act
        var response = await client.PostAsync(BasePath, requestContent);
        string responseContent = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent, _options);

        // Assert
        Assert.NotNull(problem);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", ValidPhoneNumber, ValidMessageBody, MinimumTimeToLive)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", "", ValidMessageBody, MinimumTimeToLive)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", ValidPhoneNumber, "", MinimumTimeToLive)]
    public async Task Post_WithMissingRequiredInformation_ModelValidationFails_ReturnsBadRequest(string IdempotencyId, string phoneNumber, string message, int timeToLiveInSeconds)
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = IdempotencyId,
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new()
                {
                    PhoneNumber = phoneNumber,
                    TimeToLiveInSeconds = timeToLiveInSeconds,
                    ShortMessageContent = new()
                    {
                        Body = message,
                        Sender = SenderIdentifier
                    }
                }
            }
        };

        HttpClient client = GetTestClient();
        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

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
