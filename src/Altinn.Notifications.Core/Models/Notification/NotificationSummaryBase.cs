namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// An interface representing a summary of the notifications related to an order
/// </summary>
public class NotificationSummaryBase<TClass>
    where TClass : class
{
    /// <summary>
    /// Gets the notification order id
    /// </summary>   
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets the senders reference of the notification order
    /// </summary>    
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets the number of generated notifications
    /// </summary>    
    public int Generated { get; set; }

    /// <summary>
    /// Gets the number of succeeeded notifications
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Gets the list of notifications with send result
    /// </summary>
    public List<TClass> Notifications { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationSummaryBase{TClass}"/> class.
    /// </summary>
    public NotificationSummaryBase(Guid orderId)
    {
        OrderId = orderId;
        Notifications = [];
    }
}
