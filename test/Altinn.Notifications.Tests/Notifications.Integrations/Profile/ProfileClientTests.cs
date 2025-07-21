using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Clients;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Profile;
using Altinn.Notifications.Integrations.Profile.Models;

using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.Profile;

public class ProfileClientTests
{
    private static readonly JsonSerializerOptions _serializerOptions = JsonSerializerOptionsProvider.Options;

    private static ProfileClient CreateProfileClient(DelegatingHandler? handler = null)
    {
        var profileHttpMessageHandler = handler ?? new DelegatingHandlerStub(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("users/contactpoint/lookup"))
            {
                UserContactPointLookup? lookup = JsonSerializer.Deserialize<UserContactPointLookup>(await request!.Content!.ReadAsStringAsync(token), _serializerOptions);
                return await GetUserProfileResponse(lookup!);
            }
            else if (request.RequestUri!.AbsolutePath.EndsWith("units/contactpoint/lookup"))
            {
                UnitContactPointLookup? lookup = JsonSerializer.Deserialize<UnitContactPointLookup>(await request!.Content!.ReadAsStringAsync(token), _serializerOptions);
                return await GetUnitProfileResponse(lookup!);
            }
            else if (request!.RequestUri!.AbsolutePath.EndsWith("organizations/notificationaddresses/lookup"))
            {
                OrgContactPointLookup? lookup = JsonSerializer.Deserialize<OrgContactPointLookup>(await request!.Content!.ReadAsStringAsync(token), _serializerOptions);
                return await GetResponse(lookup!);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        PlatformSettings settings = new()
        {
            ApiProfileEndpoint = "https://platform.at22.altinn.cloud/profile/api/v1/"
        };

        return new ProfileClient(
                      new HttpClient(profileHttpMessageHandler),
                      Options.Create(settings));
    }

    [Fact]
    public async Task GetUserContactPoints_SuccessResponse_NoMatches()
    {
        // Act
        List<UserContactPoints> actual = await CreateProfileClient().GetUserContactPoints(["empty-list"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetUserContactPoints_SuccessResponse_TwoElementsInResponse()
    {
        // Act
        List<UserContactPoints> actual = await CreateProfileClient().GetUserContactPoints(["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("01025101038", actual.Select(cp => cp.NationalIdentityNumber));
    }

    [Fact]
    public async Task GetUserContactPoints_FailureResponse_ExceptionIsThrown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await CreateProfileClient().GetUserContactPoints(["unavailable"]));

        Assert.StartsWith("ProfileClient.GetUserContactPoints failed with status code", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
    }

    [Fact]
    public async Task GetUserRegisteredContactPoints_SuccessResponse_NoMatches()
    {
        // Act
        List<OrganizationContactPoints> actual = await CreateProfileClient().GetUserRegisteredContactPoints(["12345678", "98754321"], "no-matches");

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetUserRegisteredContactPoints_SuccessResponse_TwoListElementsReturned()
    {
        // Act
        List<OrganizationContactPoints> actual = await CreateProfileClient().GetUserRegisteredContactPoints(["12345678", "98754321"], "some-matches");

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains(actual, ocp => ocp.OrganizationNumber == "123456789" && ocp.PartyId == 56789 && ocp.UserContactPoints.Any(u => u.UserId == 20001));
    }

    [Fact]
    public async Task GetUserRegisteredContactPoints_FailureResponse_ExceptionIsThrown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(
            async () => await CreateProfileClient().GetUserRegisteredContactPoints(["12345678", "98754321"], "error-resource"));

        Assert.StartsWith("ProfileClient.GetUserRegisteredContactPoints failed with status code", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationContactPoints_WithEmptyOrganizationNumbers_ReturnsEmpty()
    {
        // Arrange
        List<string> organizationNumbers = [];

        // Act
        var result = await CreateProfileClient().GetOrganizationContactPoints(organizationNumbers);

        // Assert
        Assert.Empty(result);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetOrganizationContactPoints_WithNullResponseContent_ReturnsEmpty()
    {
        // Arrange
        var registerClient = CreateProfileClient(new DelegatingHandlerStub((request, token) =>
        {
            if (request!.RequestUri!.AbsolutePath.EndsWith("organizations/notificationaddresses/lookup"))
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
    public async Task GetOrganizationContactPoints_WhenEmptyResult_ReturnsEmpty()
    {
        // Act
        List<OrganizationContactPoints> actual = await CreateProfileClient().GetOrganizationContactPoints(["empty-list"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetOrganizationContactPoints_WithPopulatedList_ReturnsExpectedData()
    {
        // Act
        List<OrganizationContactPoints> actual = await CreateProfileClient().GetOrganizationContactPoints(["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("910011154", actual.Select(e => e.OrganizationNumber));
    }

    [Fact]
    public async Task GetOrganizationContactPoints_WithUnavailableEndpoint_ThrowsException()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await CreateProfileClient().GetOrganizationContactPoints(["unavailable"]));

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
    }

    private static Task<HttpResponseMessage> GetUserProfileResponse(UserContactPointLookup lookup)
    {
        HttpStatusCode statusCode = HttpStatusCode.OK;
        object? contentData = null;

        switch (lookup.NationalIdentityNumbers[0])
        {
            case "empty-list":
                contentData = new UserContactPointsList() { ContactPointsList = new List<UserContactPointsDto>() };
                break;
            case "populated-list":
                contentData = new UserContactPointsList()
                {
                    ContactPointsList =
                    [
                        new UserContactPointsDto() { NationalIdentityNumber = "01025101038", Email = string.Empty },
                        new UserContactPointsDto() { NationalIdentityNumber = "01025101037", Email = string.Empty }
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

    private static Task<HttpResponseMessage> GetUnitProfileResponse(UnitContactPointLookup lookup)
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

    private static Task<HttpResponseMessage> GetResponse(OrgContactPointLookup lookup)
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

    private static Task<HttpResponseMessage> CreateMockResponse(object? contentData, HttpStatusCode statusCode)
    {
        JsonContent? content = (contentData != null) ? JsonContent.Create(contentData, options: _serializerOptions) : null;

        return Task.FromResult(new HttpResponseMessage()
        {
            StatusCode = statusCode,
            Content = content
        });
    }
}
