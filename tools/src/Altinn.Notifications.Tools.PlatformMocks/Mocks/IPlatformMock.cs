using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using WireMock.Server;

namespace Altinn.Notifications.Tools.PlatformMocks.Mocks;

/// <summary>
/// One mocked platform system, served by its own WireMock server.
/// </summary>
internal interface IPlatformMock
{
    /// <summary>
    /// The system name, used in the log and in the simulator's <c>/intercept/{system}/</c> path.
    /// </summary>
    string System { get; }

    /// <summary>
    /// Adds the system's endpoints to the server.
    /// </summary>
    void AddMappings(WireMockServer server);
}

/// <summary>
/// Helpers the mocks share.
/// </summary>
internal static class MockJson
{
    /// <summary>
    /// Case-insensitive in, camelCase out, like the real platform APIs. Relaxed escaping keeps + in mobile numbers readable in the log.
    /// </summary>
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static T Read<T>(string? body)
        where T : new()
    {
        return string.IsNullOrWhiteSpace(body) ? new T() : JsonSerializer.Deserialize<T>(body, Options) ?? new T();
    }

    internal static string Write(object value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(value, options ?? Options);
    }

    /// <summary>
    /// A positive id that is the same for an identifier every time, so a party or user keeps its id across requests and restarts.
    /// </summary>
    internal static int StableId(string identifier, int offset)
    {
        // FNV-1a, since string.GetHashCode differs between processes.
        uint hash = 2166136261;
        foreach (char c in identifier)
        {
            hash = (hash ^ c) * 16777619;
        }

        return offset + (int)(hash % 10_000_000);
    }
}
