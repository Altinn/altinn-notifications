using System.Text.Json;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Represents the result of an SMS send operation.
/// </summary>
public class SmsSendOperationResult
{
    /// <summary>
    /// Gets or sets the unique identifier of the SMS notification.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the reference to the delivery in the SMS gateway.
    /// </summary>
    public string GatewayReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the result of the SMS send operation.
    /// </summary>
    public SmsNotificationResultType SendResult { get; set; }

    /// <summary>
    /// Serializes the <see cref="SmsSendOperationResult"/> object to a JSON string.
    /// </summary>
    /// <returns>A JSON string representation of the <see cref="SmsSendOperationResult"/> object.</returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }

    /// <summary>
    /// Deserializes a JSON string into an <see cref="SmsSendOperationResult"/> object.
    /// </summary>
    /// <param name="serializedString">The JSON string to deserialize.</param>
    /// <returns>An <see cref="SmsSendOperationResult"/> object.</returns>
    public static SmsSendOperationResult? Deserialize(string serializedString)
    {
        return JsonSerializer.Deserialize<SmsSendOperationResult>(serializedString, JsonSerializerOptionsProvider.Options);
    }

    /// <summary>
    /// Tries to parse a JSON string into an <see cref="SmsSendOperationResult"/> object.
    /// </summary>
    /// <param name="input">The JSON string to parse.</param>
    /// <param name="value">When this method returns, contains the parsed <see cref="SmsSendOperationResult"/> object, if the parsing succeeded, or a new instance of <see cref="SmsSendOperationResult"/> if the parsing failed.</param>
    /// <returns><c>true</c> if the JSON string was parsed successfully; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string input, out SmsSendOperationResult value)
    {
        value = new SmsSendOperationResult();

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        try
        {
            var parsedOutput = Deserialize(input);
            if (parsedOutput != null)
            {
                value = parsedOutput;
                return value.Id != Guid.Empty || !string.IsNullOrEmpty(value.GatewayReference);
            }
        }
        catch
        {
            // Ignore exceptions and return false
        }

        return false;
    }
}
