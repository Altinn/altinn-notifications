using Altinn.Notifications.Core.Models.ShortMessageService;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines a client for sending text messages through the Altinn Notifications SMS service.
/// </summary>
public interface IShortMessageServiceClient
{
    /// <summary>
    /// Sends a text message using the Altinn Notifications short message service.
    /// </summary>
    /// <param name="shortMessage">The message payload.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains:
    /// - Success: <c>true</c> if the message was successfully accepted by the service provider (HTTP 200)
    /// - StatusCode: The HTTP status code returned by the service
    /// - ErrorDetails: Problem details if the request failed (HTTP 400 or 499)
    /// </returns>
    public Task<ShortMessageSendResult> Send(ShortMessage shortMessage);
}
