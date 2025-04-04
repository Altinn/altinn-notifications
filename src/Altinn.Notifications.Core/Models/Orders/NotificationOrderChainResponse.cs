namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the response returned after successfully creating a notification order chain.
/// </summary>
/// <remarks>
/// This class encapsulates the confirmation details provided to clients upon successful creation 
/// of a notification order with optional reminders. It contains both the unique identifier for the 
/// notification order chain itself and a detailed receipt with tracking information for each component of the notification chain.
/// </remarks>
public class NotificationOrderChainResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the notification order chain itself.
    /// </summary>
    /// <remarks>
    /// This identifier serves as the primary key to reference the entire notification chain
    /// </remarks>
    public required Guid OrderChainId { get; set; }

    /// <summary>
    /// Gets or sets the detailed receipt for the notification order creation.
    /// </summary>
    /// <remarks>
    /// This receipt provides all the necessary identifiers to track each component of the notification chain separately
    /// </remarks>
    public required NotificationOrderChainReceipt OrderChainReceipt { get; set; }
}
