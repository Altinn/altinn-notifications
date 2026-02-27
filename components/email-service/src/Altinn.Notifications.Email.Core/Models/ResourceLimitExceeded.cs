using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Email.Core.Models;

/// <summary>
/// A class holding data on an exceeded resource limit in an Altinn service
/// </summary>
public class ResourceLimitExceeded
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
    /// The resource that has reached its capacity limit
    /// </summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp for when the service is available again
    /// </summary>
    public DateTime ResetTime { get; set; }

    /// <summary>
    /// Serialize the <see cref="ResourceLimitExceeded"/> into a json string
    /// </summary>
    /// <returns></returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, _serializerOptions);
    }
}
