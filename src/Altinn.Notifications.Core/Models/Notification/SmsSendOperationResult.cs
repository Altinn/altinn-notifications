using System.Text.Json;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// A class representing a sms send operation update object
/// </summary>    
public class SmsSendOperationResult
{
    /// <summary>
    /// The notification id
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// The reference to the delivery in sms gateway
    /// </summary>
    public string GatewayReference { get; set; } = string.Empty;

    /// <summary>
    /// The sms send result
    /// </summary>
    public SmsNotificationResultType SendResult { get; set; }

    /// <summary>
    /// Json serializes the <see cref="SmsSendOperationResult"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }

    /// <summary>
    /// Deserialize a json string into the <see cref="SmsSendOperationResult"/>
    /// </summary>
    public static SmsSendOperationResult? Deserialize(string serializedString)
    {
        return JsonSerializer.Deserialize<SmsSendOperationResult>(
            serializedString, JsonSerializerOptionsProvider.Options);
    }

    /// <summary>
    /// Try to parse a json string into a<see cref="SmsSendOperationResult"/>
    /// </summary>
    public static bool TryParse(string input, out SmsSendOperationResult value)
    {
        SmsSendOperationResult? parsedOutput;
        value = new SmsSendOperationResult();

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        try
        {
            parsedOutput = Deserialize(input!);

            value = parsedOutput!;
            return value.NotificationId != Guid.Empty;
        }
        catch
        {
            // try parse, we simply return false if fails
        }

        return false;
    }
}
