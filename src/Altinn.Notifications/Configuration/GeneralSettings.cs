namespace Altinn.Notifications.Configuration
{
    /// <summary>
    /// Represents settings needed by the application for various purposes...
    /// </summary>
    public class GeneralSettings
    {
        /// <summary>
        /// Gets or sets the name of the JSON Web Token cookie.
        /// </summary>
        public string JwtCookieName { get; set; } = string.Empty;
    }
}
