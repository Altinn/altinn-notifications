using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.InstantEmailService;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Tests.Notifications.Utils;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.InstantOrdersController;

/// <summary>
/// Integration tests for the <see cref="Controllers.InstantOrdersController"/> email endpoint with flattened structure.
/// </summary>
public class InstantEmailOrdersControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Program>>
{
    private const string BasePath = "/notifications/api/v1/future/orders/instant/email";
    private const string NotificationCreationScope = "altinn:serviceowner/notifications.create";

    private const string CreatorShortName = "ttd";
    private const string ValidEmailAddress = "test@example.com";
    private const string InvalidEmailAddress = "invalid-email";
    private const string ValidSubject = "Test Subject";
    private const string ValidBody = "Test email body";
    private const string ValidSenderEmailAddress = "sender@altinn.no";
    private const string ValidIdempotencyId = "D1112C2C-80B9-430F-9500-A09B76AE8221";
    private const string ValidSendersReference = "89F4BE02-2722-4E77-87AC-23081CC6365D";

    private readonly JsonSerializerOptions _options;
    private readonly IntegrationTestWebApplicationFactory<Program> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantEmailOrdersControllerTests"/> class.
    /// </summary>
    public InstantEmailOrdersControllerTests(IntegrationTestWebApplicationFactory<Program> factory)
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
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = ValidEmailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = ValidSubject,
                    Body = ValidBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = ValidSenderEmailAddress
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var validatorMock = new Mock<IValidator<InstantEmailNotificationOrderRequestExt>>();
        validatorMock.Setup(v => v.Validate(request)).Returns(new ValidationResult());

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();

        orderRequestServiceMock
            .Setup(s => s.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        orderRequestServiceMock
            .Setup(s => s.PersistInstantEmailNotificationAsync(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, requestContent, TestContext.Current.CancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonSerializer.Deserialize<AltinnProblemDetails>(responseContent, _options);

        // Assert
        Assert.Equal(499, (int)response.StatusCode);

        Assert.NotNull(problem);
        Assert.Equal("NOT-00002", problem.ErrorCode.ToString()); // Problems.RequestTerminated
        Assert.Equal((int)response.StatusCode, problem.Status);

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Once);
        validatorMock.Verify(e => e.Validate(request), Times.Once);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantEmailNotificationAsync(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
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

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = ValidEmailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = ValidSubject,
                    Body = ValidBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = ValidSenderEmailAddress
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
            .Setup(e => e.PersistInstantEmailNotificationAsync(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        var validatorMock = new Mock<IValidator<InstantEmailNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<InstantNotificationOrderResponseExt>(responseContent, _options);

        Assert.NotNull(result);
        Assert.Equal(trackingInfo.OrderChainId, result.OrderChainId);
        Assert.Equal(trackingInfo.Notification.ShipmentId, result.Notification.ShipmentId);
        Assert.Equal(trackingInfo.Notification.SendersReference, result.Notification.SendersReference);

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Once);
        validatorMock.Verify(e => e.Validate(request), Times.Once);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()), Times.Once);
        orderRequestServiceMock.Verify(e => e.PersistInstantEmailNotificationAsync(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_WhenCreatorShortNameIsMissing_ReturnsForbiddenAndDoesNotPersistOrder()
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = ValidEmailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = ValidSubject,
                    Body = ValidBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = ValidSenderEmailAddress
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var validatorMock = new Mock<IValidator<InstantEmailNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(string.Empty, scope: NotificationCreationScope));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        validatorMock.Verify(e => e.Validate(request), Times.Once);

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Never);
        orderRequestServiceMock.Verify(e => e.PersistInstantEmailNotificationAsync(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = ValidEmailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = ValidSubject,
                    Body = ValidBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = ValidSenderEmailAddress
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();
        orderRequestServiceMock
            .Setup(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        var validatorMock = new Mock<IValidator<InstantEmailNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<InstantNotificationOrderResponseExt>(responseContent, _options);

        Assert.NotNull(result);
        Assert.Equal(trackingInfo.OrderChainId, result.OrderChainId);
        Assert.Equal(trackingInfo.Notification.ShipmentId, result.Notification.ShipmentId);
        Assert.Equal(trackingInfo.Notification.SendersReference, result.Notification.SendersReference);

        validatorMock.Verify(e => e.Validate(request), Times.Once);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()), Times.Once);

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Never);
        orderRequestServiceMock.Verify(e => e.PersistInstantEmailNotificationAsync(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(InvalidEmailAddress)]
    public async Task Post_WhenEmailAddressIsInvalid_ReturnsBadRequestWithValidationErrors(string emailAddress)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = ValidSubject,
                    Body = ValidBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = ValidSenderEmailAddress
                }
            }
        };

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        var validatorMock = new Mock<IValidator<InstantEmailNotificationOrderRequestExt>>();
        validatorMock
        .Setup(v => v.Validate(request))
        .Returns<InstantEmailNotificationOrderRequestExt>(request =>
        {
            var failures = new List<ValidationFailure>();

            if (request.InstantEmailDetails.EmailAddress == InvalidEmailAddress)
            {
                failures.Add(new ValidationFailure(nameof(InstantEmailDetailsExt.EmailAddress), "Email address is not valid."));
            }

            return new ValidationResult(failures);
        });

        var orderRequestServiceMock = new Mock<IInstantOrderRequestService>();

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        // Act
        var response = await client.PostAsync(BasePath, requestContent, TestContext.Current.CancellationToken);
        string responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent, _options);

        // Assert
        Assert.NotNull(problem);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        validatorMock.Verify(e => e.Validate(request), Times.Once);

        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Never);
        orderRequestServiceMock.Verify(e => e.RetrieveTrackingInformation(CreatorShortName, ValidIdempotencyId, It.IsAny<CancellationToken>()), Times.Never);
        orderRequestServiceMock.Verify(e => e.PersistInstantEmailNotificationAsync(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", ValidEmailAddress, ValidSubject, ValidBody)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", "", ValidSubject, ValidBody)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", ValidEmailAddress, "", ValidBody)]
    [InlineData("3AFD849E-200D-49FB-80BD-3A91A85B13AE", ValidEmailAddress, ValidSubject, "")]
    public async Task Post_WhenRequiredInformationAreMissing_ReturnsBadRequestWithValidationErrors(string idempotencyId, string emailAddress, string subject, string body)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = subject,
                    Body = body,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = ValidSenderEmailAddress
                }
            }
        };

        HttpClient client = GetTestClient();
        using var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        // Act
        var response = await client.PostAsync(BasePath, requestContent, TestContext.Current.CancellationToken);
        string responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent, _options);

        // Assert
        Assert.NotNull(problem);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithNullSenderEmailAddress_UsesDefaultSenderFromConfiguration()
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

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = ValidEmailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = ValidSubject,
                    Body = ValidBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = null // Null sender should trigger default
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
            .Setup(e => e.PersistInstantEmailNotificationAsync(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        var validatorMock = new Mock<IValidator<InstantEmailNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        orderRequestServiceMock.Verify(
            e => e.PersistInstantEmailNotificationAsync(
            It.Is<InstantEmailNotificationOrder>(order =>
                order.InstantEmailDetails.EmailContent.FromAddress == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(EmailContentTypeExt.Plain)]
    [InlineData(EmailContentTypeExt.Html)]
    public async Task Post_WithDifferentContentTypes_HandlesCorrectly(EmailContentTypeExt contentType)
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

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = ValidEmailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = ValidSubject,
                    Body = contentType == EmailContentTypeExt.Html ? "<p>HTML content</p>" : "Plain text content",
                    ContentType = contentType,
                    SenderEmailAddress = ValidSenderEmailAddress
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
            .Setup(e => e.PersistInstantEmailNotificationAsync(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingInfo);

        var validatorMock = new Mock<IValidator<InstantEmailNotificationOrderRequestExt>>();
        validatorMock.Setup(e => e.Validate(request)).Returns(new ValidationResult());

        var client = GetTestClient(
            validator: validatorMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            instantOrderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        var requestSerialized = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestSerialized, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        orderRequestServiceMock.Verify(
            e => e.PersistInstantEmailNotificationAsync(
            It.Is<InstantEmailNotificationOrder>(order =>
                order.InstantEmailDetails.EmailContent.ContentType == (Altinn.Notifications.Core.Enums.EmailContentType)contentType),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Post_SenderNotRegisteredForServiceOwner_ReturnsBadRequestAndDoesNotSendOrPersist()
    {
        // Arrange
        string unregisteredSender = $"{Guid.NewGuid():N}@test.no";
        var orderRepositoryMock = new Mock<IOrderRepository>();
        var emailClientMock = new Mock<IInstantEmailServiceClient>();

        var instantOrderRequestService = await CreateTestService(orderRepositoryMock.Object, emailClientMock.Object);

        var client = GetTestClient(instantOrderRequestService: instantOrderRequestService, validator: ValidatorThatPasses());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

        using var content = new StringContent(JsonSerializer.Serialize(CreateRequest(unregisteredSender)), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BasePath, content, TestContext.Current.CancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonSerializer.Deserialize<AltinnProblemDetails>(responseContent, _options);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("NOT-00005", problem.ErrorCode.ToString());
        Assert.Contains(unregisteredSender, problem.Detail);

        emailClientMock.Verify(c => c.SendAsync(It.IsAny<InstantEmail>()), Times.Never);
        orderRepositoryMock.Verify(
            r => r.Create(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<EmailNotification>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_SenderRegisteredForServiceOwner_ReturnsCreated()
    {
        // Arrange
        string registeredSender = $"{Guid.NewGuid():N}@test.no";
        await PostgreUtil.InsertServiceOwnerSender(CreatorShortName, "Email", registeredSender);

        try
        {
            var orderRepositoryMock = new Mock<IOrderRepository>();
            orderRepositoryMock
                .Setup(r => r.Create(It.IsAny<InstantEmailNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<EmailNotification>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InstantNotificationOrderTracking
                {
                    OrderChainId = Guid.NewGuid(),
                    Notification = new NotificationOrderChainShipment { ShipmentId = Guid.NewGuid(), SendersReference = ValidSendersReference }
                });

            var emailClientMock = new Mock<IInstantEmailServiceClient>();
            emailClientMock
                .Setup(c => c.SendAsync(It.IsAny<InstantEmail>()))
                .ReturnsAsync(new InstantEmailSendResult { Success = true, StatusCode = HttpStatusCode.OK });

            var instantOrderRequestService = await CreateTestService(orderRepositoryMock.Object, emailClientMock.Object);

            var client = GetTestClient(instantOrderRequestService: instantOrderRequestService, validator: ValidatorThatPasses());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(CreatorShortName, scope: NotificationCreationScope));

            using var content = new StringContent(JsonSerializer.Serialize(CreateRequest(registeredSender)), Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync(BasePath, content, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            emailClientMock.Verify(c => c.SendAsync(It.Is<InstantEmail>(e => e.Sender == registeredSender)), Times.Once);
        }
        finally
        {
            await PostgreUtil.DeleteServiceOwnerSender(CreatorShortName, registeredSender);
        }
    }

    private static InstantEmailNotificationOrderRequestExt CreateRequest(string senderEmailAddress)
    {
        return new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = ValidIdempotencyId,
            SendersReference = ValidSendersReference,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = ValidEmailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Body = ValidBody,
                    Subject = ValidSubject,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = senderEmailAddress
                }
            }
        };
    }

    private static IValidator<InstantEmailNotificationOrderRequestExt> ValidatorThatPasses()
    {
        var validatorMock = new Mock<IValidator<InstantEmailNotificationOrderRequestExt>>();
        validatorMock
            .Setup(v => v.Validate(It.IsAny<InstantEmailNotificationOrderRequestExt>()))
            .Returns(new ValidationResult());

        return validatorMock.Object;
    }

    private static async Task<IInstantOrderRequestService> CreateTestService(IOrderRepository orderRepository, IInstantEmailServiceClient emailClient)
    {
        var config = SenderOwnershipTestUtil.CreateConfig(enforceEmail: true);
        var serviceOwnerService = await SenderOwnershipTestUtil.CreateLoadedServiceOwnerService(config);

        var guidService = SenderOwnershipTestUtil.CreateGuidService();
        var dateTimeService = SenderOwnershipTestUtil.CreateDateTimeService();

        return new InstantOrderRequestService(
            guidService.Object,
            dateTimeService.Object,
            orderRepository,
            config,
            Mock.Of<IShortMessageServiceClient>(),
            emailClient,
            NullLogger<InstantOrderRequestService>.Instance,
            serviceOwnerService);
    }

    private HttpClient GetTestClient(IDateTimeService? dateTimeService = null, IInstantOrderRequestService? instantOrderRequestService = null, IValidator<InstantEmailNotificationOrderRequestExt>? validator = null)
    {
        dateTimeService ??= Mock.Of<IDateTimeService>();
        instantOrderRequestService ??= Mock.Of<IInstantOrderRequestService>();
        validator ??= Mock.Of<IValidator<InstantEmailNotificationOrderRequestExt>>();

        _factory.ResetInstalledMocks();
        _factory.InstallService(validator);
        _factory.InstallService(dateTimeService);
        _factory.InstallService(instantOrderRequestService);
        return _factory.SharedClient;
    }
}
