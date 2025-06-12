using System.Text.RegularExpressions;

namespace Altinn.Notifications.Persistence.Utilities
{
    /// <summary>
    /// Helper class containing utility methods for the Notifications persistence layer.
    /// </summary>
    internal static partial class Helpers
    {
        /// <summary>
        /// A regular expression pattern to detect mobile numbers.
        /// </summary>
        /// <returns>
        /// A <see cref="Regex"/> that matches mobile number formats, accepting:
        /// <list type="bullet">
        ///   <item><description>Optional leading '+' or '00' international prefix</description></item>
        ///   <item><description>One or more digits</description></item>
        /// </list>
        /// </returns>
        [GeneratedRegex(@"^(?:\+|00)?[1-9]\d{1,14}$")]
        internal static partial Regex MobileNumbersRegex();
    }
}
