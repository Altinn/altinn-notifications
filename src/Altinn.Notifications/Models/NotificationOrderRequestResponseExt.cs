using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// A class representing a container for an order id.
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class NotificationOrderRequestResponseExt
{
    /// <summary>
    /// The order id
    /// </summary>
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; set; }

    /// <summary>
    /// The recipient lookup summary
    /// </summary>
    [JsonPropertyName("recipientLookup")]
    public RecipientLookup? RecipientLookup { get; set; } 

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderRequestResponseExt"/> class.
    /// </summary>
    public NotificationOrderRequestResponseExt(Guid orderId)
    {
        OrderId = orderId;
    }
}

/// <summary>
/// Class describing a summary of recipient lookup for a notification order
/// </summary>
public class RecipientLookup
{
    /// <summary>
    /// The lookup status
    /// </summary>
    public RecipientLookupStatusExt Status { get; set; }
}

/// <summary>
/// Enum describing the success rate for recipient lookup
/// </summary>
public enum RecipientLookupStatusExt
{
    /// <summary>
    /// The recipient lookup was successful for all recipients
    /// </summary>
    Success,

    /// <summary>
    /// The recipient lookup was successful for some recipients
    /// </summary>
    PartialSuccess,

    /// <summary>
    /// The recipient lookup failed for all recipients
    /// </summary>
    Failed
}
