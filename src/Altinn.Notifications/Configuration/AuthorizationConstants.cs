namespace Altinn.Notifications.Configuration;

/// <summary>
/// Constants used in authorization of access to Notifications.
/// </summary>
public static class AuthorizationConstants
{
    /// <summary>
    /// Id for policy for allowing configuration administration
    /// </summary>
    public const string POLICY_SCOPE_CONFIG_ADMIN = "PolicyScopeConfigAdmin";

    /// <summary>
    /// Scope for allowing administration of configuration
    /// </summary>
    public const string SCOPE_CONFIG_ADMIN = "altinn:notifications.config.admin";
}
