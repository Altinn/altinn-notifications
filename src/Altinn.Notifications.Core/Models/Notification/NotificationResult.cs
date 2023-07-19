namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Notification result class
/// </summary>
public class NotificationResult<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationResult{TEnum}"/> class.
    /// </summary>
    public NotificationResult(TEnum result)
    {
        ResultTime = DateTime.UtcNow;
        Result = result;
    }

    /// <summary>
    /// Gets the date and time for when the last result was set.
    /// </summary>
    public DateTime ResultTime { get; }

    /// <summary>
    /// Gets the send result of the notification
    /// </summary>
    public TEnum Result { get; }
}