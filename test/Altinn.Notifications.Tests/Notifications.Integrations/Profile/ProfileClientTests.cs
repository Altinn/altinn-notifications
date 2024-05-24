using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Clients;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Profile;
using Altinn.Notifications.Integrations.Register;

using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.Profile;

public class ProfileClientTests
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ProfileClient _profileClient;

    public ProfileClientTests()
    {
        var profileHttpMessageHandler = new DelegatingHandlerStub(async (request, token) =>
           {
               if (request!.RequestUri!.AbsolutePath.EndsWith("users/contactpoint/lookup"))
               {
                   UserContactPointLookup? lookup = JsonSerializer.Deserialize<UserContactPointLookup>(await request!.Content!.ReadAsStringAsync(token), JsonSerializerOptionsProvider.Options);
                   return await GetUserProfileResponse(lookup!);
               }
               else if (request!.RequestUri!.AbsolutePath.EndsWith("units/contactpoint/lookup"))
               {
                   UnitContactPointLookup? lookup = JsonSerializer.Deserialize<UnitContactPointLookup>(await request!.Content!.ReadAsStringAsync(token), JsonSerializerOptionsProvider.Options);
                   return await GetUnitProfileResponse(lookup!);
               }

               return new HttpResponseMessage(HttpStatusCode.NotFound);
           });

        PlatformSettings settings = new()
        {
            ApiProfileEndpoint = "https://platform.at22.altinn.cloud/profile/api/v1/"
        };

        _profileClient = new ProfileClient(
                      new HttpClient(profileHttpMessageHandler),
                      Options.Create(settings));
    }

    [Fact]
    public async Task GetUserContactPoints_SuccessResponse_NoMatches()
    {
        // Act
        List<UserContactPoints> actual = await _profileClient.GetUserContactPoints(["empty-list"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetUserContactPoints_SuccessResponse_TwoElementsInResponse()
    {
        // Act
        List<UserContactPoints> actual = await _profileClient.GetUserContactPoints(["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("01025101038", actual.Select(cp => cp.NationalIdentityNumber));
    }

    [Fact]
    public async Task GetUserContactPoints_FailureResponse_ExceptionIsThrown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await _profileClient.GetUserContactPoints(["unavailable"]));

        Assert.StartsWith("ProfileClient.GetUserContactPoints failed with status code", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
    }

    [Fact]
    public async Task GetUserRegisteredContactPoints_SuccessResponse_NoMatches()
    {
        // Act
        List<OrganizationContactPoints> actual = await _profileClient.GetUserRegisteredContactPoints(["12345678", "98754321"], "no-matches");

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetUserRegisteredContactPoints_SuccessResponse_TwoListElementsReturned()
    {
        // Act
        List<OrganizationContactPoints> actual = await _profileClient.GetUserRegisteredContactPoints(["12345678", "98754321"], "some-matches");

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains(actual, ocp => ocp.OrganizationNumber == "123456789" && ocp.PartyId == 56789 && ocp.UserContactPoints.Any(u => u.UserId == 20001));
    }

    [Fact]
    public async Task GetUserRegisteredContactPoints_FailureResponse_ExceptionIsThrown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(
            async () => await _profileClient.GetUserRegisteredContactPoints(["12345678", "98754321"], "error-resource"));

        Assert.StartsWith("ProfileClient.GetUserRegisteredContactPoints failed with status code", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
    }

    private Task<HttpResponseMessage> GetUserProfileResponse(UserContactPointLookup lookup)
    {
        HttpStatusCode statusCode = HttpStatusCode.OK;
        object? contentData = null;

        switch (lookup.NationalIdentityNumbers[0])
        {
            case "empty-list":
                contentData = new UserContactPointsList() { ContactPointsList = new List<UserContactPoints>() };
                break;
            case "populated-list":
                contentData = new UserContactPointsList()
                {
                    ContactPointsList =
                    [
                        new UserContactPoints() { NationalIdentityNumber = "01025101038", Email = string.Empty },
                        new UserContactPoints() { NationalIdentityNumber = "01025101037", Email = string.Empty }
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

    private Task<HttpResponseMessage> GetUnitProfileResponse(UnitContactPointLookup lookup)
    {
        HttpStatusCode statusCode = HttpStatusCode.OK;
        object? contentData = null;

        switch (lookup.ResourceId)
        {
            case "no-matches":
                contentData = new OrgContactPointsList();
                break;
            case "some-matches":
                contentData = new OrgContactPointsList()
                {
                    ContactPointsList = new List<OrganizationContactPoints>
                    {
                        new() { OrganizationNumber = "123456789", PartyId = 56789, UserContactPoints = [new() { UserId = 20001 }] },
                        new() { OrganizationNumber = "987654321", PartyId = 54321,  UserContactPoints = [new() { UserId = 20001 }] }
                    }
                };
                break;
            case "error-resource":
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
