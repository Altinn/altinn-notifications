using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Email.Core.Models;

/// <summary>
/// A class representing a generic service update
/// </summary>
public class GenericServiceUpdate
{
    private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// The source of the service update
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// The schema of the service update data
    /// </summary>
    public AltinnServiceUpdateSchema Schema { get; set; }

    /// <summary>
    /// The data of the service update as a json serialized string
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Serialize the <see cref="GenericServiceUpdate"/> into a json string
    /// </summary>
    /// <returns></returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, _serializerOptions);
    }
}
