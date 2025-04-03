using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TestingControllers;

public class FutureOrdersControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<FutureOrdersController>>
{
    private const string _basePath = "/notifications/api/v1/future/orders";

    private readonly JsonSerializerOptions _options;
    private readonly IntegrationTestWebApplicationFactory<FutureOrdersController> _factory;

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
    public async Task Post_CalledByUser_And_ValidRequest_ReturnsForbidden()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),

            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test email body",
                        Subject = "Test email subject",
                        SenderName = "Test sender name",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));

        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestExt), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_InvalidScope_And_ValidRequest_ReturnsForbidden()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),

            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test email body",
                        Subject = "Test email subject",
                        SenderName = "Test sender name",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "dummy:scope"));

        // Act
        HttpResponseMessage response = await SendPostRequest(client, requestExt);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidBearerToken_And_ValidRequest_ReturnAccepted()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test email body",
                        Subject = "Test email subject",
                        SenderName = "Test sender name",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

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
            .Setup(s => s.RegisterNotificationOrderChain(It.Is<NotificationOrderChainRequest>(e => e.IdempotencyId == requestExt.IdempotencyId && e.RequestedSendTime == requestExt.RequestedSendTime), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        HttpClient client = GetTestClient(orderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        Assert.NotNull(responseObject);

        Assert.Null(responseObject.OrderChainReceipt.Reminders);

        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);

        orderRequestServiceMock.VerifyAll();
    }

    [Fact]
    public async Task Post_ValidPlatformAccessToken_And_ValidRequest_ReturnAccepted()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test email body",
                        Subject = "Test email subject",
                        SenderName = "Test sender name",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

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
            .Setup(s => s.RegisterNotificationOrderChain(It.Is<NotificationOrderChainRequest>(e => e.IdempotencyId == requestExt.IdempotencyId && e.RequestedSendTime == requestExt.RequestedSendTime), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        HttpClient client = GetTestClient(orderRequestService: orderRequestServiceMock.Object);

        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestExt), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        Assert.NotNull(responseObject);
        Assert.Null(responseObject.OrderChainReceipt.Reminders);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);

        orderRequestServiceMock.VerifyAll();
    }

    [Fact]
    public async Task Post_InvalidRequest_MissingRecipientInfo_BadRequest()
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
    public async Task Post_MissingBearer_And_ValidRequest_ReturnsUnauthorized()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test email body",
                        Subject = "Test email subject",
                        SenderName = "Test sender name",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

        HttpClient client = GetTestClient();

        // Act
        HttpResponseMessage response = await SendPostRequest(client, requestExt);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidSmsRecipient_With_Reminders_ReturnsAccepted()
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

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
    public async Task Post_InvalidRequest_ValidEmailRecipient_And_NoReminders_ReturnsAccepted()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),

            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test email body",
                        Subject = "Test email subject",
                        SenderName = "Test sender name",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

        var expectedResponse = CreateNotificationOrderChainResponse(Guid.NewGuid());
        var client = GetTestClient(expectedResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        Assert.NotNull(responseObject);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);

        Assert.NotNull(responseObject.OrderChainReceipt);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);

        Assert.Null(responseObject.OrderChainReceipt.Reminders);
    }

    [Fact]
    public async Task Post_InvalidRequest_ValidPersonRecipient_And_NoReminders_ReturnsAccepted()
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
                        SenderName = "Email sender name",
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        Assert.NotNull(responseObject);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);

        Assert.NotNull(responseObject.OrderChainReceipt);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(expectedResponse.OrderChainId, responseObject.OrderChainReceipt.ShipmentId);

        Assert.Null(responseObject.OrderChainReceipt.Reminders);
    }

    [Fact]
    public async Task Post_InvalidRequest_ValidOrganizationRecipient_And_MultipleReminders_ReturnsAccepted()
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
                        SenderName = "Email sender name",
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
                                SenderName = "Reminder email sender name",
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        // Act
        var response = await SendPostRequest(client, requestExt);
        var responseObject = await DeserializeResponse(response);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

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
    public async Task Post_InvalidRequest_RequestedSendTimeInPast_ReturnsBadRequest()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(-2),
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test email body",
                        Subject = "Test email subject",
                        SenderName = "Test sender name",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestExt), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

        Assert.NotNull(problem);
        Assert.Equal("One or more validation errors occurred.", problem.Title);

        Assert.Contains("RequestedSendTime", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_InvalidRequest_MissingCreatorInfo_ReturnsForbidden()
    {
        // Arrange
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test email body",
                        Subject = "Test email subject",
                        SenderName = "Test sender name",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

        var orderRequestServiceMock = new Mock<IOrderRequestService>();
        var validatorMock = new Mock<IValidator<NotificationOrderChainRequestExt>>();
        validatorMock.Setup(v => v.Validate(It.IsAny<NotificationOrderChainRequestExt>())).Returns(new FluentValidation.Results.ValidationResult());

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(e => e.Items).Returns(new Dictionary<object, object?> { { "Org", null } });

        var controllerContext = new ControllerContext
        {
            HttpContext = httpContextMock.Object
        };

        var controller = new FutureOrdersController(orderRequestServiceMock.Object, validatorMock.Object)
        {
            ControllerContext = controllerContext
        };

        // Act
        var result = await controller.Post(requestExt);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        orderRequestServiceMock.Verify(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Sends a POST request with a notification order to the specified API endpoint.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> instance used to send the request.</param>
    /// <param name="request">The notification order request object.</param>
    /// <returns>A task representing the asynchronous operation, returning the HTTP response message.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    private static async Task<HttpResponseMessage> SendPostRequest(HttpClient client, NotificationOrderChainRequestExt request)
    {
        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        return await client.PostAsync(_basePath, content);
    }

    /// <summary>
    /// Deserializes the HTTP response content into a <see cref="NotificationOrderChainResponseExt"/> object.
    /// </summary>
    /// <param name="response">The HTTP response message containing JSON content.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning a deserialized 
    /// <see cref="NotificationOrderChainResponseExt"/> object, or <c>null</c> if deserialization fails.
    /// </returns>
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
    /// <param name="sendersReference">Optional custom sender's reference for the main notification.</param>
    /// <returns>A configured <see cref="NotificationOrderChainResponse"/> for testing.</returns>
    private static NotificationOrderChainResponse CreateNotificationOrderChainResponse(Guid orderId, int reminderCount = 0, string sendersReference = "notification-ref")
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
    /// <param name="expectedResponse">Optional specific response to be returned by the mock service.</param>
    /// <param name="orderRequestService">Optional pre-configured order request service.</param>
    /// <returns>An HTTP client configured for testing.</returns>
    private HttpClient GetTestClient(NotificationOrderChainResponse? expectedResponse = null, IOrderRequestService? orderRequestService = null)
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
