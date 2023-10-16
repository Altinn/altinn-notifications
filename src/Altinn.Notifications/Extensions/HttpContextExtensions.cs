namespace Altinn.Notifications.Extensions;

/// <summary>
/// Extensions for HTTP Context
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Get the org identifier string or null if it is not an org.
    /// </summary>        
    public static string? GetOrg(this HttpContext context)
    {
        return context.Items["Org"] as string;
    }
}
