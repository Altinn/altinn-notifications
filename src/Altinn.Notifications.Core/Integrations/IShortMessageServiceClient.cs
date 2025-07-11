using Altinn.Notifications.Core.Models.ShortMessageService;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Represents an abstraction for sending short text messages (SMS) through the Altinn Notifications SMS service.
/// Implementations of this interface are responsible for communicating with external SMS providers,
/// handling message delivery, and reporting the outcome of each send attempt.
/// </summary>
public interface IShortMessageServiceClient
{
    /// <summary>
    /// Asynchronously sends a short text message to a single recipient using the Altinn Notifications SMS service.
    /// </summary>
    /// <param name="shortMessage">
    /// The <see cref="ShortMessage"/> payload containing message content, recipient information,
    /// sender identifier, notification correlation, and delivery constraints.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> that can be used to cancel the send operation.
    /// Defaults to <see cref="CancellationToken.None"/> if not specified.
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
    Task<ShortMessageSendResult> SendAsync(ShortMessage shortMessage, CancellationToken cancellationToken = default);
}
