using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Notifications.Core.Models.ContactPoints;
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
    public async Task GetOrganizationContactPoints_WithEmptyOrganizationNumbers_ReturnsEmpty()
    {
        // Arrange
        List<string> organizationNumbers = [];

        // Act
        var result = await _registerClient.GetOrganizationContactPoints(organizationNumbers);

        // Assert
        Assert.Empty(result);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetOrganizationContactPoints_WithNullResponseContent_ReturnsEmpty()
    {
        // Arrange
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
        {
            if (request!.RequestUri!.AbsolutePath.EndsWith("contactpoint/lookup"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }));

        // Act
        List<OrganizationContactPoints> actual = await registerClient.GetOrganizationContactPoints(["test-org"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetOrganizationContactPoints_WithPopulatedList_ReturnsExpectedData()
    {
        // Act
        List<OrganizationContactPoints> actual = await _registerClient.GetOrganizationContactPoints(["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("910011154", actual.Select(e => e.OrganizationNumber));
    }

    [Fact]
    public async Task GetOrganizationContactPoints_WithUnavailableEndpoint_ThrowsException()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await _registerClient.GetOrganizationContactPoints(["unavailable"]));

        // Assert
        Assert.StartsWith("503 - Service Unavailable", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
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
        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetails(["populated-list"], ["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("313600947", actual.Select(e => e.OrganizationNumber));
        Assert.Contains("07837399275", actual.Select(e => e.NationalIdentityNumber));
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

    [Fact]
    public async Task GetPartyDetails_WithValidJSONResponse_ReturnsPopulatedList()
    {
        // Arrange
        var registerClient = CreateRegisterClient(new DelegatingHandlerStub((request, token) =>
        {
            if (request!.RequestUri!.AbsolutePath.EndsWith("nameslookup"))
            {
                var responseContent = new PartyDetailsLookupResult
                {
                    PartyDetailsList =
                    [
                        new() { NationalIdentityNumber = "987654321", Name = "Person 1" },
                        new() { OrganizationNumber = "123456789", Name = "Organization 1" }
                    ]
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
        Assert.NotEmpty(actual);
        Assert.Equal(2, actual.Count);
        Assert.Contains(actual, e => e.NationalIdentityNumber == "987654321" && e.Name == "Person 1");
        Assert.Contains(actual, e => e.OrganizationNumber == "123456789" && e.Name == "Organization 1");
    }

    private RegisterClient CreateRegisterClient(DelegatingHandler? handler = null, string accessToken = "valid-token")
    {
        var registerHttpMessageHandler = handler ?? new DelegatingHandlerStub(async (request, token) =>
        {
            if (request!.RequestUri!.AbsolutePath.EndsWith("contactpoint/lookup"))
            {
                OrgContactPointLookup? lookup = JsonSerializer.Deserialize<OrgContactPointLookup>(await request!.Content!.ReadAsStringAsync(token), _serializerOptions);
                return await GetResponse(lookup!);
            }
            else if (request!.RequestUri!.AbsolutePath.EndsWith("nameslookup"))
            {
                PartyDetailsLookupBatch? lookup = JsonSerializer.Deserialize<PartyDetailsLookupBatch>(await request!.Content!.ReadAsStringAsync(token), _serializerOptions);
                return await GetPartyDetailsResponse(lookup!);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
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
        object? contentData = null;
        HttpStatusCode statusCode = HttpStatusCode.OK;

        var firstRequest = lookup.PartyDetailsLookupRequestList?.FirstOrDefault();
        if (firstRequest == null)
        {
            return CreateMockResponse(contentData, statusCode);
        }

        if (!string.IsNullOrWhiteSpace(firstRequest.OrganizationNumber))
        {
            switch (firstRequest.OrganizationNumber)
            {
                case "empty-list":
                    contentData = new PartyDetailsLookupResult() { PartyDetailsList = [] };
                    break;

                case "populated-list":
                    contentData = new PartyDetailsLookupResult
                    {
                        PartyDetailsList =
                        [
                            new() { OrganizationNumber = "313600947", Name = "Test Organization 1" },
                            new() { OrganizationNumber = "315058384", Name = "Test Organization 2" }
                        ]
                    };
                    break;

                case "unavailable":
                    statusCode = HttpStatusCode.ServiceUnavailable;
                    break;
            }
        }
        else if (!string.IsNullOrWhiteSpace(firstRequest.SocialSecurityNumber))
        {
            switch (firstRequest.SocialSecurityNumber)
            {
                case "empty-list":
                    contentData = new PartyDetailsLookupResult() { PartyDetailsList = [] };
                    break;

                case "populated-list":
                    contentData = new PartyDetailsLookupResult
                    {
                        PartyDetailsList =
                        [
                            new() { NationalIdentityNumber = "07837399275", Name = "Test Person 1" },
                            new() { NationalIdentityNumber = "04917199103", Name = "Test Person 2" }
                        ]
                    };
                    break;

                case "unavailable":
                    statusCode = HttpStatusCode.ServiceUnavailable;
                    break;
            }
        }

        return CreateMockResponse(contentData, statusCode);
    }

    private Task<HttpResponseMessage> GetResponse(OrgContactPointLookup lookup)
    {
        object? contentData = null;
        HttpStatusCode statusCode = HttpStatusCode.OK;

        switch (lookup.OrganizationNumbers[0])
        {
            case "empty-list":
                contentData = new OrgContactPointsList() { ContactPointsList = [] };
                break;

            case "populated-list":
                contentData = new OrgContactPointsList
                {
                    ContactPointsList =
                    [
                        new OrganizationContactPoints { OrganizationNumber = "910011154", EmailList = [] },
                        new OrganizationContactPoints { OrganizationNumber = "910011155", EmailList = [] }
                    ]
                };
                break;

            case "unavailable":
                statusCode = HttpStatusCode.ServiceUnavailable;
                break;
        }

        return CreateMockResponse(contentData, statusCode);
    }
}
