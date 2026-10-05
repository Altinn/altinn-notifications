using System.Diagnostics;

using OpenTelemetry;

namespace Altinn.Notifications.Shared.Telemetry;

/// <summary>
/// Rewrites Wolverine messaging activity display names to include the message type for easier trace navigation.
/// </summary>
public sealed class WolverineActivityNameProcessor : BaseProcessor<Activity>
{
    /// <inheritdoc/>
    public override void OnEnd(Activity activity)
    {
        if (activity.Kind is not (ActivityKind.Consumer or ActivityKind.Producer))
        {
            return;
        }

        string? messageType = GetTagValue(activity, "messaging.message.type")
            ?? GetTagValue(activity, "message.type")
            ?? GetTagValue(activity, "wolverine.message_type")
            ?? GetTagValue(activity, "messaging.destination.name")
            ?? GetTagValue(activity, "messaging.destination");

        if (string.IsNullOrWhiteSpace(messageType))
        {
            return;
        }

        string currentName = activity.DisplayName;
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return;
        }

        if (!currentName.EndsWith(" receive", StringComparison.OrdinalIgnoreCase)
            && !currentName.EndsWith(" send", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (currentName.Contains(messageType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        activity.DisplayName = $"{currentName} {messageType}";
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
