namespace Altinn.Notifications.Email.Core.Models;

/// <summary>
/// Command requesting a status check for an email that has been submitted to
/// Azure Communication Services (ACS). The handler will poll ACS for the
/// current delivery status and either complete the flow or re-issue the
/// command for another poll cycle.
/// </summary>
public class CheckEmailSendStatusCommand
{
    /// <summary>
    /// Gets the notification identifier that this email belongs to.
    /// </summary>
    public required Guid NotificationId { get; init; }

    /// <summary>
    /// Gets the ACS send operation identifier returned when the email was submitted.
    /// Used to query ACS for the current delivery status.
    /// </summary>
    public required string SendOperationId { get; init; }

    /// <summary>
    /// Gets the UTC timestamp of the most recent status poll against ACS.
    /// Used to enforce a minimum delay between consecutive polls.
    /// </summary>
    public required DateTime LastCheckedAtUtc { get; init; }
}
