using System.Text.Json;
using System.Text.Json.Nodes;

namespace Altinn.Notifications.Tools.AcsMock;

/// <summary>
/// Masks personal data in ACS email payloads before they are logged.
/// </summary>
internal static class PayloadMasker
{
    private static readonly HashSet<string> _addressFields = new(StringComparer.OrdinalIgnoreCase) { "address", "recipient" };
    private static readonly HashSet<string> _textFields = new(StringComparer.OrdinalIgnoreCase) { "subject", "plainText", "html", "contentInBase64", "displayName" };

    /// <summary>
    /// Keeps the first character and the domain of each recipient address,
    /// and replaces subjects, bodies, display names and attachments with their length, since they can hold personal data or one-time codes.
    /// </summary>
    public static string Mask(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return string.Empty;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return $"[not JSON, {json.Length} chars]";
        }

        MaskNode(node);
        return node?.ToJsonString() ?? string.Empty;
    }

    /// <summary>
    /// <c>ola.nordmann@example.com</c> becomes <c>o***@example.com</c>.
    /// </summary>
    public static string MaskAddress(string address)
    {
        int at = address.LastIndexOf('@');
        return at <= 0 ? "***" : $"{address[0]}***{address[at..]}";
    }

    private static void MaskNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToList())
                {
                    if (value is JsonValue leaf && leaf.TryGetValue(out string? text))
                    {
                        if (_addressFields.Contains(name))
                        {
                            obj[name] = MaskAddress(text);
                        }
                        else if (_textFields.Contains(name))
                        {
                            obj[name] = $"[masked, {text.Length} chars]";
                        }
                    }
                    else
                    {
                        MaskNode(value);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    MaskNode(item);
                }

                break;
        }
    }
}
