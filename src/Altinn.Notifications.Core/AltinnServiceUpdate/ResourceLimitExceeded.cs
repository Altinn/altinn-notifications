using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.AltinnServiceUpdate
{
    /// <summary>
    /// A class holding data on an exceeded resource limit in an Altinn service
    /// </summary>
    public class ResourceLimitExceeded
    {
        /// <summary>
        /// The resource that has reached its capacity limit
        /// </summary>
        public string Resource { get; set; } = string.Empty;

        /// <summary>
        /// The timeout in seconds until service is available again
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// The error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Deserialize a json string into the <see cref="ResourceLimitExceeded"/>
        /// </summary>
        public static ResourceLimitExceeded? Deserialize(string serializedString)
        {
            return JsonSerializer.Deserialize<ResourceLimitExceeded>(
                serializedString,
                new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });
        }

        /// <summary>
        /// Try to parse a json string into a<see cref="ResourceLimitExceeded"/>
        /// </summary>
        public static bool Tryparse(string input, out ResourceLimitExceeded value)
        {
            ResourceLimitExceeded? parsedOutput;
            value = new ResourceLimitExceeded();

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            try
            {
                parsedOutput = Deserialize(input!);

                value = parsedOutput!;
                return !string.IsNullOrEmpty(value.Resource);
            }
            catch
            {
                // try parse, we simply return false if fails
            }

            return false;
        }
    }
}
