﻿using System.Collections.Generic;
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
        var sblBridgeHttpMessageHandler = new DelegatingHandlerStub(async (request, token) =>
           {
               if (request!.RequestUri!.AbsolutePath.EndsWith("contactpoint/lookup"))
               {
                   UserContactPointLookup? lookup = JsonSerializer.Deserialize<UserContactPointLookup>(await request!.Content!.ReadAsStringAsync(token), JsonSerializerOptionsProvider.Options);
                   return await GetResponse<UserContactPointsList>(lookup!);
               }
               else if (request!.RequestUri!.AbsolutePath.EndsWith("contactpoint/availability"))
               {
                   UserContactPointLookup? lookup = JsonSerializer.Deserialize<UserContactPointLookup>(await request!.Content!.ReadAsStringAsync(token), JsonSerializerOptionsProvider.Options);
                   return await GetResponse<UserContactPointAvailabilityList>(lookup!);
               }

               return new HttpResponseMessage(HttpStatusCode.NotFound);
           });

        PlatformSettings settings = new()
        {
            ApiProfileEndpoint = "https://platform.at22.altinn.cloud/profile/api/v1/"
        };

        _profileClient = new ProfileClient(
                      new HttpClient(sblBridgeHttpMessageHandler),
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
    public async Task GetUserContactPointAvailabilities_SuccessResponse_NoMatches()
    {
        // Act
        List<UserContactPointAvailability> actual = await _profileClient.GetUserContactPointAvailabilities(["empty-list"]);

        // Assert
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetUserContactPointAvailabilities_SuccessResponse_TwoElementsInResponse()
    {
        // Act
        List<UserContactPointAvailability> actual = await _profileClient.GetUserContactPointAvailabilities(["populated-list"]);

        // Assert
        Assert.Equal(2, actual.Count);
        Assert.Contains("01025101038", actual.Select(cp => cp.NationalIdentityNumber));
    }

    [Fact]
    public async Task GetUserContactPointAvailabilities_FailureResponse_ExceptionIsThrown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(async () => await _profileClient.GetUserContactPointAvailabilities(["unavailable"]));

        // Assert
        Assert.StartsWith("ProfileClient.GetUserContactPointAvailabilities failed with status code", exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.Response?.StatusCode);
    }

    private Task<HttpResponseMessage> GetResponse<T>(UserContactPointLookup lookup)
    {
        HttpStatusCode statusCode = HttpStatusCode.OK;
        object? contentData = null;

        switch (lookup.NationalIdentityNumbers[0])
        {
            case "empty-list":
                contentData = GetEmptyListContent<T>();
                break;
            case "populated-list":
                contentData = GetPopulatedListContent<T>();
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

    private static object? GetEmptyListContent<T>()
    {
        if (typeof(T) == typeof(UserContactPointAvailabilityList))
        {
            return new UserContactPointAvailabilityList() { AvailabilityList = new List<UserContactPointAvailability>() };
        }
        else if (typeof(T) == typeof(UserContactPointsList))
        {
            return new UserContactPointsList() { ContactPointList = new List<UserContactPoints>() };
        }

        return null;
    }

    private static object? GetPopulatedListContent<T>()
    {
        if (typeof(T) == typeof(UserContactPointAvailabilityList))
        {
            var availabilityList = new List<UserContactPointAvailability>
            {
                new UserContactPointAvailability() { NationalIdentityNumber = "01025101038", EmailRegistered = true },
                new UserContactPointAvailability() { NationalIdentityNumber = "01025101037", EmailRegistered = false }
            };
            return new UserContactPointAvailabilityList() { AvailabilityList = availabilityList };
        }
        else if (typeof(T) == typeof(UserContactPointsList))
        {
            var contactPointsList = new List<UserContactPoints>
            {
                new UserContactPoints() { NationalIdentityNumber = "01025101038", Email = string.Empty },
                new UserContactPoints() { NationalIdentityNumber = "01025101037", Email = string.Empty }
            };
            return new UserContactPointsList() { ContactPointList = contactPointsList };
        }

        return null;
    }
}
