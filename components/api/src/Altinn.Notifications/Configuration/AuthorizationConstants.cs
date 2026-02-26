namespace Altinn.Notifications.Configuration
{
    /// <summary>
    /// Constants related to authorization of notifications
    /// </summary>
    public static class AuthorizationConstants
    {
        /// <summary>
        /// Id for the policy requiring create scope or access platform access token
        /// </summary>
        public const string POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS = "CreateScopeOrPlatfomAccessToken";

        /// <summary>
        /// Scope for allowing access to creating notifications
        /// </summary>
        public const string SCOPE_NOTIFICATIONS_CREATE = "altinn:serviceowner/notifications.create";
    }
}
