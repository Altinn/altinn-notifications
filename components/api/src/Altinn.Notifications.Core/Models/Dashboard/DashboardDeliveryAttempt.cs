namespace Altinn.Notifications.Core.Models.Dashboard;

/// <summary>
/// Represents a single delivery attempt for a dashboard notification, tied to a specific channel.
/// </summary>
public record DashboardDeliveryAttempt
{
    /// <summary>
    /// The national identity number of the recipient.
    /// </summary>
    public string? NationalIdentityNumber { get; init; }

    /// <summary>
    /// The delivery channel: "email" or "sms".
    /// </summary>
    public string Channel { get; init; }

    /// <summary>
    /// The email address the notification was sent to. Only set when <see cref="Channel"/> is "email".
    /// </summary>
    public string? EmailAddress { get; init; }

    /// <summary>
    /// The mobile number the notification was sent to. Only set when <see cref="Channel"/> is "sms".
    /// </summary>
    public string? MobileNumber { get; init; }

    /// <summary>
    /// The delivery result status.
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// When the result was recorded.
    /// </summary>
    public DateTime? ResultTime { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardDeliveryAttempt"/> record.
    /// </summary>
    public DashboardDeliveryAttempt(
        string? nationalIdentityNumber,
        string channel,
        string? emailAddress,
        string? mobileNumber,
        string? result,
        DateTime? resultTime)
    {
        NationalIdentityNumber = nationalIdentityNumber;
        Channel = channel;
        EmailAddress = emailAddress;
        MobileNumber = mobileNumber;
        Result = result;
        ResultTime = resultTime;
    }
}
