using Altinn.Notifications.Tools.PlatformMocks.Configuration;

using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;
using WireMock.Util;

namespace Altinn.Notifications.Tools.PlatformMocks.Mocks;

/// <summary>
/// The dialog lookup in Dialogporten, which the API calls with the end user's token to check that the user may see a dialog's
/// notification log. Every end user has access to every dialog, except the dialogs in <see cref="DialogportenMockSettings.DeniedDialogs"/>.
/// It does not check the token, only that there is one, like the API's own check before it calls.
/// </summary>
internal sealed class DialogportenMock(DialogportenMockSettings settings) : IPlatformMock
{
    internal const string DialogLookupPath = "/dialogporten/api/v1/enduser/dialoglookup";

    private const string DialogIdPrefix = "urn:altinn:dialog-id:";

    public string System => "dialogporten";

    public void AddMappings(WireMockServer server)
    {
        server
            .Given(Request.Create().WithPath(DialogLookupPath).UsingGet())
            .RespondWith(Response.Create().WithCallback(Lookup));
    }

    private ResponseMessage Lookup(IRequestMessage request)
    {
        bool hasToken = request.Headers is not null
            && request.Headers.TryGetValue("Authorization", out var authorization)
            && authorization.Any(value => value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase));
        if (!hasToken)
        {
            return Answer(401, null);
        }

        string instanceRef = request.Query is not null && request.Query.TryGetValue("instanceRef", out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
        if (!instanceRef.StartsWith(DialogIdPrefix, StringComparison.Ordinal) || !Guid.TryParse(instanceRef[DialogIdPrefix.Length..], out Guid dialogId))
        {
            return Answer(400, new { title = "instanceRef must be urn:altinn:dialog-id:<guid>" });
        }

        if (settings.DeniedDialogs.Contains(dialogId))
        {
            return Answer(403, null);
        }

        return Answer(200, new { dialogId, instanceRef });
    }

    private static ResponseMessage Answer(int statusCode, object? body)
    {
        var message = new ResponseMessage { StatusCode = statusCode };
        if (body is not null)
        {
            message.AddHeader("Content-Type", "application/json");
            message.BodyData = new BodyData { BodyAsString = MockJson.Write(body), DetectedBodyType = BodyType.String };
        }

        return message;
    }
}
