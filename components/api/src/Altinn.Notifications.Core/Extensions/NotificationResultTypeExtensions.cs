using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Extensions;

/// <summary>
/// Domain rules describing whether an email or SMS notification result is final.
/// </summary>
/// <remarks>
/// A final result can no longer progress towards delivery. Failed and expired results are final.
/// </remarks>
public static class NotificationResultTypeExtensions
{
    /// <summary>
    /// Determines whether the SMS notification result is final.
    /// </summary>
    /// <param name="result">The SMS notification result.</param>
    /// <returns><c>true</c> if the result is final; <c>false</c> if the notification is still pending.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="result"/> is not a defined value.</exception>
    public static bool IsFinal(this SmsNotificationResultType result)
    {
        return result switch
        {
            SmsNotificationResultType.New => false,
            SmsNotificationResultType.Sending => false,
            SmsNotificationResultType.Accepted => false,
            SmsNotificationResultType.Delivered => true,
            SmsNotificationResultType.Failed => true,
            SmsNotificationResultType.Failed_TTL => true,
            SmsNotificationResultType.Failed_Deleted => true,
            SmsNotificationResultType.Failed_Expired => true,
            SmsNotificationResultType.Failed_Rejected => true,
            SmsNotificationResultType.Failed_Undelivered => true,
            SmsNotificationResultType.Failed_BarredReceiver => true,
            SmsNotificationResultType.Failed_InvalidRecipient => true,
            SmsNotificationResultType.Failed_RecipientReserved => true,
            SmsNotificationResultType.Failed_RecipientNotIdentified => true,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown SMS notification result type.")
        };
    }

    /// <summary>
    /// Determines whether the email notification result is final.
    /// </summary>
    /// <param name="result">The email notification result.</param>
    /// <returns><c>true</c> if the result is final; <c>false</c> if the notification is still pending.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="result"/> is not a defined value.</exception>
    public static bool IsFinal(this EmailNotificationResultType result)
    {
        return result switch
        {
            EmailNotificationResultType.New => false,
            EmailNotificationResultType.Sending => false,
            EmailNotificationResultType.Succeeded => false,
            EmailNotificationResultType.Delivered => true,
            EmailNotificationResultType.Failed => true,
            EmailNotificationResultType.Failed_TTL => true,
            EmailNotificationResultType.Failed_Bounced => true,
            EmailNotificationResultType.Failed_Quarantined => true,
            EmailNotificationResultType.Failed_FilteredSpam => true,
            EmailNotificationResultType.Failed_InvalidSasUrl => true,
            EmailNotificationResultType.Failed_TransientError => false,
            EmailNotificationResultType.Failed_PayloadTooLarge => true,
            EmailNotificationResultType.Failed_RecipientReserved => true,
            EmailNotificationResultType.Failed_InvalidEmailFormat => true,
            EmailNotificationResultType.Failed_SupressedRecipient => true,
            EmailNotificationResultType.Failed_RecipientNotIdentified => true,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown email notification result type.")
        };
    }
}
