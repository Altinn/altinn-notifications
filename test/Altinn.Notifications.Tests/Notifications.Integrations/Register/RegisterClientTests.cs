using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Models.Parties;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Register;

using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.Register;

public class RegisterClientTests
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RegisterClient _registerClient;

    public RegisterClientTests()
    {
        var registerHttpMessageHandler = new DelegatingHandlerStub(async (request, token) =>
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
            ApiRegisterEndpoint = "https://platform.at22.altinn.cloud/register/api/v1/"
        };

        _registerClient = new RegisterClient(
            new HttpClient(registerHttpMessageHandler),
            Options.Create(settings));
    }

    [Fact]
    public async Task GetOrganizationContactPoints_WithEmptyList_ReturnsEmpty()
    {
        // Act
        List<OrganizationContactPoints> actual = await _registerClient.GetOrganizationContactPoints(["empty-list"]);

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
        Assert.Contains("910011154", actual.Select(cp => cp.OrganizationNumber));
    }

    [Fact]
    public async Task GetOrganizationContactPoints_WithUnavailableEndpoint_ThrowsException()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await _registerClient.GetOrganizationContactPoints(["unavailable"]));

        Assert.StartsWith("503 - Service Unavailable", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
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
    public async Task GetPartyDetails_WithPopulatedList_ReturnsExpectedData()
    {
        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetails(["populated-list"], ["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("313600947", actual.Select(pd => pd.OrganizationNumber));
    }

    [Fact]
    public async Task GetPartyDetails_WithUnavailableEndpoint_ThrowsException()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await _registerClient.GetPartyDetails(["unavailable"], ["unavailable"]));

        Assert.StartsWith("503 - Service Unavailable", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
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

            case "null-contact-points-list":
                contentData = new OrgContactPointsList { ContactPointsList = [] };
                break;
        }

        return CreateMockResponse(contentData, statusCode);
    }

    private Task<HttpResponseMessage> GetPartyDetailsResponse(PartyDetailsLookupBatch lookup)
    {
        object? contentData = null;
        HttpStatusCode statusCode = HttpStatusCode.OK;

        var firstRequest = lookup.PartyDetailsLookupRequestList?.FirstOrDefault();
        if (firstRequest != null)
        {
            if (firstRequest.OrganizationNumber != null)
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
            else if (firstRequest.SocialSecurityNumber != null)
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
        }

        return CreateMockResponse(contentData, statusCode);
    }
}
