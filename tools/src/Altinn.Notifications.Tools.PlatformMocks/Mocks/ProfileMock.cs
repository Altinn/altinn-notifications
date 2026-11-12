using Altinn.Notifications.Tools.PlatformMocks.Configuration;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Altinn.Notifications.Tools.PlatformMocks.Mocks;

/// <summary>
/// The contact point lookups in Altinn Profile. Every person and organisation it is asked about has contact points, so any test
/// data works, except the identifiers listed in <see cref="ProfileMockSettings"/>.
/// </summary>
internal sealed class ProfileMock(ProfileMockSettings settings) : IPlatformMock
{
    internal const string BasePath = "/profile/api/v1/";

    public string System => "profile";

    public void AddMappings(WireMockServer server)
    {
        Map(server, "users/contactpoint/lookup", body => LookupPersons(MockJson.Read<PersonLookup>(body)));
        Map(server, "users/contactpoint/lookupsi", body => LookupExternalIdentities(MockJson.Read<ExternalIdentityLookup>(body)));
        Map(server, "organizations/notificationaddresses/lookup", body => LookupOrganizations(MockJson.Read<OrganizationLookup>(body)));
        Map(server, "units/contactpoint/lookup", body => LookupRegisteredUsers(MockJson.Read<OrganizationLookup>(body)));
    }

    internal static string Email(string identifier) => $"{identifier}@example.com";

    internal static int PartyId(string organizationNumber) => MockJson.StableId(organizationNumber, 50_000_000);

    internal static int UserId(string nationalIdentityNumber) => MockJson.StableId(nationalIdentityNumber, 20_000_000);

    /// <summary>
    /// The one user with registered contact points for an organisation. The national identity number is made from the organisation number.
    /// </summary>
    internal static string RegisteredUser(string organizationNumber) => organizationNumber + "01";

    private static void Map(WireMockServer server, string path, Func<string?, object> respond)
    {
        server
            .Given(Request.Create().WithPath(BasePath + path).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(request => MockJson.Write(respond(request.Body))));
    }

    private object LookupPersons(PersonLookup lookup)
    {
        return new { ContactPointsList = lookup.NationalIdentityNumbers.Select(Person) };
    }

    private object LookupExternalIdentities(ExternalIdentityLookup lookup)
    {
        return new
        {
            ContactPointsList = lookup.ExternalIdentities.Select(identity =>
            {
                // ID-porten email identities have only the email in the identity. Others, like legacy usernames, get the usual contact points.
                string last = identity[(identity.LastIndexOf(':') + 1)..];
                bool isEmailIdentity = identity.Contains(":idporten-email:", StringComparison.OrdinalIgnoreCase);
                return new
                {
                    ExternalIdentity = identity,
                    Email = isEmailIdentity ? last : Email(last),
                    MobileNumber = isEmailIdentity ? string.Empty : settings.MobileNumber,
                };
            }),
        };
    }

    private object LookupOrganizations(OrganizationLookup lookup)
    {
        return new
        {
            ContactPointsList = lookup.OrganizationNumbers.Select(organizationNumber =>
            {
                bool hasContactPoints = !settings.OrganizationsWithoutContactPoints.Contains(organizationNumber);
                return new
                {
                    OrganizationNumber = organizationNumber,
                    PartyId = PartyId(organizationNumber),
                    EmailList = hasContactPoints ? [Email(organizationNumber)] : Array.Empty<string>(),
                    MobileNumberList = hasContactPoints ? [settings.MobileNumber] : Array.Empty<string>(),
                };
            }),
        };
    }

    private object LookupRegisteredUsers(OrganizationLookup lookup)
    {
        return new
        {
            ContactPointsList = lookup.OrganizationNumbers.Select(organizationNumber => new
            {
                OrganizationNumber = organizationNumber,
                PartyId = PartyId(organizationNumber),
                EmailList = Array.Empty<string>(),
                MobileNumberList = Array.Empty<string>(),
                UserContactPoints = settings.OrganizationsWithoutContactPoints.Contains(organizationNumber)
                    ? []
                    : new[] { Person(RegisteredUser(organizationNumber)) },
            }),
        };
    }

    private object Person(string nationalIdentityNumber)
    {
        bool hasContactPoints = !settings.PersonsWithoutContactPoints.Contains(nationalIdentityNumber);
        return new
        {
            UserId = UserId(nationalIdentityNumber),
            NationalIdentityNumber = nationalIdentityNumber,
            IsReserved = settings.ReservedPersons.Contains(nationalIdentityNumber),
            MobileNumber = hasContactPoints ? settings.MobileNumber : string.Empty,
            Email = hasContactPoints ? Email(nationalIdentityNumber) : string.Empty,
        };
    }

    private sealed class PersonLookup
    {
        public List<string> NationalIdentityNumbers { get; set; } = [];
    }

    private sealed class ExternalIdentityLookup
    {
        public List<string> ExternalIdentities { get; set; } = [];
    }

    private sealed class OrganizationLookup
    {
        public List<string> OrganizationNumbers { get; set; } = [];
    }
}
