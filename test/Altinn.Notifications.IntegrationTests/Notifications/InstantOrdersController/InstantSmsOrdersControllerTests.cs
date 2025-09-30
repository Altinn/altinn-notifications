using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models.Orders;
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
using Controllers = Altinn.Notifications.Controllers;

namespace Altinn.Notifications.IntegrationTests.Notifications.InstantOrdersController;

/// <summary>
/// Integration tests for the <see cref="Controllers.InstantOrdersController"/> SMS endpoint with flattened structure.
/// </summary>
public class InstantSmsOrdersControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.InstantOrdersController>>
{
    private const string BasePath = "/notifications/api/v1/future/orders/instant/sms";
    private const string NotificationCreationScope = "altinn:serviceowner/notifications.create";

    private const int ValidTimeToLive = 360;
    private const int MinimumTimeToLive = 60;
    private const int MaximumTimeToLive = 17280;

    private const string CreatorShortName = "ttd";
    private const string SenderIdentifier = "Altinn";
    private const string ValidPhoneNumber = "+4799999999";
    private const string ValidMessageBody = "Test message";
    private const string InvalidPhoneNumber = "+4712345678";
    private const string ValidIdempotencyId = "D1112C2C-80B9-430F-9500-A09B76AE8221";
    private const string ValidSendersReference = "89F4BE02-2722-4E77-87AC-23081CC6365D";

    private readonly JsonSerializerOptions _options;
    private readonly IntegrationTestWebApplicationFactory<Controllers.InstantOrdersController> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantSmsOrdersControllerTests"/> class.
    /// </summary>
    public InstantSmsOrdersControllerTests(IntegrationTestWebApplicationFactory<Controllers.InstantOrdersController> factory)
    {
        _factory = factory;
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task Post_WhenOrderPersistenceIsCanceled_Returns499RequestTerminated()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = ValidPhoneNumber,
                TimeToLiveInSeconds = ValidTimeToLive,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = ValidMessageBody,
                    Sender = SenderIdentifier
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var validatorMock = new Mock<IValidator<InstantSmsNotificationOrderRequestExt>>();
        validatorMock.Setup(v => v.Validate(request)).Returns(new ValidationResult());

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();

        orderRequestServiceMock
            .Setup(s => s.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(s => s.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

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

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Once);
        validatorMock.Verify(e => e.Validate(request), Times.Once);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_WithValidNewOrderRequest_ReturnsCreatedAndTrackingInformation()
    {
        // Arrange
        var trackingInfo = new InstantNotificationOrderTracking
        {
            OrderChainId = Guid.NewGuid(),
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = ValidSendersReference
            }
        };

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = ValidPhoneNumber,
                TimeToLiveInSeconds = ValidTimeToLive,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = ValidMessageBody,
                    Sender = SenderIdentifier
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        var validatorMock = new Mock<IValidator<InstantSmsNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

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

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Once);
        validatorMock.Verify(e => e.Validate(request), Times.Once);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_WhenCreatorShortNameIsMissing_ReturnsForbiddenAndDoesNotPersistOrder()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = ValidPhoneNumber,
                TimeToLiveInSeconds = ValidTimeToLive,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = ValidMessageBody,
                    Sender = SenderIdentifier
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var validatorMock = new Mock<IValidator<InstantSmsNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(string.Empty, scope: NotificationCreationScope));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        validatorMock.Verify(e => e.Validate(request), Times.Once);

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Never);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_WhenOrderWithSameIdempotencyIdExists_ReturnsOkWithExistingTrackingInfo()
    {
        // Arrange
        var trackingInfo = new InstantNotificationOrderTracking
        {
            OrderChainId = Guid.NewGuid(),
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = ValidSendersReference
            }
        };

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = ValidPhoneNumber,
                TimeToLiveInSeconds = ValidTimeToLive,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = ValidMessageBody,
                    Sender = SenderIdentifier
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        orderRequestServiceMock
            .Setup(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var validatorMock = new Mock<IValidator<InstantSmsNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

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
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()), Times.Once);

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Never);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_WhenOrderRegistrationFails_ReturnsInternalServerErrorWithProblemDetails()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = ValidPhoneNumber,
                TimeToLiveInSeconds = ValidTimeToLive,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = ValidMessageBody,
                    Sender = SenderIdentifier
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var validatorMock = new Mock<IValidator<InstantSmsNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

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

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Once);
        validatorMock.Verify(e => e.Validate(request), Times.Once);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(InvalidPhoneNumber, MinimumTimeToLive)]
    [InlineData(ValidPhoneNumber, MaximumTimeToLive + 20)]
    public async Task Post_WhenPhoneNumberOrTimeToLiveIsInvalid_ReturnsBadRequestWithValidationErrors(string phoneNumber, int timeToLiveInSeconds)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = timeToLiveInSeconds,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = ValidMessageBody,
                    Sender = SenderIdentifier
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var validatorMock = new Mock<IValidator<InstantSmsNotificationOrderRequestExt>>();
        validatorMock
        .Setup(v => v.Validate(request))
        .Returns<InstantSmsNotificationOrderRequestExt>(request =>
        {
            var failures = new List<ValidationFailure>();

            if (request.RecipientSms.PhoneNumber != ValidPhoneNumber)
            {
                failures.Add(new ValidationFailure(nameof(ShortMessageDeliveryDetailsExt.PhoneNumber), "Recipient phone number is not a valid mobile number."));
            }

            if (request.RecipientSms.TimeToLiveInSeconds != MaximumTimeToLive)
            {
                failures.Add(new ValidationFailure(nameof(ShortMessageDeliveryDetailsExt.TimeToLiveInSeconds), "Time-to-live must be between 60 and 172800 seconds (48 hours)."));
            }

            return new ValidationResult(failures);
        });

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();

        orderRequestServiceMock
            .Setup(s => s.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(s => s.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        // Act
        var response = await client.PostAsync(BasePath, requestContent);
        string responseContent = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent, _options);

        // Assert
        Assert.NotNull(problem);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        validatorMock.Verify(e => e.Validate(request), Times.Once);

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Never);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()), Times.Never);
        orderRequestServiceMock.Verify(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", ValidPhoneNumber, ValidMessageBody, MinimumTimeToLive)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", "", ValidMessageBody, MinimumTimeToLive)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", ValidPhoneNumber, "", MinimumTimeToLive)]
    public async Task Post_WhenRequiredInformationAreMissing_ReturnsBadRequestWithValidationErrors(string idempotencyId, string phoneNumber, string message, int timeToLiveInSeconds)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = timeToLiveInSeconds,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = message,
                    Sender = SenderIdentifier
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

    [Fact]
    public async Task Post_WithNullSender_UsesDefaultSenderFromConfiguration()
    {
        // Arrange
        var trackingInfo = new InstantNotificationOrderTracking
        {
            OrderChainId = Guid.NewGuid(),
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = ValidSendersReference
            }
        };

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = ValidPhoneNumber,
                TimeToLiveInSeconds = ValidTimeToLive,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = ValidMessageBody,
                    Sender = null // Null sender should trigger default
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(e => e.PersistInstantSmsNotificationAsync(It.IsAny<InstantSmsNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        var validatorMock = new Mock<IValidator<InstantSmsNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        orderRequestServiceMock.Verify(
            e => e.PersistInstantSmsNotificationAsync(
            It.Is<InstantSmsNotificationOrder>(order =>
                order.ShortMessageDeliveryDetails.ShortMessageContent.Sender == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private HttpClient GetTestClient(IDateTimeService? dateTimeService = null, IInstantOrderRequestService? instantOrderRequestService = null, IValidator<InstantSmsNotificationOrderRequestExt>? validator = null)
    {
        dateTimeService ??= Mock.Of<IDateTimeService>();
        instantOrderRequestService ??= Mock.Of<IInstantOrderRequestService>();
        validator ??= Mock.Of<IValidator<InstantSmsNotificationOrderRequestExt>>();

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(validator);
                services.AddSingleton(dateTimeService);
                services.AddSingleton(instantOrderRequestService);
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();
    }
}
