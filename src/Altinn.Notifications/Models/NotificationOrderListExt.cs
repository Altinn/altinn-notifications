namespace Altinn.Notifications.Models;

/// <summary>
/// A class representing a list of notification order. 
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class NotificationOrderListExt
{
    /// <summary>
    /// Gets or sets the number of orders in the list
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the list of notification orders
    /// </summary>
    public List<NotificationOrderExt> Orders { get; set; } = new List<NotificationOrderExt>();
}