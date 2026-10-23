using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Tools.Simulator.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.Simulator;

/// <summary>
/// A predefined answer from <c>templates/&lt;system&gt;/&lt;id&gt;.json</c>. <see cref="Key"/> is <c>&lt;system&gt;/&lt;id&gt;</c>.
/// </summary>
internal sealed record ResponseTemplate(string Key, string System, string Name, int Status, IReadOnlyDictionary<string, string> Headers, string Body)
{
    /// <summary>
    /// Fills in <c>{{guid}}</c> with a new id and <c>{{now}}</c> with the current time, in the headers and the body.
    /// </summary>
    public SimulatedResponse Render(DateTimeOffset now)
    {
        string guid = Guid.NewGuid().ToString();
        string Fill(string text) => text.Replace("{{guid}}", guid).Replace("{{now}}", now.UtcDateTime.ToString("O"));
        return new SimulatedResponse(Status, Headers.ToDictionary(h => h.Key, h => Fill(h.Value)), Fill(Body));
    }
}

/// <summary>
/// Reads the templates from disk on every call, so a template can be added or changed while the simulator runs.
/// </summary>
internal sealed class TemplateStore(IOptions<SimulatorSettings> options, ILogger<TemplateStore> logger)
{
    /// <summary>
    /// The folder with templates every system can use.
    /// </summary>
    public const string Generic = "generic";

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };
    private static readonly JsonSerializerOptions _bodyOptions = new() { WriteIndented = true };

    /// <summary>
    /// All templates, ordered by system and file name.
    /// </summary>
    public IReadOnlyList<ResponseTemplate> GetAll()
    {
        string root = Path.Combine(AppContext.BaseDirectory, options.Value.TemplatesPath);
        if (!Directory.Exists(root))
        {
            logger.LogWarning("Template folder {Folder} does not exist", root);
            return [];
        }

        List<ResponseTemplate> templates = [];
        foreach (string folder in Directory.GetDirectories(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string file in Directory.GetFiles(folder, "*.json").Order(StringComparer.OrdinalIgnoreCase))
            {
                if (Read(Path.GetFileName(folder), file) is { } template)
                {
                    templates.Add(template);
                }
            }
        }

        return templates;
    }

    /// <summary>
    /// The templates for <paramref name="system"/>, followed by the generic ones.
    /// </summary>
    public IReadOnlyList<ResponseTemplate> For(string system) =>
        [.. GetAll().Where(t => t.System.Equals(system, StringComparison.OrdinalIgnoreCase)), .. GetAll().Where(t => t.System == Generic)];

    public ResponseTemplate? Find(string key) => GetAll().FirstOrDefault(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private ResponseTemplate? Read(string system, string file)
    {
        try
        {
            var content = JsonSerializer.Deserialize<TemplateFile>(File.ReadAllText(file), _jsonOptions) ?? new TemplateFile();
            string body = content.Body.ValueKind switch
            {
                JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
                JsonValueKind.String => content.Body.GetString() ?? string.Empty,
                _ => JsonSerializer.Serialize(content.Body, _bodyOptions),
            };

            string id = Path.GetFileNameWithoutExtension(file);
            return new ResponseTemplate($"{system}/{id}", system, content.Name ?? id, content.Status, content.Headers ?? [], body);
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "Skipping template {File}, which is not valid JSON", file);
            return null;
        }
    }

    private sealed class TemplateFile
    {
        public string? Name { get; set; }

        public int Status { get; set; } = StatusCodes.Status200OK;

        public Dictionary<string, string>? Headers { get; set; }

        // A JSON body is written as JSON, anything else (XML, plain text) as a string.
        [JsonPropertyName("body")]
        public JsonElement Body { get; set; }
    }
}
