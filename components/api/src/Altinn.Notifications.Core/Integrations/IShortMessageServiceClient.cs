using Altinn.Notifications.Core.Models.ShortMessageService;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines a client for sending short text messages.
/// </summary>
public interface IShortMessageServiceClient
{
    /// <summary>
    /// Sends a text message.
    /// </summary>
    /// <param name="shortMessage">
    /// The <see cref="ShortMessage"/> payload containing message content, recipient information,
    /// sender identifier, notification correlation, and delivery constraints.
    /// </param>
    /// <returns>
    /// A <see cref="Task{ShortMessageSendResult}"/> representing the asynchronous send operation.
    /// The result contains:
    /// <list type="table">
    ///   <item>
    ///     <description>
    ///       <see cref="ShortMessageSendResult.Success"/>: <c>true</c> if the message was accepted for delivery; <c>false</c> otherwise.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="ShortMessageSendResult.StatusCode"/>: The HTTP status code returned by the SMS service provider.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="ShortMessageSendResult.ErrorDetails"/>: Problem details describing any error encountered during the send attempt.
    ///     </description>
    ///   </item>
    /// </list>
    /// </returns>
    Task<ShortMessageSendResult> SendAsync(ShortMessage shortMessage);
}
