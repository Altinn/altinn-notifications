using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.AltinnServiceUpdate
{
    /// <summary>
    /// A class representing a service update of the type Resource Limit
    /// </summary>
    public class ResourceLimitUpdate 
    {
        /// <summary>
        /// The resource that is limited
        /// </summary>
        public string Resource { get; set; }

        /// <summary>
        /// The timeout in seconds until service is available again
        /// </summary>
        public int Timeout { get; set; }
        
        /// <summary>
        /// The error message
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Deserialize a json string into the <see cref="ResourceLimitUpdate"/>
        /// </summary>
        public static ResourceLimitUpdate? Deserialize(string serializedString)
        {
            return JsonSerializer.Deserialize<ResourceLimitUpdate>(
                serializedString,
                new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });
        }

        /// <summary>
        /// Try to parse a json string into a<see cref="ResourceLimitUpdate"/>
        /// </summary>
        public static bool Tryparse(string input, out ResourceLimitUpdate value)
        {
            ResourceLimitUpdate? parsedOutput;
            value = new ResourceLimitUpdate();

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
