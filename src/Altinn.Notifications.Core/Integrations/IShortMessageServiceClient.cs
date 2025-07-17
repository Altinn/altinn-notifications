using Altinn.Notifications.Core.Models.ShortMessageService;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines a client for sending short text messages through the Altinn Notifications SMS service.
/// </summary>
public interface IShortMessageServiceClient
{
    /// <summary>
    /// Sends a text message using the Altinn Notifications SMS service.
    /// </summary>
    /// <param name="shortMessage">The message payload.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains:
    /// - Success: <c>true</c> if the message was successfully accepted by the service provider
    /// - StatusCode: The status code returned by the service
    /// - ErrorDetails: Problem details if the request failed
    /// </returns>
    public Task<ShortMessageSendResult> SendAsync(ShortMessage shortMessage);
}
