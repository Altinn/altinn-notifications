using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents the response from creating a notification order with reminders.
/// </summary>
public class NotificationOrderReminderResponseExt
{
    /// <summary>
    /// Gets or sets the notification order identifier.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("notificationOrderId")]
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the creation result.
    /// </summary>
    [Required]
    [JsonPropertyOrder(2)]
    [JsonPropertyName("notification")]
    public required NotificationOrderCreationResultExt CreationResult { get; set; }
}
