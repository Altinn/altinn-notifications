using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Altinn.Notifications.Tools.PlatformMocks.Mocks;

/// <summary>
/// The name lookup in Altinn Register, used to fill in <c>$recipientName$</c>. Every party it is asked about has a name.
/// </summary>
internal sealed class RegisterMock : IPlatformMock
{
    internal const string NamesLookupPath = "/register/api/v1/parties/nameslookup";

    public string System => "register";

    public void AddMappings(WireMockServer server)
    {
        server
            .Given(Request.Create().WithPath(NamesLookupPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(request => MockJson.Write(LookupNames(MockJson.Read<NamesLookup>(request.Body)))));
    }

    internal static string PersonName(string nationalIdentityNumber) => $"Testperson {nationalIdentityNumber}";

    internal static string OrganizationName(string organizationNumber) => $"Testorganisasjon {organizationNumber} AS";

    private static object LookupNames(NamesLookup lookup)
    {
        return new
        {
            PartyNames = lookup.Parties.Select(party => party.OrgNo is { Length: > 0 } organizationNumber
                ? new Party { Name = OrganizationName(organizationNumber), OrgNo = organizationNumber }
                : new Party { Name = PersonName(party.Ssn ?? string.Empty), Ssn = party.Ssn }),
        };
    }

    private sealed class NamesLookup
    {
        public List<Party> Parties { get; set; } = [];
    }

    private sealed class Party
    {
        public string? Name { get; set; }

        public string? OrgNo { get; set; }

        public string? Ssn { get; set; }
    }
}
