using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Altinn.Notifications.Tools.PlatformMocks.Mocks;

/// <summary>
/// Fixed condition endpoints an order's <c>conditionEndpoint</c> can point to, one for each answer the API has to handle.
/// See https://docs.altinn.studio/notifications/explanation/send-condition/.
/// </summary>
internal sealed class ConditionsMock : IPlatformMock
{
    /// <summary>
    /// The paths and what they answer: status code, content type and body.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (int StatusCode, string ContentType, string Body)> Conditions =
        new Dictionary<string, (int, string, string)>
        {
            ["/condition/true"] = (200, "application/json", """{"sendNotification":true}"""),
            ["/condition/false"] = (200, "application/json", """{"sendNotification":false}"""),
            ["/condition/corrupt"] = (200, "text/plain", "this is not json {{{"),
            ["/condition/error"] = (500, "text/plain", "condition endpoint failed"),
        };

    public string System => "conditions";

    public void AddMappings(WireMockServer server)
    {
        foreach (var (path, (statusCode, contentType, body)) in Conditions)
        {
            server
                .Given(Request.Create().WithPath(path).UsingGet())
                .RespondWith(Response.Create().WithStatusCode(statusCode).WithHeader("Content-Type", contentType).WithBody(body));
        }
    }
}
