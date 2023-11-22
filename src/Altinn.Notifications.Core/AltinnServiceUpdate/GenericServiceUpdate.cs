using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.AltinnServiceUpdate
{
    /// <summary>
    /// A class representing a generic service update
    /// </summary>
    public class GenericServiceUpdate
    {
        /// <summary>
        /// The source of the service update
        /// </summary>
        public AltinnService Source { get; set; }

        /// <summary>
        /// The schema of the service update data
        /// </summary>
        public AltinnServiceUpdateSchema Schema { get; set; }

        /// <summary>
        /// The data of the service update as a json serialized string
        /// </summary>
        public string Data { get; set; } = string.Empty;

        /// <summary>
        /// Deserialize a json string into the <see cref="GenericServiceUpdate"/>
        /// </summary>
        public static GenericServiceUpdate? Deserialize(string serializedString)
        {
            return JsonSerializer.Deserialize<GenericServiceUpdate>(
                serializedString,
                new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });
        }

        /// <summary>
        /// Try to parse a json string into a<see cref="GenericServiceUpdate"/>
        /// </summary>
        public static bool TryParse(string input, out GenericServiceUpdate value)
        {
            GenericServiceUpdate? parsedOutput;
            value = new GenericServiceUpdate();

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            try
            {
                parsedOutput = Deserialize(input!);

                value = parsedOutput!;
                return value.Source != AltinnService.Unknown;
            }
            catch
            {
                // try parse, we simply return false if fails
            }

            return false;
        }
    }
}
