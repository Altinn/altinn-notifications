using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Tools.PlatformMocks.Configuration;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Altinn.Notifications.Tools.PlatformMocks.Mocks;

/// <summary>
/// The decision endpoint in Altinn Authorization. It permits every request in a XACML multi-request, except for the users in
/// <see cref="AuthorizationMockSettings.DeniedUsers"/>. Each result carries the attributes the request marked with
/// <c>IncludeInResult</c>, since the notifications API matches results to users and parties by them.
/// </summary>
internal sealed class AuthorizationMock(AuthorizationMockSettings settings) : IPlatformMock
{
    internal const string DecisionPath = "/authorization/api/v1/decision";

    internal const string SubjectCategory = "urn:oasis:names:tc:xacml:1.0:subject-category:access-subject";
    internal const string ActionCategory = "urn:oasis:names:tc:xacml:3.0:attribute-category:action";
    internal const string ResourceCategory = "urn:oasis:names:tc:xacml:3.0:attribute-category:resource";

    private const string UserIdAttribute = "urn:altinn:userid";

    // XACML JSON is PascalCase.
    private static readonly JsonSerializerOptions _xacmlOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public string System => "authorization";

    public void AddMappings(WireMockServer server)
    {
        server
            .Given(Request.Create().WithPath(DecisionPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(request => MockJson.Write(Decide(MockJson.Read<XacmlRequestRoot>(request.Body).Request), _xacmlOptions)));
    }

    private object Decide(XacmlRequest request)
    {
        // Without a multi-request every category is part of the one request.
        List<List<string>> references = request.MultiRequests?.RequestReference.Select(r => r.ReferenceId).ToList()
            ?? [[.. request.AccessSubject.Concat(request.Action).Concat(request.Resource).Select(c => c.Id ?? string.Empty)]];

        return new
        {
            Response = references.Select(ids =>
            {
                var subject = Find(request.AccessSubject, ids);
                var action = Find(request.Action, ids);
                var resource = Find(request.Resource, ids);

                bool denied = subject?.Attribute.Exists(a => a.AttributeId == UserIdAttribute && int.TryParse(a.Value, out int userId) && settings.DeniedUsers.Contains(userId)) ?? false;

                return new
                {
                    Decision = denied ? "Deny" : "Permit",
                    Status = new { StatusCode = new { Value = "urn:oasis:names:tc:xacml:1.0:status:ok" } },
                    Category = new[] { Result(SubjectCategory, subject), Result(ActionCategory, action), Result(ResourceCategory, resource) }
                        .Where(c => c is not null),
                };
            }),
        };
    }

    private static XacmlCategory? Find(List<XacmlCategory> categories, List<string> ids)
    {
        return categories.Find(c => c.Id is not null && ids.Contains(c.Id)) ?? (categories.Count == 1 ? categories[0] : null);
    }

    private static object? Result(string categoryId, XacmlCategory? category)
    {
        var attributes = category?.Attribute.Where(a => a.IncludeInResult).ToList();
        return attributes is { Count: > 0 } ? new { CategoryId = categoryId, Attribute = attributes } : null;
    }

    private sealed class XacmlRequestRoot
    {
        public XacmlRequest Request { get; set; } = new();
    }

    private sealed class XacmlRequest
    {
        public List<XacmlCategory> AccessSubject { get; set; } = [];

        public List<XacmlCategory> Action { get; set; } = [];

        public List<XacmlCategory> Resource { get; set; } = [];

        public XacmlMultiRequests? MultiRequests { get; set; }
    }

    private sealed class XacmlMultiRequests
    {
        public List<XacmlRequestReference> RequestReference { get; set; } = [];
    }

    private sealed class XacmlRequestReference
    {
        public List<string> ReferenceId { get; set; } = [];
    }

    private sealed class XacmlCategory
    {
        public string? Id { get; set; }

        public List<XacmlAttribute> Attribute { get; set; } = [];
    }

    private sealed class XacmlAttribute
    {
        public string? AttributeId { get; set; }

        public string? Value { get; set; }

        public string? DataType { get; set; }

        public string? Issuer { get; set; }

        public bool IncludeInResult { get; set; }
    }
}
