using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text.RegularExpressions;

namespace Altinn.Notifications.Extensions
{
    /// <summary>
    /// Extension class for Uri
    /// </summary>
    public static class UriExtensions
    {
        /// <summary>
        /// Validates a Uri as a Url.
        /// </summary>
        public static bool IsValidUrl(this Uri uri)
        {
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                return false;
            }

            // Check if the URL has a valid host and TLD
            string host = uri.Host;
            string domainPattern = @"^[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            if (Regex.IsMatch(host, domainPattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
            {
                return true;
            }

            return false;
        }
    }
}
