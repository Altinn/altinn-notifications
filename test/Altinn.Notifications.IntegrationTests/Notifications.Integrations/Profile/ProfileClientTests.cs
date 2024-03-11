using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Clients;
using Altinn.Notifications.Integrations.Profile;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.Profile;

public class ProfileClientTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.EmailNotificationOrdersController>>
{
    private readonly WebApplicationFactorySetup<Controllers.EmailNotificationOrdersController> _factorySetup;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProfileClientTests(IntegrationTestWebApplicationFactory<Controllers.EmailNotificationOrdersController> factory)
    {
        _factorySetup = new WebApplicationFactorySetup<Controllers.EmailNotificationOrdersController>(factory)
        {
            SblBridgeHttpMessageHandler = new DelegatingHandlerStub(async (request, token) =>
            {
                if (request!.RequestUri!.AbsolutePath.EndsWith("contactpoint/lookup"))
                {
                    UserContactPointLookup? lookup = JsonSerializer.Deserialize<UserContactPointLookup>(await request!.Content!.ReadAsStringAsync(token));
                    return await GetResponse<UserContactPointsList>(lookup!);
                }
                else if (request!.RequestUri!.AbsolutePath.EndsWith("contactpoint/availability"))
                {
                    UserContactPointLookup? lookup = JsonSerializer.Deserialize<UserContactPointLookup>(await request!.Content!.ReadAsStringAsync(token));
                    return await GetResponse<UserContactPointAvailabilityList>(lookup!);
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            })
        };
    }

    [Fact]
    public async Task GetUserContactPoints_SuccessResponse_NoMatches()
    {
        // Arrange
        var client = _factorySetup.GetTestServerClient();
        UserContactPointLookup lookup = new() { NationalIdentityNumbers = ["empty-list"] };

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, "users/contactpoint/lookup");

        httpRequestMessage.Content = new StringContent(JsonSerializer.Serialize(lookup, _serializerOptions), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        UserContactPointsList contactPoints = JsonSerializer.Deserialize<UserContactPointsList>(await response.Content.ReadAsStringAsync())!;
        Assert.Empty(contactPoints.ContactPointList);
    }

    [Fact]
    public async Task GetUserContactPoints_SuccessResponse_TwoElementsInResponse()
    {
        // Arrange
        var client = _factorySetup.GetTestServerClient();
        UserContactPointLookup lookup = new() { NationalIdentityNumbers = ["populated-list"] };

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, "users/contactpoint/lookup");

        httpRequestMessage.Content = new StringContent(JsonSerializer.Serialize(lookup, _serializerOptions), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        UserContactPointsList contactPoints = JsonSerializer.Deserialize<UserContactPointsList>(await response.Content.ReadAsStringAsync())!;
        Assert.True(contactPoints.ContactPointList.Count == 2);
        Assert.Contains("01025101038", contactPoints.ContactPointList.Select(cp => cp.NationalIdentityNumber));
    }

    [Fact]
    public async Task GetUserContactPoints_FailureResponse_ExceptionIsThrown()
    {
        // Arrange
        var client = _factorySetup.GetTestServerClient();
        UserContactPointLookup lookup = new() { NationalIdentityNumbers = ["unavailable"] };

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, "users/contactpoint/lookup");

        httpRequestMessage.Content = new StringContent(JsonSerializer.Serialize(lookup, _serializerOptions), System.Text.Encoding.UTF8, "application/json");

        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(() => client.SendAsync(httpRequestMessage));

        // Now you can access properties or perform specific validations on the exception
        Assert.StartsWith("ProfileClient.GetUserContactPoints failed with status code", exception.Message);
    }

    [Fact]
    public async Task GetUserContactPointAvailabilities_SuccessResponse_NoMatches()
    {
        // Arrange
        var client = _factorySetup.GetTestServerClient();
        UserContactPointLookup lookup = new() { NationalIdentityNumbers = ["empty-list"] };

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, "users/contactpoint/availability");

        httpRequestMessage.Content = new StringContent(JsonSerializer.Serialize(lookup, _serializerOptions), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        UserContactPointAvailabilityList contactPoints = JsonSerializer.Deserialize<UserContactPointAvailabilityList>(await response.Content.ReadAsStringAsync())!;
        Assert.Empty(contactPoints.AvailabilityList);
    }

    [Fact]
    public async Task GetUserContactPointAvailabilities_SuccessResponse_TwoElementsInResponse()
    {
        // Arrange
        var client = _factorySetup.GetTestServerClient();
        UserContactPointLookup lookup = new() { NationalIdentityNumbers = ["populated-list"] };

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, "users/contactpoint/availability");

        httpRequestMessage.Content = new StringContent(JsonSerializer.Serialize(lookup, _serializerOptions), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        UserContactPointAvailabilityList contactPoints = JsonSerializer.Deserialize<UserContactPointAvailabilityList>(await response.Content.ReadAsStringAsync())!;
        Assert.True(contactPoints.AvailabilityList.Count == 2);
        Assert.Contains("01025101038", contactPoints.AvailabilityList.Select(cp => cp.NationalIdentityNumber));
    }

    [Fact]
    public async Task GetUserContactPointAvailabilities_FailureResponse_ExceptionIsThrown()
    {
        // Arrange
        var client = _factorySetup.GetTestServerClient();
        UserContactPointLookup lookup = new() { NationalIdentityNumbers = ["unavailable"] };

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, "users/contactpoint/availability");

        httpRequestMessage.Content = new StringContent(JsonSerializer.Serialize(lookup, _serializerOptions), System.Text.Encoding.UTF8, "application/json");

        // Act
        var exception = await Assert.ThrowsAsync<PlatformHttpException>(() => client.SendAsync(httpRequestMessage));

        // Now you can access properties or perform specific validations on the exception
        Assert.StartsWith("ProfileClient.GetUserContactPointAvailabilities failed with status code", exception.Message);
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

    private object? GetEmptyListContent<T>()
    {
        if (typeof(T) == typeof(UserContactPointAvailability))
        {
            return new UserContactPointAvailabilityList() { AvailabilityList = new List<UserContactPointAvailability>() };
        }
        else if (typeof(T) == typeof(UserContactPoints))
        {
            return new UserContactPointsList() { ContactPointList = new List<UserContactPoints>() };
        }

        return null;
    }

    private object? GetPopulatedListContent<T>()
    {
        if (typeof(T) == typeof(UserContactPointAvailability))
        {
            var availabilityList = new List<UserContactPointAvailability>
        {
            new UserContactPointAvailability() { NationalIdentityNumber = "01025101038", EmailRegistered = true },
            new UserContactPointAvailability() { NationalIdentityNumber = "01025101037", EmailRegistered = false }
        };
            return new UserContactPointAvailabilityList() { AvailabilityList = availabilityList };
        }
        else if (typeof(T) == typeof(UserContactPoints))
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
