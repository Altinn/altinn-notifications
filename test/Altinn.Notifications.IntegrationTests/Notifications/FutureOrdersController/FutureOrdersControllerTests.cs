using System;
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

    private readonly NotificationOrderChainResponse _notificationOrderChainResponse;
    private readonly NotificationOrderChainRequestExt _notificationOrderChainRequestExt;

    private readonly JsonSerializerOptions _options;
    private readonly Guid _orderChainId = Guid.NewGuid();
    private readonly IntegrationTestWebApplicationFactory<FutureOrdersController> _factory;

    public FutureOrdersControllerTests(IntegrationTestWebApplicationFactory<FutureOrdersController> factory)
    {
        _factory = factory;

        _notificationOrderChainResponse = new NotificationOrderChainResponse
        {
            Id = _orderChainId,
            CreationResult = new NotificationOrderChainReceipt
            {
                ShipmentId = _orderChainId,
                SendersReference = "4567890A-BCDE-F123-4567-89ABCDEF1234",
                Reminders =
                [
                    new NotificationOrderChainShipment
                    {
                        ShipmentId = Guid.NewGuid(),
                        SendersReference = "5C6D7E8F-9A0B-1C2D-3E4F-5A6B7C8D9E0F"
                    },
                    new NotificationOrderChainShipment
                    {
                        ShipmentId = Guid.NewGuid(),
                        SendersReference = "1C2D3E4F-5A6B-7C8D-9E0F-1A2B3C4D5E6F"
                    }
                ]
            }
        };

        _notificationOrderChainRequestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            IdempotencyId = "7A8B9C0D-1E2F-3A4B-5C6D-7E8F9A0B1C2D",
            SendersReference = "4567890A-BCDE-F123-4567-89ABCDEF1234",
            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:5678",
                    ChannelSchema = NotificationChannelExt.EmailPreferred,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Organization email body",
                        Subject = "Organization email subject",
                        SenderName = "Organization email sender",
                        SenderEmailAddress = "org-sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    },
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "Organization SMS body",
                        Sender = "Organization SMS sender",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
                {
                    DelayDays = 3,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientOrganization = new RecipientOrganizationExt
                        {
                            OrgNumber = "987654321",
                            ResourceId = "urn:altinn:resource:5678",
                            ChannelSchema = NotificationChannelExt.EmailPreferred,
                            EmailSettings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 1 org email body",
                                Subject = "Reminder 1 org email subject",
                                SenderName = "Reminder 1 org email sender",
                                SenderEmailAddress = "reminder-org-sender@example.com",
                                ContentType = EmailContentTypeExt.Html,
                                SendingTimePolicy = SendingTimePolicyExt.Anytime
                            },
                            SmsSettings = new SmsSendingOptionsExt
                            {
                                Body = "Reminder 1 org SMS body",
                                Sender = "Reminder 1 org SMS sender",
                                SendingTimePolicy = SendingTimePolicyExt.Daytime
                            }
                        }
                    },
                    SendersReference = "5C6D7E8F-9A0B-1C2D-3E4F-5A6B7C8D9E0F",
                    ConditionEndpoint = new Uri("https://vg.no/first-org-reminder-condition")
                },
                new NotificationReminderExt
                {
                    DelayDays = 7,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientOrganization = new RecipientOrganizationExt
                        {
                            OrgNumber = "987654321",
                            ResourceId = "urn:altinn:resource:5678",
                            ChannelSchema = NotificationChannelExt.Email,
                            EmailSettings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 2 org email body",
                                Subject = "Reminder 2 org email subject",
                                SenderName = "Reminder 2 org email sender",
                                SenderEmailAddress = "reminder2-org-sender@example.com",
                                ContentType = EmailContentTypeExt.Plain,
                                SendingTimePolicy = SendingTimePolicyExt.Anytime
                            }
                        }
                    },
                    SendersReference = "1C2D3E4F-5A6B-7C8D-9E0F-1A2B3C4D5E6F",
                    ConditionEndpoint = new Uri("https://vg.no/second-org-reminder-condition")
                }
            ]
        };

        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task Post_CalledByUser_ReturnsForbidden()
    {
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));

        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(_notificationOrderChainRequestExt), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_MissingBearer_ReturnsUnauthorized()
    {
        HttpClient client = GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(_notificationOrderChainRequestExt), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_CalledWithInvalidScope_ReturnsForbidden()
    {
        HttpClient client = GetTestClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "dummy:scope"));

        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(_notificationOrderChainRequestExt), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_CalledWithValidBearerToken_ReturnAccepted()
    {
        var orderRequestServiceMock = new Mock<IOrderRequestService>();
        orderRequestServiceMock
            .Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>()))
            .ReturnsAsync(_notificationOrderChainResponse);

        HttpClient client = GetTestClient(orderRequestService: orderRequestServiceMock.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(_notificationOrderChainRequestExt), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await client.SendAsync(request);
        string responseString = await response.Content.ReadAsStringAsync();
        var responseObject = JsonSerializer.Deserialize<NotificationOrderChainResponseExt>(responseString, _options);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(_orderChainId, responseObject!.Id);
        orderRequestServiceMock.VerifyAll();
    }

    [Fact]
    public async Task Post_CalledWithValidPlatformAccessToken_ReturnAccepted()
    {
        var orderRequestServiceMock = new Mock<IOrderRequestService>();
        orderRequestServiceMock
            .Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>()))
            .ReturnsAsync(_notificationOrderChainResponse);

        HttpClient client = GetTestClient(orderRequestService: orderRequestServiceMock.Object);

        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(_notificationOrderChainRequestExt), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        HttpResponseMessage response = await client.SendAsync(request);
        string responseString = await response.Content.ReadAsStringAsync();
        var responseObject = JsonSerializer.Deserialize<NotificationOrderChainResponseExt>(responseString, _options);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(_orderChainId, responseObject!.Id);
        orderRequestServiceMock.VerifyAll();
    }

    [Fact]
    public async Task Post_CalledWithInvalidNotificationOrderChainRequest_BadRequest()
    {
        var invalidRequest = new NotificationOrderChainRequestExt
        {
            Recipient = null!,
            IdempotencyId = "1B2C3D4E-5F6G-7H8I-9J0K-1L2M3N4O5P6Q",
            RequestedSendTime = _notificationOrderChainRequestExt.RequestedSendTime,
        };

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(invalidRequest), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();
        ProblemDetails? problem = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("One or more validation errors occurred.", problem?.Title);
    }

    private HttpClient GetTestClient(IOrderRequestService? orderRequestService = null)
    {
        if (orderRequestService == null)
        {
            var orderRequestServiceMock = new Mock<IOrderRequestService>();
            orderRequestServiceMock
                .Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>()))
                .ReturnsAsync(_notificationOrderChainResponse);
            orderRequestService = orderRequestServiceMock.Object;
        }

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(orderRequestService);

                // Set up mock authentication and authorization.
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
            });
        }).CreateClient();

        return client;
    }
}
