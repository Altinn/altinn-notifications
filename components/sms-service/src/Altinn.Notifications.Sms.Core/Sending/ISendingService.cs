namespace Altinn.Notifications.Sms.Core.Sending;

/// <summary>
/// Defines the public interface for a service that sends SMS messages.
/// </summary>
public interface ISendingService
{
    /// <summary>
    /// Sends an SMS message to a specified recipient using the default time-to-live (TTL).
    /// </summary>
    /// <param name="sms">An instance of <see cref="Sms"/> containing the message, recipient, sender, and notification ID.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous send operation.</returns>
    Task SendAsync(Sms sms);

    /// <summary>
    /// Sends an SMS message to a specified recipient using a custom time-to-live (TTL).
    /// </summary>
    /// <param name="sms">An instance of <see cref="Sms"/> containing the message, recipient, sender, and notification ID.</param>
    /// <param name="timeToLiveInSeconds">
    /// The time-to-live in seconds, indicating how long the message is valid. 
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous send operation.</returns>
    Task SendAsync(Sms sms, int timeToLiveInSeconds);
}
