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

        /// <summary>
        /// Scope for allowing access to endpoints used in Altinn support dashboard
        /// </summary>
        public const string SCOPE_SUPPORT_DASHBOARD_ACCESS = "altinn:notifications.support.admin";

        /// <summary>
        /// Policy for allowing access to endpoints used in Altinn support dashboard
        /// </summary>
        public const string POLICY_SUPPORT_DASHBOARD_ACCESS = "SupportDashboardAccess";

        /// <summary>
        /// Scope for allowing access to creating composed email notification orders
        /// </summary>
        public const string SCOPE_NOTIFICATIONS_COMPOSED_EMAIL_CREATE = "altinn:serviceowner/notifications.composedemail.create";

        /// <summary>
        /// Id for the policy requiring the composed email create scope
        /// </summary>
        public const string POLICY_COMPOSED_EMAIL_CREATE_SCOPE = "ComposedEmailCreateScope";
    }
}
