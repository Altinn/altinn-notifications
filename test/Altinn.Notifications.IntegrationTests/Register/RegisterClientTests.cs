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
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await _registerClient.GetPartyDetails(["unavailable"], ["unavailable"]));

        // Assert
        Assert.StartsWith("503 - Service Unavailable", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
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
