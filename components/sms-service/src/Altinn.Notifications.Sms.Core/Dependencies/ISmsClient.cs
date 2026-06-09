using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Shared;

namespace Altinn.Notifications.Sms.Core.Dependencies;

/// <summary>
/// Represents a contract for a client that can send text messages using an external SMS service provider.
/// </summary>
public interface ISmsClient
{
    /// <summary>
    /// Sends an SMS message to a specified recipient using the default time-to-live.
    /// </summary>
    /// <param name="sms">An instance of <see cref="Sending.Sms"/> containing the message, recipient, sender, and notification ID.</param>
    /// <returns>
    /// A <see cref="Result{T, TError}"/> that represents the outcome of the send operation:
    /// <list type="table">
    ///   <item>On success: a unique string identifier for tracking the message.</item>
    ///   <item>On failure: a <see cref="SmsClientErrorResponse"/> providing details about the error.</item>
    /// </list>
    /// </returns>
    Task<Result<string, SmsClientErrorResponse>> SendAsync(Sending.Sms sms);

    /// <summary>
    /// Sends an SMS message to a specified recipient using a custom time-to-live (TTL).
    /// </summary>
    /// <param name="sms">An instance of <see cref="Sending.Sms"/> containing the message, recipient, sender, and notification ID.</param>
    /// <param name="timeToLiveInSeconds">
    /// The time-to-live in seconds, indicating how long the message is valid.
    /// </param>
    /// <returns>
    /// A <see cref="Result{T, TError}"/> that represents the outcome of the send operation:
    /// <list type="table">
    ///   <item>On success: a unique string identifier for tracking the message.</item>
    ///   <item>On failure: a <see cref="SmsClientErrorResponse"/> providing details about the error.</item>
    /// </list>
    /// </returns>
    Task<Result<string, SmsClientErrorResponse>> SendAsync(Sending.Sms sms, int timeToLiveInSeconds);
}
