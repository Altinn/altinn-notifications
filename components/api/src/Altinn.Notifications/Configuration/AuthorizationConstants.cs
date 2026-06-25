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
        /// Scope for allowing access to creating email notification orders with file attachments
        /// </summary>
        public const string SCOPE_NOTIFICATIONS_EMAIL_WITH_ATTACHMENTS_CREATE = "altinn:serviceowner/notifications.emailwithattachments.create";

        /// <summary>
        /// Id for the policy requiring the email-with-attachments create scope
        /// </summary>
        public const string POLICY_EMAIL_WITH_ATTACHMENTS_CREATE_SCOPE = "EmailWithAttachmentsCreateScope";
    }
}
