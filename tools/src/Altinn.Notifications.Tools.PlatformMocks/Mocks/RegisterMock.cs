using System.Security.Cryptography;
using System.Text;

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

    internal const string PartiesQueryPath = "/register/api/v2/internal/parties/query";

    private const string OrganizationUrn = "urn:altinn:organization:identifier-no:";

    private const string PersonUrn = "urn:altinn:person:identifier-no:";

    public string System => "register";

    public void AddMappings(WireMockServer server)
    {
        server
            .Given(Request.Create().WithPath(NamesLookupPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(request => MockJson.Write(LookupNames(MockJson.Read<NamesLookup>(request.Body)))));

        // The v2 party query the API uses for names since #1948 (Register's internal API). It takes party URNs and answers with the same names as the lookup above.
        server
            .Given(Request.Create().WithPath(PartiesQueryPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(request => MockJson.Write(QueryParties(MockJson.Read<PartyQuery>(request.Body)))));
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

    private static object QueryParties(PartyQuery query)
    {
        return new
        {
            Data = query.Data.Select<string, object?>(urn => urn switch
            {
                _ when urn.StartsWith(OrganizationUrn, StringComparison.OrdinalIgnoreCase) => new
                {
                    PartyType = "organization",
                    PartyUuid = StableUuid(urn),
                    VersionId = 1,
                    DisplayName = OrganizationName(urn[OrganizationUrn.Length..]),
                    OrganizationIdentifier = urn[OrganizationUrn.Length..],
                },
                _ when urn.StartsWith(PersonUrn, StringComparison.OrdinalIgnoreCase) => new
                {
                    PartyType = "person",
                    PartyUuid = StableUuid(urn),
                    VersionId = 1,
                    DisplayName = PersonName(urn[PersonUrn.Length..]),
                    PersonIdentifier = urn[PersonUrn.Length..],
                },
                _ => null,
            }).OfType<object>().ToList(),
        };
    }

    private static Guid StableUuid(string value) => new(MD5.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class PartyQuery
    {
        public List<string> Data { get; set; } = [];
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
