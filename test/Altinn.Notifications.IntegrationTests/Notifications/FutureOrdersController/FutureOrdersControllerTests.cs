using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;
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
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TestingControllers;

/// <summary>
/// Integration tests for the <see cref="FutureOrdersController"/>.
/// </summary>
public class FutureOrdersControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<FutureOrdersController>>
{
    private const string BasePath = "/notifications/api/v1/future/orders";

    private readonly JsonSerializerOptions _options;
    private readonly IntegrationTestWebApplicationFactory<FutureOrdersController> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FutureOrdersControllerTests"/> class.
    /// </summary>
    /// <param name="factory">The test web application factory.</param>
    public FutureOrdersControllerTests(IntegrationTestWebApplicationFactory<FutureOrdersController> factory)
    {
        _factory = factory;
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task Post_MissingRequiredRecipient_ReturnsBadRequest()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            Recipient = null!,
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            IdempotencyId = "1B2C3D4E-5F6G-7H8I-9J0K-1L2M3N4O5P6Q",
        };

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        string content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

        // Assert
        Assert.NotNull(problem);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
    }

    [Fact]
    public async Task Post_InvalidRequest_RequestedSendTimeInPast_ReturnsBadRequest()
    {
        // Arrange
        var requestExt = CreateValidRequest();
        requestExt.RequestedSendTime = DateTime.UtcNow.AddHours(-2);

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await client.PostAsync(
            BasePath,
            new StringContent(JsonSerializer.Serialize(requestExt), Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

        Assert.NotNull(problem);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Contains("RequestedSendTime", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_MissingBearer_ReturnsUnauthorized()
    {
        // Arrange
        var requestExt = CreateValidRequest();
        HttpClient client = GetTestClient();

        // Act
        HttpResponseMessage response = await SendPostRequest(client, requestExt);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_OrganizationTokenWithInvalidScope_ReturnsForbidden()
    {
        // Arrange
        var requestExt = CreateValidRequest();
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "dummy:scope"));

        // Act
        HttpResponseMessage response = await SendPostRequest(client, requestExt);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_RegularUserWithValidToken_ReturnsForbidden()
    {
        // Arrange
        var requestExt = CreateValidRequest();
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetUserToken(1337));

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, BasePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestExt), Encoding.UTF8, "application/json")
        };
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_InvalidRequest_MissingCreatorShortName_ReturnsForbidden()
    {
        // Arrange
        var requestExt = CreateValidRequest();
        var orderRequestServiceMock = new Mock<IOrderRequestService>();
        var validatorMock = SetupValidValidator();

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(e => e.Items).Returns(new Dictionary<object, object?> { { "Org", null } });

        var controller = new FutureOrdersController(orderRequestServiceMock.Object, validatorMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContextMock.Object }
        };

        // Act
        var result = await controller.Post(requestExt);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        orderRequestServiceMock.Verify(
            s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_OrganizationTokenWithCorrectScope_ReturnsCreateddWithOrderDetails()
    {
        // Arrange
        var requestExt = CreateValidRequest();
        var expectedResponse = new NotificationOrderChainResponse
        {
            OrderChainId = Guid.NewGuid(),
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "notification-ref"
            }
        };

        var orderRequestServiceMock = new Mock<IOrderRequestService>();
        orderRequestServiceMock
            .Setup(s => s.RegisterNotificationOrderChain(
                It.Is<NotificationOrderChainRequest>(e =>
                    e.IdempotencyId == requestExt.IdempotencyId &&
                    e.RequestedSendTime == requestExt.RequestedSendTime),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        HttpClient client = GetTestClient(orderRequestService: orderRequestServiceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseObject);
        Assert.Null(responseObject.OrderChainReceipt.Reminders);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);
        orderRequestServiceMock.VerifyAll();
    }

    [Fact]
    public async Task Post_PlatformAccessTokenAuthentication_ReturnsCreatedWithOrderDetails()
    {
        // Arrange
        var requestExt = CreateValidRequest();
        var expectedResponse = new NotificationOrderChainResponse
        {
            OrderChainId = Guid.NewGuid(),
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "notification-ref"
            }
        };

        var orderRequestServiceMock = new Mock<IOrderRequestService>();
        orderRequestServiceMock
            .Setup(s => s.RegisterNotificationOrderChain(
                It.Is<NotificationOrderChainRequest>(e =>
                    e.IdempotencyId == requestExt.IdempotencyId &&
                    e.RequestedSendTime == requestExt.RequestedSendTime),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        HttpClient client = GetTestClient(orderRequestService: orderRequestServiceMock.Object);

        var request = new HttpRequestMessage(HttpMethod.Post, BasePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestExt), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseObject);
        Assert.Null(responseObject.OrderChainReceipt.Reminders);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);
        orderRequestServiceMock.VerifyAll();
    }

    [Fact]
    public async Task Post_ValidRequest_WithReminders_ReturnsCreatedResponseWithReminderDetails()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientSms = new RecipientSmsExt
                {
                    PhoneNumber = "+4799999999",
                    Settings = new SmsSendingOptionsExt
                    {
                        Body = "SMS body",
                        Sender = "SMS sender name",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
                {
                    DelayDays = 2,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientSms = new RecipientSmsExt
                        {
                            PhoneNumber = "+4799999999",
                            Settings = new SmsSendingOptionsExt
                            {
                                Body = "Reminder SMS body",
                                Sender = "Reminder SMS sender name",
                                SendingTimePolicy = SendingTimePolicyExt.Daytime
                            }
                        }
                    }
                }
            ]
        };

        var expectedResponse = CreateNotificationOrderChainResponse(Guid.NewGuid(), 1);
        var client = GetTestClient(expectedResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseObject);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);
        Assert.NotNull(responseObject.OrderChainReceipt);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotNull(responseObject.OrderChainReceipt.Reminders);
        Assert.Single(responseObject.OrderChainReceipt.Reminders);
        Assert.Equal(0, responseObject.OrderChainReceipt.Reminders.Count(e => e.ShipmentId == Guid.Empty));
        Assert.Equal(0, responseObject.OrderChainReceipt.Reminders.Count(e => e.ShipmentId == expectedResponse.OrderChainId));
    }

    [Fact]
    public async Task Post_ValidRequestUsingRecipientEmail_WithoutReminders_ReturnsCreated()
    {
        // Arrange
        var requestExt = CreateValidRequest();
        var expectedResponse = CreateNotificationOrderChainResponse(Guid.NewGuid());
        var client = GetTestClient(expectedResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseObject);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);
        Assert.NotNull(responseObject.OrderChainReceipt);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);
        Assert.Null(responseObject.OrderChainReceipt.Reminders);
    }

    [Fact]
    public async Task Post_ValidRequestUsingPersonRecipient_WithoutReminders_ReturnsCreated()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    NationalIdentityNumber = "29105573746",
                    ChannelSchema = NotificationChannelExt.EmailPreferred,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Email body",
                        Subject = "Email subject",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain
                    },
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "SMS body",
                        Sender = "SMS sender name"
                    }
                }
            }
        };

        var expectedResponse = CreateNotificationOrderChainResponse(Guid.NewGuid());
        var client = GetTestClient(expectedResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseObject);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);
        Assert.NotNull(responseObject.OrderChainReceipt);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);
        Assert.Null(responseObject.OrderChainReceipt.Reminders);
    }

    [Fact]
    public async Task Post_ValidRequestUsingOrganizationRecipient_WithReminders_ReturnsCreated()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:5201C0F33A8C",
                    ChannelSchema = NotificationChannelExt.Email,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Email body",
                        Subject = "Email subject",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
                {
                    DelayDays = 3,
                    SendersReference = "BB48ABB8-D252-46BE-8BB0-6F8038F5A30A",
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientOrganization = new RecipientOrganizationExt
                        {
                            OrgNumber = "987654321",
                            ResourceId = "urn:altinn:resource:5201C0F33A8C",
                            ChannelSchema = NotificationChannelExt.Email,
                            EmailSettings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder email body",
                                Subject = "Reminder email subject",
                                SenderEmailAddress = "sender@example.com",
                                ContentType = EmailContentTypeExt.Plain
                            }
                        }
                    }
                },
                new NotificationReminderExt
                {
                    DelayDays = 7,
                    SendersReference = "0CBF6860-77BC-4D53-B7F4-926BE85A138C",
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientOrganization = new RecipientOrganizationExt
                        {
                            OrgNumber = "987654321",
                            ResourceId = "urn:altinn:resource:5201C0F33A8C",
                            ChannelSchema = NotificationChannelExt.Sms,
                            SmsSettings = new SmsSendingOptionsExt
                            {
                                Body = "SMS body",
                                Sender = "SMS sender name",
                                SendingTimePolicy = SendingTimePolicyExt.Daytime
                            }
                        }
                    }
                }
            ]
        };

        var expectedResponse = CreateNotificationOrderChainResponse(Guid.NewGuid(), 2);
        var client = GetTestClient(expectedResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseObject);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);
        Assert.NotNull(responseObject.OrderChainReceipt);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotNull(responseObject.OrderChainReceipt.Reminders);
        Assert.Equal(2, responseObject.OrderChainReceipt.Reminders.Count);
        Assert.Equal(0, responseObject.OrderChainReceipt.Reminders.Count(e => e.ShipmentId == Guid.Empty));
        Assert.Equal(0, responseObject.OrderChainReceipt.Reminders.Count(e => e.ShipmentId == expectedResponse.OrderChainId));
    }

    [Fact]
    public async Task Post_ValidRequestWithExistingOrder_ReturnsOkWithExistingOrderDetails()
    {
        // Arrange
        var request = CreateValidRequest();
        var existingResponse = CreateOrderChainResponse();
        var validatorMock = SetupValidValidator();
        var orderServiceMock = new Mock<IOrderRequestService>();

        orderServiceMock.Setup(s => s.RetrieveOrderChainTracking("ttd", request.IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingResponse);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new FutureOrdersController(orderServiceMock.Object, validatorMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Post(request);

        // Assert
        var objectResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NotificationOrderChainResponseExt>(objectResult.Value);
        Assert.Equal(existingResponse.OrderChainId, response.OrderChainId);
        orderServiceMock.Verify(
            s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_ValidRequest_FirstTimeSubmission_ReturnsCreatedWithSelfReferenceUrl()
    {
        // Arrange
        var request = CreateValidRequest();
        var newResponse = CreateOrderChainResponse();
        var expectedUrl = newResponse.OrderChainId.GetSelfLinkFromOrderChainId();
        var validatorMock = SetupValidValidator();
        var orderServiceMock = new Mock<IOrderRequestService>();

        orderServiceMock.Setup(s => s.RetrieveOrderChainTracking("ttd", request.IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderChainResponse?)null);

        orderServiceMock.Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newResponse);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new FutureOrdersController(orderServiceMock.Object, validatorMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Post(request);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(expectedUrl, createdResult.Location);
        var response = Assert.IsType<NotificationOrderChainResponseExt>(createdResult.Value);
        Assert.Equal(newResponse.OrderChainId, response.OrderChainId);
    }

    [Fact]
    public async Task Post_OperationCanceled_ReturnsClientClosedRequest()
    {
        // Arrange
        var request = CreateValidRequest();
        var validatorMock = SetupValidValidator();
        var orderServiceMock = new Mock<IOrderRequestService>();

        orderServiceMock.Setup(s => s.RetrieveOrderChainTracking(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new FutureOrdersController(orderServiceMock.Object, validatorMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Post(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(499, statusCodeResult.StatusCode);
        Assert.NotNull(statusCodeResult.Value);
        Assert.Contains("Request terminated", statusCodeResult.Value.ToString());
    }

    [Fact]
    public async Task Post_OperationCanceledDuringRegistration_Returns499Status()
    {
        // Arrange
        var request = CreateValidRequest();
        var validatorMock = SetupValidValidator();
        var orderServiceMock = new Mock<IOrderRequestService>();

        orderServiceMock.Setup(s => s.RetrieveOrderChainTracking("ttd", request.IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderChainResponse?)null);

        orderServiceMock.Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new FutureOrdersController(orderServiceMock.Object, validatorMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Post(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(499, statusCodeResult.StatusCode);
        Assert.NotNull(statusCodeResult.Value);
        Assert.Contains("Request terminated", statusCodeResult.Value.ToString());
    }

    [Fact]
    public async Task Post_RequestDtoToInternalModelMapping_PreservesAllPropertiesIncludingReminders()
    {
        // Arrange
        var request = new NotificationOrderChainRequestExt
        {
            IdempotencyId = "test-id-12345",
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            SendersReference = "sender-ref-98765",
            ConditionEndpoint = new Uri("https://example.com/check-condition"),
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "test@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test body",
                        Subject = "Test subject",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
                {
                    DelayDays = 3,
                    SendersReference = "reminder-ref-1",
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientEmail = new RecipientEmailExt
                        {
                            EmailAddress = "reminder@example.com",
                            Settings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder body",
                                Subject = "Reminder subject",
                                SenderEmailAddress = "reminder-sender@example.com",
                                ContentType = EmailContentTypeExt.Plain
                            }
                        }
                    }
                }
            ]
        };

        NotificationOrderChainRequest? capturedRequest = null;
        var validatorMock = SetupValidValidator();
        var orderServiceMock = new Mock<IOrderRequestService>();

        orderServiceMock.Setup(s => s.RetrieveOrderChainTracking("ttd", request.IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderChainResponse?)null);

        orderServiceMock.Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateOrderChainResponse());

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new FutureOrdersController(orderServiceMock.Object, validatorMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        await controller.Post(request);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("ttd", capturedRequest.Creator.ShortName);
        Assert.Equal(request.IdempotencyId, capturedRequest.IdempotencyId);
        Assert.Equal(request.SendersReference, capturedRequest.SendersReference);
        Assert.Equal(request.ConditionEndpoint, capturedRequest.ConditionEndpoint);
        Assert.Equal(request.RequestedSendTime, capturedRequest.RequestedSendTime);
        Assert.NotNull(capturedRequest.Reminders);
        Assert.Single(capturedRequest.Reminders);
        Assert.Equal(3, capturedRequest.Reminders[0].DelayDays);
        Assert.Equal("reminder-ref-1", capturedRequest.Reminders[0].SendersReference);
    }

    [Fact]
    public async Task Post_EnsuresCancellationTokenPassedToController_IsForwardedToAllServiceLayerMethods()
    {
        // Arrange
        var request = CreateValidRequest();
        var validatorMock = SetupValidValidator();
        var cancellationToken = CancellationToken.None;
        var orderServiceMock = new Mock<IOrderRequestService>();

        orderServiceMock.Setup(s => s.RetrieveOrderChainTracking(It.IsAny<string>(), It.IsAny<string>(), cancellationToken))
            .ReturnsAsync((NotificationOrderChainResponse?)null)
            .Verifiable();

        orderServiceMock.Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), cancellationToken))
            .ReturnsAsync(CreateOrderChainResponse())
            .Verifiable();

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new FutureOrdersController(orderServiceMock.Object, validatorMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        await controller.Post(request, cancellationToken);

        // Assert
        orderServiceMock.Verify(s => s.RetrieveOrderChainTracking(It.IsAny<string>(), It.IsAny<string>(), cancellationToken), Times.Once);
        orderServiceMock.Verify(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), cancellationToken), Times.Once);
    }

    /// <summary>
    /// Creates a valid notification order chain request for testing.
    /// </summary>
    /// <returns>A properly configured <see cref="NotificationOrderChainRequestExt"/> instance.</returns>
    private static NotificationOrderChainRequestExt CreateValidRequest()
    {
        return new NotificationOrderChainRequestExt
        {
            IdempotencyId = "test-id",
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "test@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test body",
                        Subject = "Test subject",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a notification order chain response for testing.
    /// </summary>
    /// <returns>A properly configured <see cref="NotificationOrderChainResponse"/> instance.</returns>
    private static NotificationOrderChainResponse CreateOrderChainResponse()
    {
        var guid = Guid.NewGuid();
        return new NotificationOrderChainResponse
        {
            OrderChainId = guid,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "test-reference"
            }
        };
    }

    /// <summary>
    /// Configures a mock validator that always returns a valid validation result.
    /// </summary>
    /// <returns>A configured mock of <see cref="IValidator{T}"/> for <see cref="NotificationOrderChainRequestExt"/>.</returns>
    private static Mock<IValidator<NotificationOrderChainRequestExt>> SetupValidValidator()
    {
        var validatorMock = new Mock<IValidator<NotificationOrderChainRequestExt>>();
        validatorMock.Setup(v => v.Validate(It.IsAny<NotificationOrderChainRequestExt>()))
            .Returns(new ValidationResult());
        return validatorMock;
    }

    /// <summary>
    /// Sends a POST request with a notification order to the API endpoint.
    /// </summary>
    /// <param name="client">The HTTP client used to send the request.</param>
    /// <param name="request">The notification order request object.</param>
    /// <returns>The HTTP response message.</returns>
    private static async Task<HttpResponseMessage> SendPostRequest(HttpClient client, NotificationOrderChainRequestExt request)
    {
        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        return await client.PostAsync(BasePath, content);
    }

    /// <summary>
    /// Deserializes the HTTP response content into a <see cref="NotificationOrderChainResponseExt"/> object.
    /// </summary>
    /// <param name="response">The HTTP response message containing JSON content.</param>
    /// <returns>A deserialized <see cref="NotificationOrderChainResponseExt"/> object, or <c>null</c> if deserialization fails.</returns>
    private async Task<NotificationOrderChainResponseExt?> DeserializeResponse(HttpResponseMessage response)
    {
        string responseString = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NotificationOrderChainResponseExt>(responseString, _options);
    }

    /// <summary>
    /// Creates a test notification order chain response with optional reminders.
    /// </summary>
    /// <param name="orderId">The unique identifier for the notification order.</param>
    /// <param name="reminderCount">The number of reminder shipments to include.</param>
    /// <param name="sendersReference">Custom sender's reference for the main notification.</param>
    /// <returns>A configured <see cref="NotificationOrderChainResponse"/> for testing.</returns>
    private static NotificationOrderChainResponse CreateNotificationOrderChainResponse(
        Guid orderId,
        int reminderCount = 0,
        string sendersReference = "notification-ref")
    {
        List<NotificationOrderChainShipment>? reminders = null;

        if (reminderCount > 0)
        {
            reminders = new List<NotificationOrderChainShipment>(reminderCount);

            for (int i = 0; i < reminderCount; i++)
            {
                reminders.Add(new NotificationOrderChainShipment
                {
                    ShipmentId = Guid.NewGuid(),
                    SendersReference = Guid.NewGuid().ToString()
                });
            }
        }

        return new NotificationOrderChainResponse
        {
            OrderChainId = orderId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                Reminders = reminders,
                ShipmentId = Guid.NewGuid(),
                SendersReference = sendersReference
            }
        };
    }

    /// <summary>
    /// Creates a test client with optional mock service and response configuration.
    /// </summary>
    /// <param name="expectedResponse">Specific response to be returned by the mock service.</param>
    /// <param name="orderRequestService">Pre-configured order request service.</param>
    /// <returns>An HTTP client configured for testing.</returns>
    private HttpClient GetTestClient(
        NotificationOrderChainResponse? expectedResponse = null,
        IOrderRequestService? orderRequestService = null)
    {
        if (orderRequestService == null)
        {
            var response = expectedResponse ?? new NotificationOrderChainResponse
            {
                OrderChainId = Guid.NewGuid(),
                OrderChainReceipt = new NotificationOrderChainReceipt
                {
                    ShipmentId = Guid.NewGuid(),
                    SendersReference = "senders-reference-628BE69FE9ED"
                }
            };

            var orderRequestServiceMock = new Mock<IOrderRequestService>();
            orderRequestServiceMock
                .Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            orderRequestService = orderRequestServiceMock.Object;
        }

        return _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(orderRequestService);
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();
    }
}
