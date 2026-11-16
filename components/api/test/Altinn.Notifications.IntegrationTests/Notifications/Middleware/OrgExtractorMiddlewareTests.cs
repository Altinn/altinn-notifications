using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Files;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Tests.Notifications.Utils;

using Microsoft.IdentityModel.Tokens;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Middleware;

/// <summary>
/// Integration tests verifying which service owner (org) the OrgExtractorMiddleware resolves for a request.
/// </summary>
public class OrgExtractorMiddlewareTests : IClassFixture<IntegrationTestWebApplicationFactory<Program>>
{
    private const string _bearerOrg = "skd";
    private const string _accessTokenIssuer = "ttd";
    private const string _createScope = "altinn:serviceowner/notifications.create";
    private const string _composedEmailScope = "altinn:serviceowner/notifications.composedemail.create";
    private const string _shipmentPath = "/notifications/api/v1/future/shipment";
    private const string _composedEmailPath = "/notifications/api/v1/future/orders/composed-email";

    private readonly IntegrationTestWebApplicationFactory<Program> _factory;

    public OrgExtractorMiddlewareTests(IntegrationTestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public static TheoryData<string> InvalidAccessTokens => new()
    {
        { "unsigned" },
        { "wrong-signing-key" },
        { "unknown-issuer" },
        { "expired" },
        { "malformed" }
    };

    public static TheoryData<string> InvalidBearerTokens => new()
    {
        { "unsigned" },
        { "wrong-signing-key" },
        { "expired" }
    };

    [Theory]
    [MemberData(nameof(InvalidBearerTokens))]
    public async Task Shipment_InvalidBearerTokenWithOrgAndScope_ReturnsUnauthorizedAndServiceNotCalled(string bearerTokenKind)
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateInvalidBearerToken(bearerTokenKind, _createScope));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(capturedCreators);
    }

    [Theory]
    [MemberData(nameof(InvalidBearerTokens))]
    public async Task ComposedEmail_InvalidBearerTokenWithOrgAndScope_ReturnsUnauthorizedAndServiceNotCalled(string bearerTokenKind)
    {
        // Arrange
        var (client, capturedCreators) = GetComposedEmailClient();
        HttpRequestMessage request = CreateComposedEmailRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateInvalidBearerToken(bearerTokenKind, _composedEmailScope));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(capturedCreators);
    }

    [Theory]
    [MemberData(nameof(InvalidAccessTokens))]
    public async Task Shipment_ScopeAuthorizedWithInvalidAccessToken_UsesOrgFromBearerToken(string accessTokenKind)
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(_bearerOrg, scope: _createScope));
        request.Headers.Add("PlatformAccessToken", CreateInvalidAccessToken(accessTokenKind));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([_bearerOrg], capturedCreators);
    }

    [Theory]
    [MemberData(nameof(InvalidAccessTokens))]
    public async Task Shipment_OnlyInvalidAccessToken_ReturnsUnauthorizedAndServiceNotCalled(string accessTokenKind)
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Add("PlatformAccessToken", CreateInvalidAccessToken(accessTokenKind));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(capturedCreators);
    }

    [Fact]
    public async Task Shipment_ValidAccessTokenOnly_UsesIssuerOfAccessToken()
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken(_accessTokenIssuer, "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([_accessTokenIssuer], capturedCreators);
    }

    [Fact]
    public async Task Shipment_ValidAccessTokenAndUserBearerTokenWithoutOrg_UsesIssuerOfAccessToken()
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken(_accessTokenIssuer, "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([_accessTokenIssuer], capturedCreators);
    }

    [Fact]
    public async Task Shipment_ValidAccessTokenAndBearerTokenForSameOrg_UsesThatOrg()
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(_accessTokenIssuer, scope: _createScope));
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken(_accessTokenIssuer, "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([_accessTokenIssuer], capturedCreators);
    }

    [Theory]
    [InlineData(_createScope)]
    [InlineData("dummy:scope")]
    public async Task Shipment_ValidAccessTokenAndBearerTokenForDifferentOrg_ReturnsForbiddenAndServiceNotCalled(string bearerScope)
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(_bearerOrg, scope: bearerScope));
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken(_accessTokenIssuer, "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(capturedCreators);
    }

    [Fact]
    public async Task Shipment_BearerTokenOnly_UsesOrgFromBearerToken()
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(_bearerOrg, scope: _createScope));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([_bearerOrg], capturedCreators);
    }

    [Theory]
    [MemberData(nameof(InvalidBearerTokens))]
    public async Task Shipment_InvalidBearerTokenWithValidAccessToken_IgnoresBearerAndUsesIssuerOfAccessToken(string bearerTokenKind)
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateInvalidBearerToken(bearerTokenKind, _createScope, _bearerOrg));
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken(_accessTokenIssuer, "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([_accessTokenIssuer], capturedCreators);
    }

    [Theory]
    [MemberData(nameof(InvalidBearerTokens))]
    public async Task ComposedEmail_InvalidBearerTokenWithValidAccessToken_ReturnsUnauthorizedAndServiceNotCalled(string bearerTokenKind)
    {
        // Arrange
        var (client, capturedCreators) = GetComposedEmailClient();
        HttpRequestMessage request = CreateComposedEmailRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateInvalidBearerToken(bearerTokenKind, _composedEmailScope, _bearerOrg));
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken(_accessTokenIssuer, "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(capturedCreators);
    }

    [Theory]
    [MemberData(nameof(InvalidAccessTokens))]
    public async Task Shipment_UserTokenWithInvalidAccessToken_ReturnsForbiddenAndServiceNotCalled(string accessTokenKind)
    {
        // Arrange
        var (client, capturedCreators) = GetShipmentClient();
        HttpRequestMessage request = CreateShipmentRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));
        request.Headers.Add("PlatformAccessToken", CreateInvalidAccessToken(accessTokenKind));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(capturedCreators);
    }

    [Theory]
    [MemberData(nameof(InvalidAccessTokens))]
    public async Task ComposedEmail_ScopeOnlyPolicyWithInvalidAccessToken_UsesOrgFromBearerToken(string accessTokenKind)
    {
        // Arrange
        var (client, capturedCreators) = GetComposedEmailClient();
        HttpRequestMessage request = CreateComposedEmailRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(_bearerOrg, scope: _composedEmailScope));
        request.Headers.Add("PlatformAccessToken", CreateInvalidAccessToken(accessTokenKind));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([_bearerOrg], capturedCreators);
    }

    [Fact]
    public async Task ComposedEmail_ScopeOnlyPolicyWithValidAccessToken_UsesOrgFromBearerToken()
    {
        // Arrange
        var (client, capturedCreators) = GetComposedEmailClient();
        HttpRequestMessage request = CreateComposedEmailRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(_bearerOrg, scope: _composedEmailScope));
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken(_accessTokenIssuer, "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([_bearerOrg], capturedCreators);
    }

    private (HttpClient Client, List<string> CapturedCreators) GetShipmentClient()
    {
        List<string> capturedCreators = [];

        var serviceMock = new Mock<INotificationDeliveryManifestService>();
        serviceMock
            .Setup(s => s.GetDeliveryManifestAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, CancellationToken>((_, creator, _) => capturedCreators.Add(creator))
            .ReturnsAsync((Guid shipmentId, string _, CancellationToken _) => CreateDeliveryManifest(shipmentId));

        _factory.ResetInstalledMocks();
        _factory.InstallService(serviceMock.Object);

        return (_factory.SharedClient, capturedCreators);
    }

    private (HttpClient Client, List<string> CapturedCreators) GetComposedEmailClient()
    {
        List<string> capturedCreators = [];

        var serviceMock = new Mock<IComposedEmailOrderRequestService>();
        serviceMock
            .Setup(s => s.RegisterComposedEmailOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, CancellationToken>((orderRequest, _) => capturedCreators.Add(orderRequest.Creator.ShortName))
            .ReturnsAsync(new NotificationOrderChainResponse
            {
                IsNewlyCreated = true,
                OrderChainId = Guid.NewGuid(),
                OrderChainReceipt = new NotificationOrderChainReceipt
                {
                    ShipmentId = Guid.NewGuid(),
                    SendersReference = "ref-org-extractor"
                }
            });

        _factory.ResetInstalledMocks();
        _factory.InstallService(serviceMock.Object);

        return (_factory.SharedClient, capturedCreators);
    }

    private static HttpRequestMessage CreateShipmentRequest()
    {
        return new HttpRequestMessage(HttpMethod.Get, $"{_shipmentPath}/{Guid.NewGuid()}");
    }

    private static HttpRequestMessage CreateComposedEmailRequest()
    {
        var orderRequest = new ComposedEmailRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            SendersReference = "ref-org-extractor",
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new RecipientComposedEmailExt
            {
                EmailAddress = "recipient@altinnxyz.no",
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
                            SasUrl = new Uri("https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature")
                        }
                    ]
                }
            }
        };

        return new HttpRequestMessage(HttpMethod.Post, _composedEmailPath)
        {
            Content = new StringContent(JsonSerializer.Serialize(orderRequest), Encoding.UTF8, "application/json")
        };
    }

    private static Result<INotificationDeliveryManifest> CreateDeliveryManifest(Guid shipmentId)
    {
        return new NotificationDeliveryManifest
        {
            Type = "Notification",
            ShipmentId = shipmentId,
            LastUpdate = DateTime.UtcNow,
            Status = ProcessingLifecycle.Order_Completed,
            Recipients = ImmutableList<IDeliveryManifest>.Empty
        };
    }

    /// <summary>
    /// Creates a platform access token that must not be accepted by the AccessTokenHandler.
    /// </summary>
    private static string CreateInvalidAccessToken(string kind)
    {
        return kind switch
        {
            "unsigned" => CreateUnsignedToken(_accessTokenIssuer),
            "wrong-signing-key" => CreateSignedToken(_accessTokenIssuer, GetUnitTestSigningCredentials(), DateTime.UtcNow.AddMinutes(5)),
            "unknown-issuer" => CreateSignedToken("unknown-org", GetUnitTestSigningCredentials(), DateTime.UtcNow.AddMinutes(5)),
            "expired" => CreateSignedToken(_accessTokenIssuer, GetTtdSigningCredentials(), DateTime.UtcNow.AddMinutes(-5)),
            "malformed" => "not-a-jwt",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    /// <summary>
    /// Creates a bearer token carrying a service owner org claim and the given scope that must not be accepted
    /// by the JwtCookie authentication handler.
    /// </summary>
    private static string CreateInvalidBearerToken(string kind, string scope, string org = _accessTokenIssuer)
    {
        Claim[] claims =
        [
            new Claim("urn:altinn:org", org),
            new Claim("urn:altinn:orgNumber", "991825827"),
            new Claim("urn:altinn:scope", scope)
        ];

        return kind switch
        {
            "unsigned" => CreateUnsignedBearerToken(scope, org),
            "wrong-signing-key" => CreateSignedToken("UnitTest", GetTtdSigningCredentials(), DateTime.UtcNow.AddMinutes(5), claims),
            "expired" => CreateSignedToken("UnitTest", GetUnitTestSigningCredentials(), DateTime.UtcNow.AddMinutes(-5), claims),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static string CreateUnsignedBearerToken(string scope, string org)
    {
        long expires = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        string header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        string payload = Base64UrlEncoder.Encode($$"""{"iss":"UnitTest","urn:altinn:org":"{{org}}","urn:altinn:orgNumber":"991825827","urn:altinn:scope":"{{scope}}","exp":{{expires}}}""");
        return $"{header}.{payload}.";
    }

    private static string CreateUnsignedToken(string issuer)
    {
        long expires = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        string header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        string payload = Base64UrlEncoder.Encode($$"""{"iss":"{{issuer}}","urn:altinn:app":"apps-test","exp":{{expires}}}""");
        return $"{header}.{payload}.";
    }

    private static string CreateSignedToken(string issuer, SigningCredentials signingCredentials, DateTime expires, Claim[]? claims = null)
    {
        JwtSecurityTokenHandler tokenHandler = new();
        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Subject = new ClaimsIdentity(claims ?? [new Claim("urn:altinn:app", "apps-test")]),
            NotBefore = expires.AddMinutes(-10),
            IssuedAt = expires.AddMinutes(-10),
            Expires = expires,
            SigningCredentials = signingCredentials,
            Audience = "altinn.no",
            Issuer = issuer
        };

        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }

    private static X509SigningCredentials GetUnitTestSigningCredentials()
    {
        X509Certificate2 cert = X509CertificateLoader.LoadPkcs12FromFile("jwtselfsignedcert.pfx", "qwer1234");
        return new X509SigningCredentials(cert, SecurityAlgorithms.RsaSha256);
    }

    private static X509SigningCredentials GetTtdSigningCredentials()
    {
        X509Certificate2 cert = X509CertificateLoader.LoadPkcs12FromFile("ttd-org.pfx", null);
        return new X509SigningCredentials(cert, SecurityAlgorithms.RsaSha256);
    }
}
