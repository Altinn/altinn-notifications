namespace Altinn.Notifications.Tools.PlatformMocks.Configuration;

/// <summary>
/// Settings for the local mocks of the Altinn platform APIs the notifications API calls.
/// </summary>
public class PlatformMocksSettings
{
    /// <summary>
    /// Where requests a mock does not recognise go, as <c>{FallbackUrl}{system}/{path}</c>, e.g. the simulator's
    /// <c>http://localhost:5096/intercept/</c>. Empty means a plain 404.
    /// </summary>
    public string FallbackUrl { get; set; } = string.Empty;

    public ProfileMockSettings Profile { get; set; } = new() { Port = 5131 };

    public MockServerSettings Register { get; set; } = new() { Port = 5121 };

    public AuthorizationMockSettings Authorization { get; set; } = new() { Port = 5151 };

    public MockServerSettings Conditions { get; set; } = new() { Port = 5199 };

    public MockServerSettings Maskinporten { get; set; } = new() { Port = 5197 };
}

/// <summary>
/// Settings every mock server has.
/// </summary>
public class MockServerSettings
{
    /// <summary>
    /// Whether the server is started at all.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The localhost port the server listens on.
    /// </summary>
    public int Port { get; set; }
}

/// <summary>
/// Settings for the Profile mock. Every person and organisation has contact points, except the ones listed here.
/// </summary>
public class ProfileMockSettings : MockServerSettings
{
    /// <summary>
    /// The mobile number every person, organisation and registered user gets. Email addresses are made from the identifier instead,
    /// but a mobile number has to be valid, so it is one fixed number that is safe to send to.
    /// </summary>
    public string MobileNumber { get; set; } = "+4799999999";

    /// <summary>
    /// National identity numbers that are reserved against electronic contact.
    /// </summary>
    public List<string> ReservedPersons { get; set; } = [];

    /// <summary>
    /// National identity numbers with neither email nor mobile number.
    /// </summary>
    public List<string> PersonsWithoutContactPoints { get; set; } = [];

    /// <summary>
    /// Organisation numbers with no notification addresses and no users with registered contact points.
    /// </summary>
    public List<string> OrganizationsWithoutContactPoints { get; set; } = [];
}

/// <summary>
/// Settings for the Authorization mock, which permits everything except the users listed here.
/// </summary>
public class AuthorizationMockSettings : MockServerSettings
{
    /// <summary>
    /// User ids that get <c>Deny</c>, so their registered contact points are filtered out.
    /// </summary>
    public List<int> DeniedUsers { get; set; } = [];
}
