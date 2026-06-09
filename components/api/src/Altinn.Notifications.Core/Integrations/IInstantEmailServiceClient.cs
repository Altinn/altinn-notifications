using Altinn.Notifications.Core.Models.InstantEmailService;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines a client for sending instant emails.
/// </summary>
public interface IInstantEmailServiceClient
{
    /// <summary>
    /// Sends an instant email.
    /// </summary>
    /// <param name="instantEmail">
    /// The <see cref="InstantEmail"/> payload containing email content, recipient information,
    /// sender identifier, notification correlation, and delivery constraints.
    /// </param>
    /// <returns>
    /// A <see cref="Task{InstantEmailSendResult}"/> representing the asynchronous send operation.
    /// The result contains:
    /// <list type="table">
    ///   <item>
    ///     <description>
    ///       <see cref="InstantEmailSendResult.Success"/>: <c>true</c> if the email was accepted for delivery; <c>false</c> otherwise.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="InstantEmailSendResult.StatusCode"/>: The HTTP status code returned by the email service provider.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="InstantEmailSendResult.ErrorDetails"/>: Problem details describing any error encountered during the send attempt.
    ///     </description>
    ///   </item>
    /// </list>
    /// </returns>
    Task<InstantEmailSendResult> SendAsync(InstantEmail instantEmail);
}
