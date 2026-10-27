using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Notifications.Core.Models.Parties;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Register;
using Altinn.Notifications.Integrations.Register.Models;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Register;

public class RegisterClientTests
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RegisterClient _registerClient;

    public RegisterClientTests()
    {
        _registerClient = CreateRegisterClient();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.PartialContent)]
    public async Task QueryPartyDetails_WithMatchingParties_ReturnsPartyDetails(HttpStatusCode statusCode)
    {
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request!.Method);
            Assert.Equal("/register/api/v1/notifications/parties/query", request.RequestUri!.AbsolutePath);
            Assert.Equal("valid-token", Assert.Single(request.Headers.GetValues("PlatformAccessToken")));

            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var data = body.RootElement.GetProperty("data");
            Assert.Equal(2, data.GetArrayLength());
            Assert.Equal("urn:altinn:organization:identifier-no:123456785", data[0].GetString());
            Assert.Equal("urn:altinn:person:identifier-no:25871999336", data[1].GetString());

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    """
                    {
                        "data": [
                            {
                                "partyType": "person",
                                "personIdentifier": "25871999336",
                                "partyUuid": "00000000-0000-0000-0000-000000000001",
                                "versionId": 1,
                                "displayName": "Test Person"
                            },
                            {
                                "partyType": "organization",
                                "organizationIdentifier": "123456785",
                                "partyUuid": "00000000-0000-0000-0000-000000000002",
                                "versionId": 2,
                                "displayName": "Test Organization"
                            }
                        ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }));

        var actual = await registerClient.QueryPartyDetails(["123456785", "123456785"], ["25871999336"], TestContext.Current.CancellationToken);

        Assert.Equal(2, actual.Count);
        var person = Assert.Single(actual, e => e.NationalIdentityNumber == "25871999336");
        Assert.Equal("Test Person", person.Name);
        Assert.Null(person.OrganizationNumber);
        var organization = Assert.Single(actual, e => e.OrganizationNumber == "123456785");
        Assert.Equal("Test Organization", organization.Name);
        Assert.Null(organization.NationalIdentityNumber);
    }

    [Fact]
    public async Task QueryPartyDetails_WithOnlyInvalidIdentifiers_DoesNotSendRequest()
    {
        var requestSent = false;
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
        {
            requestSent = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }));

        var actual = await registerClient.QueryPartyDetails(["123"], ["456"], TestContext.Current.CancellationToken);

        Assert.Empty(actual);
        Assert.False(requestSent);
    }

    [Fact]
    public async Task QueryPartyDetails_WithMoreThan100Identifiers_SendsBatchesOfAtMost100()
    {
        var batchSizes = new List<int>();
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub(async (request, token) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            batchSizes.Add(body.RootElement.GetProperty("data").GetArrayLength());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[]}""", Encoding.UTF8, "application/json")
            };
        }));

        var organizationNumbers = Enumerable.Range(10000000, 400)
            .Select(prefix => (Prefix: prefix.ToString(), Digit: OrganizationChecksumDigit(prefix.ToString())))
            .Where(e => e.Digit.HasValue)
            .Select(e => e.Prefix + e.Digit)
            .Take(150)
            .ToList();

        await registerClient.QueryPartyDetails(organizationNumbers, [], TestContext.Current.CancellationToken);

        Assert.Equal([100, 50], batchSizes);
    }

    private static int? OrganizationChecksumDigit(string prefix)
    {
        int[] weights = [3, 2, 7, 6, 5, 4, 3, 2];
        var sum = prefix.Select((c, i) => (c - '0') * weights[i]).Sum();
        var digit = 11 - (sum % 11);

        return digit switch
        {
            11 => 0,
            10 => null,
            _ => digit
        };
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.PartialContent)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task QueryPartyDetails_WithNoMatches_ReturnsEmpty(HttpStatusCode statusCode)
    {
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = statusCode == HttpStatusCode.NoContent
                    ? null
                    : new StringContent("""{"data":[]}""", Encoding.UTF8, "application/json")
            })));

        var actual = await registerClient.QueryPartyDetails(["123456785"], [], TestContext.Current.CancellationToken);

        Assert.Empty(actual);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task QueryPartyDetails_WithUnsuccessfulResponse_ThrowsPlatformDependencyException(HttpStatusCode statusCode)
    {
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
            Task.FromResult(new HttpResponseMessage(statusCode))));

        var exception = await Assert.ThrowsAsync<PlatformDependencyException>(() =>
            registerClient.QueryPartyDetails(["123456785"], [], TestContext.Current.CancellationToken));

        var innerException = Assert.IsType<PlatformHttpException>(exception.InnerException);
        Assert.Equal(statusCode, innerException.Response?.StatusCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"data":null}""")]
    [InlineData("""{"data":{}}""")]
    public async Task QueryPartyDetails_WithInvalidResponse_ThrowsPlatformDependencyException(string responseBody)
    {
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            })));

        var exception = await Assert.ThrowsAsync<PlatformDependencyException>(() =>
            registerClient.QueryPartyDetails(["123456785"], [], TestContext.Current.CancellationToken));

        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task QueryPartyDetails_WithCanceledToken_PropagatesCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
        {
            cancellationTokenSource.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            registerClient.QueryPartyDetails(["123456785"], [], cancellationTokenSource.Token));
    }

    [Fact]
    public async Task GetPartyDetails_WithEmptyJSONArrayResponse_ReturnsEmpty()
    {
        // Arrange
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
        {
            if (request!.RequestUri!.AbsolutePath.EndsWith("nameslookup"))
            {
                var responseContent = new PartyDetailsLookupResult
                {
                    PartyDetailsList = []
                };

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseContent), Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }));

        // Act
        List<PartyDetails> actual = await registerClient.GetPartyDetails(["test-org"], ["test-ssn"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetPartyDetails_WithEmptyList_ReturnsEmpty()
    {
        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetails(["empty-list"], ["empty-list"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetPartyDetails_WithEmptyOrganizationNumbersAndNullSocialSecurityNumbers_ReturnsEmpty()
    {
        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetails([], null);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetPartyDetails_WithNullDeserializedResponse_ReturnsEmpty()
    {
        // Arrange
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
        {
            if (request!.RequestUri!.AbsolutePath.EndsWith("nameslookup"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }));

        // Act
        List<PartyDetails> actual = await registerClient.GetPartyDetails(["test-org"], ["test-ssn"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetPartyDetails_WithNullOrganizationNumbersAndEmptySocialSecurityNumbers_ReturnsEmpty()
    {
        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetails(null, []);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetPartyDetails_WithPopulatedList_ReturnsExpectedData()
    {
        // Arrange
        var organizationNumbers = new List<string> { "populated-list" };
        var socialSecurityNumbers = new List<string> { "populated-list" };

        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetails(organizationNumbers, socialSecurityNumbers);

        // Assert
        Assert.Equal(4, actual.Count);

        var firstOrganization = actual.FirstOrDefault(e => e.OrganizationNumber == "313600947");
        Assert.NotNull(firstOrganization);
        Assert.Equal("Test Organization 1", firstOrganization.Name);

        var secondOrganization = actual.FirstOrDefault(e => e.OrganizationNumber == "315058384");
        Assert.NotNull(secondOrganization);
        Assert.Equal("Test Organization 2", secondOrganization.Name);

        var firstPerson = actual.FirstOrDefault(e => e.NationalIdentityNumber == "07837399275");
        Assert.NotNull(firstPerson);
        Assert.Equal("Test Person 1", firstPerson.Name);

        var secondPerson = actual.FirstOrDefault(e => e.NationalIdentityNumber == "04917199103");
        Assert.NotNull(secondPerson);
        Assert.Equal("Test Person 2", secondPerson.Name);
    }

    [Fact]
    public async Task GetPartyDetails_WithUnavailableEndpoint_ThrowsException()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformDependencyException>(async () => await _registerClient.GetPartyDetails(["unavailable"], ["unavailable"]));

        // Assert
        var innerException = Assert.IsType<PlatformHttpException>(exception.InnerException);
        Assert.StartsWith("Platform dependency", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, innerException.Response?.StatusCode);
    }

    [Fact]
    public async Task GetPartyDetails_WithValidAccessToken_AddsHeader()
    {
        // Arrange
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
        {
            if (request!.RequestUri!.AbsolutePath.EndsWith("nameslookup"))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new PartyDetailsLookupResult { PartyDetailsList = [] }), Encoding.UTF8, "application/json")
                };

                // Assert that the PlatformAccessToken header is present
                Assert.True(request.Headers.Contains("PlatformAccessToken"));
                Assert.Equal("valid-token", request.Headers.GetValues("PlatformAccessToken").First());

                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }));

        // Act
        List<PartyDetails> actual = await registerClient.GetPartyDetails(["test-org"], ["test-ssn"]);

        // Assert
        Assert.NotNull(actual);
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetPartyDetails_WithTransientHttpException_ThrowsPlatformDependencyException()
    {
        // Arrange
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
        {
            if (request!.RequestUri!.AbsolutePath.EndsWith("nameslookup"))
            {
                throw new HttpRequestException("Simulated transient network error");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PlatformDependencyException>(
            async () => await registerClient.GetPartyDetails(["test-org"], ["test-ssn"]));

        Assert.NotNull(exception.InnerException);
        Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Contains("Simulated transient network error", exception.InnerException.Message);
    }

    [Fact]
    public async Task GetPartyDetails_WithTaskCanceledException_ThrowsPlatformDependencyException()
    {
        // Arrange
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
        {
            if (request!.RequestUri!.AbsolutePath.EndsWith("nameslookup"))
            {
                throw new TaskCanceledException("Request timeout");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PlatformDependencyException>(
            async () => await registerClient.GetPartyDetails(["test-org"], ["test-ssn"]));

        Assert.NotNull(exception.InnerException);
        Assert.IsType<TaskCanceledException>(exception.InnerException);
        Assert.Contains("Request timeout", exception.InnerException.Message);
    }

    private RegisterClient CreateRegisterClient(DelegatingHandler? handler = null, string accessToken = "valid-token")
    {
        var registerHttpMessageHandler = handler ?? new DelegatingHandlerStub(async (request, token) =>
        {
            PartyDetailsLookupBatch? lookup = JsonSerializer.Deserialize<PartyDetailsLookupBatch>(await request!.Content!.ReadAsStringAsync(token), _serializerOptions);
            return await GetPartyDetailsResponse(lookup!);
        });

        PlatformSettings settings = new()
        {
            ApiRegisterEndpoint = "https://dummy.endpoint/register/api/v1/"
        };

        Mock<IAccessTokenGenerator> accessTokenGenerator = new();
        accessTokenGenerator.Setup(e => e.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>())).Returns(accessToken);

        return new RegisterClient(new HttpClient(registerHttpMessageHandler), Options.Create(settings), accessTokenGenerator.Object);
    }

    private Task<HttpResponseMessage> CreateMockResponse(object? contentData, HttpStatusCode statusCode)
    {
        JsonContent? content = (contentData != null) ? JsonContent.Create(contentData, options: _serializerOptions) : null;

        return Task.FromResult(new HttpResponseMessage()
        {
            StatusCode = statusCode,
            Content = content
        });
    }

    private Task<HttpResponseMessage> GetPartyDetailsResponse(PartyDetailsLookupBatch lookup)
    {
        var contentData = new PartyDetailsLookupResult { PartyDetailsList = [] };

        if (lookup == null)
        {
            return CreateMockResponse(contentData, HttpStatusCode.BadRequest);
        }

        HttpStatusCode statusCode = HttpStatusCode.OK;

        if (lookup.PartyDetailsLookupRequestList == null)
        {
            return CreateMockResponse(contentData, statusCode);
        }

        foreach (var lookupRequest in lookup.PartyDetailsLookupRequestList)
        {
            if (!string.IsNullOrWhiteSpace(lookupRequest.OrganizationNumber))
            {
                switch (lookupRequest.OrganizationNumber)
                {
                    case "empty-list":
                        break;

                    case "populated-list":
                        contentData.PartyDetailsList.AddRange(
                        [
                            new() { OrganizationNumber = "313600947", Name = "Test Organization 1" },
                            new() { OrganizationNumber = "315058384", Name = "Test Organization 2" }
                        ]);
                        break;

                    case "unavailable":
                        statusCode = HttpStatusCode.ServiceUnavailable;
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(lookupRequest.SocialSecurityNumber))
            {
                switch (lookupRequest.SocialSecurityNumber)
                {
                    case "empty-list":
                        break;

                    case "populated-list":
                        contentData.PartyDetailsList.AddRange(
                        [
                            new() { NationalIdentityNumber = "07837399275", Name = "Test Person 1" },
                            new() { NationalIdentityNumber = "04917199103", Name = "Test Person 2" }
                        ]);
                        break;

                    case "unavailable":
                        statusCode = HttpStatusCode.ServiceUnavailable;
                        break;
                }
            }
        }

        return CreateMockResponse(contentData, statusCode);
    }
}
