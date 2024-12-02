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
    public async Task GetOrganizationContactPoints_SuccessResponse_NoMatches()
    {
        // Act
        List<OrganizationContactPoints> actual = await _registerClient.GetOrganizationContactPoints(["empty-list"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetOrganizationContactPoints_SuccessResponse_TwoElementsInResponse()
    {
        // Act
        List<OrganizationContactPoints> actual = await _registerClient.GetOrganizationContactPoints(["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("910011154", actual.Select(cp => cp.OrganizationNumber));
    }

    [Fact]
    public async Task GetOrganizationContactPoints_FailureResponse_ExceptionIsThrown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await _registerClient.GetOrganizationContactPoints(["unavailable"]));

        Assert.StartsWith("503 - Service Unavailable", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
    }

    [Fact]
    public async Task GetPartyDetailsForOrganizations_SuccessResponse_NoMatches()
    {
        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetailsForOrganizations(["empty-list"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetPartyDetailsForOrganizations_SuccessResponse_TwoElementsInResponse()
    {
        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetailsForOrganizations(["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("313600947", actual.Select(pd => pd.OrganizationNumber));
    }

    [Fact]
    public async Task GetPartyDetailsForOrganizations_FailureResponse_ExceptionIsThrown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await _registerClient.GetPartyDetailsForOrganizations(["unavailable"]));

        Assert.StartsWith("503 - Service Unavailable", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
    }

    [Fact]
    public async Task GetPartyDetailsForPersons_SuccessResponse_NoMatches()
    {
        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetailsForPersons(["empty-list"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetPartyDetailsForPersons_SuccessResponse_TwoElementsInResponse()
    {
        // Act
        List<PartyDetails> actual = await _registerClient.GetPartyDetailsForPersons(["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("04917199103", actual.Select(pd => pd.NationalIdentityNumber));
    }

    [Fact]
    public async Task GetPartyDetailsForPersons_FailureResponse_ExceptionIsThrown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await _registerClient.GetPartyDetailsForPersons(["unavailable"]));

        Assert.StartsWith("503 - Service Unavailable", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
    }

    private Task<HttpResponseMessage> GetResponse(OrgContactPointLookup lookup)
    {
        HttpStatusCode statusCode = HttpStatusCode.OK;
        object? contentData = null;

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
                        new() { OrganizationNumber = "910011154", EmailList = [] },
                        new() { OrganizationNumber = "910011155", EmailList = [] }
                    ]
                };
                break;
            case "unavailable":
                statusCode = HttpStatusCode.ServiceUnavailable;
                break;
        }

        JsonContent? content = (contentData != null) ? JsonContent.Create(contentData, options: _serializerOptions) : null;

        return Task.FromResult(
            new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = content
            });
    }

    private Task<HttpResponseMessage> GetPartyDetailsResponse(PartyDetailsLookupBatch lookup)
    {
        HttpStatusCode statusCode = HttpStatusCode.OK;
        object? contentData = null;

        switch (lookup.PartyDetailsLookupRequestList?.FirstOrDefault()?.OrganizationNumber)
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

        switch (lookup.PartyDetailsLookupRequestList?.FirstOrDefault()?.SocialSecurityNumber)
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

        JsonContent? content = (contentData != null) ? JsonContent.Create(contentData, options: _serializerOptions) : null;

        return Task.FromResult(
            new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = content
            });
    }
}
