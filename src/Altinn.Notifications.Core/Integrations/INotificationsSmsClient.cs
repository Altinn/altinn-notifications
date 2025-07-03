using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Interface describing a client to interact with the Altinn Notifications SMS service for sending SMS.
/// </summary>
public interface INotificationsSmsClient
{
    /// <summary>
    /// Sends an SMS message using the Notifications SMS service.
    /// </summary>
    /// <param name="instantSmsPayload">The payload containing SMS details including sender,
    /// message content, recipient, time-to-live duration, and notification identifier.</param>
    /// <returns>A boolean value indicating the success (true) or failure (false) of the SMS delivery attempt.</returns>
    public Task<bool> Send(InstantSmsPayload instantSmsPayload);
}
