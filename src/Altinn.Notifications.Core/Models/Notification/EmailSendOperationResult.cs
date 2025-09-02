using System.Text.Json;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// A class representing an email send operation result object
/// </summary>                              
public class EmailSendOperationResult
{
    /// <summary>
    /// The notification id
    /// </summary>
    public Guid? NotificationId { get; set; }

    /// <summary>
    /// The send operation id
    /// </summary>
    public string OperationId { get; set; } = string.Empty;

    /// <summary>
    /// The email send result
    /// </summary>
    public EmailNotificationResultType? SendResult { get; set; }

    /// <summary>
    /// Json serializes the <see cref="EmailSendOperationResult"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }

    /// <summary>
    /// Deserialize a json string into the <see cref="EmailSendOperationResult"/>
    /// </summary>
    public static EmailSendOperationResult? Deserialize(string serializedString)
    {
        return JsonSerializer.Deserialize<EmailSendOperationResult>(
            serializedString, JsonSerializerOptionsProvider.Options);
    }

    /// <summary>
    /// Try to parse a json string into a<see cref="EmailSendOperationResult"/>
    /// </summary>
    public static bool TryParse(string input, out EmailSendOperationResult value)
    {
        EmailSendOperationResult? parsedOutput;
        value = new EmailSendOperationResult();

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        try
        {
            parsedOutput = Deserialize(input!);

            value = parsedOutput!;
            return (value.NotificationId.HasValue && value.NotificationId.Value != Guid.Empty) || !string.IsNullOrEmpty(value.OperationId);
        }
        catch
        {
            // try parse, we simply return false if fails
        }

        return false;
    }
}
