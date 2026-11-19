using System.Diagnostics;

using OpenTelemetry;

namespace Altinn.Notifications.Shared.Telemetry;

/// <summary>
/// Rewrites Wolverine messaging activity display names to include the message type for easier trace navigation.
/// </summary>
public sealed class WolverineActivityNameProcessor : BaseProcessor<Activity>
{
    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        if (data.Kind is not (ActivityKind.Consumer or ActivityKind.Producer))
        {
            return;
        }

        string? messageType = GetDestinationName(data)
            ?? GetTagValue(data, "messaging.message_type")
            ?? GetTagValue(data, "message_type")
            ?? GetTagValue(data, "wolverine.message_type");

        if (string.IsNullOrWhiteSpace(messageType))
        {
            return;
        }

        string currentName = data.DisplayName;
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return;
        }

        if (!currentName.EndsWith("receive", StringComparison.OrdinalIgnoreCase)
            && !currentName.EndsWith("send", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (currentName.Contains(messageType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        data.DisplayName = $"{currentName} {messageType}";
    }

    private static string? GetDestinationName(Activity activity)
    {
        foreach (var tag in activity.TagObjects)
        {
            if (!string.Equals(tag.Key, "messaging.destination.name", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tag.Key, "messaging.destination", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (tag.Value is Uri uri)
            {
                string queueName = uri.AbsolutePath.Trim('/');
                if (!string.IsNullOrWhiteSpace(queueName))
                {
                    return queueName;
                }

                continue;
            }

            if (tag.Value is string text)
            {
                if (Uri.TryCreate(text, UriKind.Absolute, out Uri? parsedUri))
                {
                    string queueName = parsedUri.AbsolutePath.Trim('/');
                    if (!string.IsNullOrWhiteSpace(queueName))
                    {
                        return queueName;
                    }
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? GetTagValue(Activity activity, string tagName)
    {
        foreach (var tag in activity.Tags)
        {
            if (string.Equals(tag.Key, tagName, StringComparison.OrdinalIgnoreCase))
            {
                return tag.Value;
            }
        }

        return null;
    }
}
