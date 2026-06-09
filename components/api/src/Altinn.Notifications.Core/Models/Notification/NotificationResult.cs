namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Represents the result of the notification send operation.
/// </summary>
/// <typeparam name="TEnum">The type of the enumeration used to represent the notification send status.</typeparam>
public class NotificationResult<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationResult{TEnum}"/> class.
    /// </summary>
    /// <param name="result">The result of the notification send operation.</param>
    /// <param name="resultTime">The date and time when the result was set.</param>
    public NotificationResult(TEnum result, DateTime resultTime)
    {
        Result = result;
        ResultTime = resultTime;
    }

    /// <summary>
    /// Gets the date and time when the result was set.
    /// </summary>
    public DateTime ResultTime { get; }

    /// <summary>
    /// Gets the result of the notification send operation.
    /// </summary>
    public TEnum Result { get; }

    /// <summary>
    /// Gets the description of the send result.
    /// </summary>
    public string? ResultDescription { get; private set; }

    /// <summary>
    /// Sets the description of the send result.
    /// </summary>
    /// <param name="description">The description of the send result.</param>
    public void SetResultDescription(string? description)
    {
        ResultDescription = description;
    }
}
