using System.Text.Json;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents a message that needs to be retried due to a failed operation.
/// </summary>
public record RetryMessage
{
    /// <summary>
    /// Gets or sets the unique identifier for the delivery report operation id that failed.
    /// </summary>
    public Guid? OperationId { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts made. Defaults to 1.
    /// </summary>
    public int Attempts { get; set; } = 1;

    /// <summary>
    /// Gets or sets the timestamp when the retry message was first created. Defaults to current UTC time.
    /// </summary>
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the unique identifier for the notification that failed to send.
    /// </summary>
    public Guid? NotificationId { get; set; }

    /// <summary>
    /// Gets or sets the delivery report result object to be put on the retry topic.
    /// </summary>
    public string? SendResult { get; set; }

    /// <summary>
    /// Serializes the current instance to a JSON string.
    /// </summary>
    /// <returns></returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}
