using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Microsoft.Extensions.Logging;

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
    public string? GatewayReference { get; set; } = null;

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
    /// <param name="jsonString">The JSON string to parse.</param>
    /// <param name="result">
    /// When this method returns, contains the parsed <see cref="SmsSendOperationResult"/> object if the parsing succeeded; 
    /// otherwise, a new instance of <see cref="SmsSendOperationResult"/>.
    /// </param>
    /// <returns><c>true</c> if the JSON string was parsed successfully; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string jsonString, out SmsSendOperationResult result)
    {
        result = new SmsSendOperationResult();

        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return false;
        }

        try
        {
            var parsedResult = Deserialize(jsonString);
            if (parsedResult != null)
            {
                result = parsedResult;
            }
        }
        catch
        {
            // Ignore exceptions and return false
        }

        return result.Id != Guid.Empty;
    }
}
