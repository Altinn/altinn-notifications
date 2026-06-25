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
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Files;
using Altinn.Notifications.Models.Recipient;
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

/// <summary>
/// Integration tests for the <see cref="NotificationOrderWithAttachmentsController"/>.
/// </summary>
public class NotificationOrderWithAttachmentsControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<NotificationOrderWithAttachmentsController>>
{
    private const string BasePath = "/notifications/api/v1/future/orders/email-with-attachments";
    private const string ValidScope = "altinn:serviceowner/notifications.emailwithattachments.create";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string _validSasUrl =
        "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
        "?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature";

    private readonly IntegrationTestWebApplicationFactory<NotificationOrderWithAttachmentsController> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderWithAttachmentsControllerTests"/> class.
    /// </summary>
    public NotificationOrderWithAttachmentsControllerTests(IntegrationTestWebApplicationFactory<NotificationOrderWithAttachmentsController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_MissingBearer_ReturnsUnauthorized()
    {
        var client = GetTestClient();

        var response = await SendPostRequest(client, ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_OrgTokenWithInvalidScope_ReturnsForbidden()
    {
        var client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "dummy:scope"));

        var response = await SendPostRequest(client, ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_UserToken_ReturnsForbidden()
    {
        var client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PrincipalUtil.GetUserToken(1337));

        var response = await SendPostRequest(client, ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_InvalidRequest_MissingIdempotencyId_ReturnsBadRequest()
    {
        var request = new NotificationOrderWithAttachmentsRequestExt
        {
            IdempotencyId = string.Empty,
            SendersReference = "ref-attach-001",
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new RecipientEmailWithAttachmentsExt
            {
                EmailAddress = "recipient@agency.no",
                Settings = new ComposedEmailSendingOptionsExt
                {
                    Subject = "Decision from Altinn",
                    Body = "Please see the attached document.",
                    Attachments =
                    [
                        new SasFileReferenceExt
                        {
                            Filename = "contract.pdf",
                            MimeType = "application/pdf",
                            SasUrl = _validSasUrl
                        }
                    ]
                }
            }
        };

        var client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PrincipalUtil.GetOrgToken("ttd", scope: ValidScope));

        var response = await SendPostRequest(client, request);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Contains("IdempotencyId", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_InvalidRequest_SasUrlNotHttps_ReturnsBadRequest()
    {
        var request = new NotificationOrderWithAttachmentsRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            SendersReference = "ref-attach-001",
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new RecipientEmailWithAttachmentsExt
            {
                EmailAddress = "recipient@agency.no",
                Settings = new ComposedEmailSendingOptionsExt
                {
                    Subject = "Decision from Altinn",
                    Body = "Please see the attached document.",
                    Attachments =
                    [
                        new SasFileReferenceExt
                        {
                            Filename = "contract.pdf",
                            MimeType = "application/pdf",
                            SasUrl = "http://not-https.example.com/file.pdf"
                        }
                    ]
                }
            }
        };

        var client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PrincipalUtil.GetOrgToken("ttd", scope: ValidScope));

        var response = await SendPostRequest(client, request);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("HTTPS", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_InvalidRequest_SasExpiryTooCloseToSendTime_ReturnsBadRequest()
    {
        var sendTime = DateTime.UtcNow.AddHours(2);
        var expiryTooSoon = sendTime.AddMinutes(10).ToString("o");

        var request = new NotificationOrderWithAttachmentsRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = sendTime,
            Recipient = new RecipientEmailWithAttachmentsExt
            {
                EmailAddress = "recipient@agency.no",
                Settings = new ComposedEmailSendingOptionsExt
                {
                    Subject = "Decision from Altinn",
                    Body = "Please see the attached document.",
                    Attachments =
                    [
                        new SasFileReferenceExt
                        {
                            Filename = "contract.pdf",
                            MimeType = "application/pdf",
                            SasUrl = $"https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf?se={Uri.EscapeDataString(expiryTooSoon)}&sig=x"
                        }
                    ]
                }
            }
        };

        var client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PrincipalUtil.GetOrgToken("ttd", scope: ValidScope));

        var response = await SendPostRequest(client, request);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("sasUrl must be valid for at least 15 minutes", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_OrgTokenWithValidScope_ReturnsCreated()
    {
        var request = ValidRequest();
        var expectedResponse = CreateOrderChainResponse();

        var client = GetTestClient(expectedResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PrincipalUtil.GetOrgToken("ttd", scope: ValidScope));

        var response = await SendPostRequest(client, request);
        var responseObject = await DeserializeResponse(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseObject);
        Assert.Equal(expectedResponse.OrderChainId, responseObject.OrderChainId);
        Assert.NotEqual(Guid.Empty, responseObject.OrderChainReceipt.ShipmentId);
    }

    [Fact]
    public async Task Post_ExistingOrder_ReturnsOkWithExistingDetails()
    {
        var request = ValidRequest();
        var existingResponse = CreateOrderChainResponse();

        var serviceMock = new Mock<IOrderRequestService>();
        serviceMock.Setup(s => s.RetrieveOrderChainTracking("ttd", request.IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingResponse);

        var client = GetTestClient(orderRequestService: serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PrincipalUtil.GetOrgToken("ttd", scope: ValidScope));

        var response = await SendPostRequest(client, request);
        var responseObject = await DeserializeResponse(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseObject);
        Assert.Equal(existingResponse.OrderChainId, responseObject.OrderChainId);
        serviceMock.Verify(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static NotificationOrderWithAttachmentsRequestExt ValidRequest() => new()
    {
        IdempotencyId = Guid.NewGuid().ToString(),
        SendersReference = "ref-attach-001",
        RequestedSendTime = DateTime.UtcNow.AddHours(2),
        Recipient = new RecipientEmailWithAttachmentsExt
        {
            EmailAddress = "recipient@agency.no",
            Settings = new ComposedEmailSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments =
                [
                    new SasFileReferenceExt
                    {
                        Filename = "contract.pdf",
                        MimeType = "application/pdf",
                        SasUrl = _validSasUrl
                    }
                ]
            }
        }
    };

    private static NotificationOrderChainResponse CreateOrderChainResponse() => new()
    {
        OrderChainId = Guid.NewGuid(),
        OrderChainReceipt = new NotificationOrderChainReceipt
        {
            ShipmentId = Guid.NewGuid(),
            SendersReference = "ref-attach-001"
        }
    };

    private static async Task<HttpResponseMessage> SendPostRequest(HttpClient client, NotificationOrderWithAttachmentsRequestExt request)
    {
        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        return await client.PostAsync(BasePath, content, TestContext.Current.CancellationToken);
    }

    private static async Task<NotificationOrderChainResponseExt?> DeserializeResponse(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonSerializer.Deserialize<NotificationOrderChainResponseExt>(body, _options);
    }

    private HttpClient GetTestClient(NotificationOrderChainResponse? expectedResponse = null, IOrderRequestService? orderRequestService = null)
    {
        if (orderRequestService == null)
        {
            var response = expectedResponse ?? CreateOrderChainResponse();
            var mock = new Mock<IOrderRequestService>();
            mock.Setup(s => s.RetrieveOrderChainTracking(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NotificationOrderChainResponse?)null);
            mock.Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
            orderRequestService = mock.Object;
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
