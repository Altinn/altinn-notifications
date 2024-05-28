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
                   OrgContactPointLookup? lookup = JsonSerializer.Deserialize<OrgContactPointLookup>(await request!.Content!.ReadAsStringAsync(token), JsonSerializerOptionsProvider.Options);
                   return await GetResponse(lookup!);
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

    private Task<HttpResponseMessage> GetResponse(OrgContactPointLookup lookup)
    {
        HttpStatusCode statusCode = HttpStatusCode.OK;
        object? contentData = null;

        switch (lookup.OrganizationNumbers[0])
        {
            case "empty-list":
                contentData = new OrgContactPointsList() { ContactPointsList = new List<OrganizationContactPoints>() };
                break;
            case "populated-list":
                contentData = new OrgContactPointsList
                {
                    ContactPointsList =
                    [
                        new OrganizationContactPoints() { OrganizationNumber = "910011154", EmailList = [] },
                        new OrganizationContactPoints() { OrganizationNumber = "910011155", EmailList = [] }
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
