using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Persistence.Mappers;

/// <summary>
/// Helper class for mapping processing life cycle related data to and from the database.
/// </summary>
internal static class ProcessingLifecycleMapper
{
    /// <summary>
    /// Maps database SMS notification result types to ProcessingLifecycle values.
    /// </summary>
    private static readonly Dictionary<string, ProcessingLifecycle> _smsStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "new", ProcessingLifecycle.SMS_New },
        { "failed", ProcessingLifecycle.SMS_Failed },
        { "sending", ProcessingLifecycle.SMS_Sending },
        { "accepted", ProcessingLifecycle.SMS_Accepted },
        { "delivered", ProcessingLifecycle.SMS_Delivered },
        { "failed_deleted", ProcessingLifecycle.SMS_Failed_Deleted },
        { "failed_expired", ProcessingLifecycle.SMS_Failed_Expired },
        { "failed_rejected", ProcessingLifecycle.SMS_Failed_Rejected },
        { "failed_undelivered", ProcessingLifecycle.SMS_Failed_Undelivered },
        { "failed_barredreceiver", ProcessingLifecycle.SMS_Failed_BarredReceiver },
        { "failed_invalidreceiver", ProcessingLifecycle.SMS_Failed_InvalidRecipient },
        { "failed_invalidrecipient", ProcessingLifecycle.SMS_Failed_InvalidRecipient },
        { "failed_recipientreserved", ProcessingLifecycle.SMS_Failed_RecipientReserved },
        { "failed_recipientnotidentified", ProcessingLifecycle.SMS_Failed_RecipientNotIdentified }
    };

    /// <summary>
    /// Maps database email notification result types to ProcessingLifecycle values.
    /// </summary>
    private static readonly Dictionary<string, ProcessingLifecycle> _emailStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "new", ProcessingLifecycle.Email_New },
        { "failed", ProcessingLifecycle.Email_Failed },
        { "sending", ProcessingLifecycle.Email_Sending },
        { "succeeded", ProcessingLifecycle.Email_Succeeded },
        { "delivered", ProcessingLifecycle.Email_Delivered },
        { "failed_bounced", ProcessingLifecycle.Email_Failed_Bounced },
        { "failed_quarantined", ProcessingLifecycle.Email_Failed_Quarantined },
        { "failed_filteredspam", ProcessingLifecycle.Email_Failed_FilteredSpam },
        { "failed_transienterror", ProcessingLifecycle.Email_Failed_TransientError },
        { "failed_invalidemailformat", ProcessingLifecycle.Email_Failed_InvalidFormat },
        { "failed_recipientreserved", ProcessingLifecycle.Email_Failed_RecipientReserved },
        { "failed_supressedrecipient", ProcessingLifecycle.Email_Failed_SuppressedRecipient },
        { "failed_recipientnotidentified", ProcessingLifecycle.Email_Failed_RecipientNotIdentified }
    };

    /// <summary>
    /// Maps database order processing state types to ProcessingLifecycle values.
    /// </summary>
    private static readonly Dictionary<string, ProcessingLifecycle> _orderStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "cancelled", ProcessingLifecycle.Order_Cancelled },
        { "completed", ProcessingLifecycle.Order_Completed },
        { "processed", ProcessingLifecycle.Order_Processed },
        { "registered", ProcessingLifecycle.Order_Registered },
        { "processing", ProcessingLifecycle.Order_Processing },
        { "sendconditionnotmet", ProcessingLifecycle.Order_SendConditionNotMet }
    };

    /// <summary>
    /// Converts a database SMS notification status to its corresponding <see cref="ProcessingLifecycle"/> enum value.
    /// </summary>
    /// <param name="status">The SMS status string from the database (from SMSNOTIFICATIONRESULTTYPE type).</param>
    /// <returns>The corresponding <see cref="ProcessingLifecycle"/> enum value for the SMS notification status.</returns>
    /// <exception cref="ArgumentException">Thrown when the status string doesn't match any known SMS notification status.</exception>
    /// <remarks>
    /// This method performs case-insensitive mapping between database SMS status values and the 
    /// ProcessingLifecycle enum. It's used when processing delivery information from the database
    /// to ensure consistent status representation throughout the notification system.
    /// </remarks>
    internal static ProcessingLifecycle GetSmsLifecycleStage(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (_smsStatusMap.TryGetValue(status.ToLowerInvariant(), out ProcessingLifecycle result))
        {
            return result;
        }

        throw new ArgumentException($"Unknown SMS notification processing state: '{status}'", nameof(status));
    }

    /// <summary>
    /// Converts a database email notification status to its corresponding <see cref="ProcessingLifecycle"/> enum value.
    /// </summary>
    /// <param name="status">The email status string from the database (from EMAILNOTIFICATIONRESULTTYPE type).</param>
    /// <returns>The corresponding <see cref="ProcessingLifecycle"/> enum value for the email notification status.</returns>
    /// <exception cref="ArgumentException">Thrown when the status string doesn't match any known email notification status.</exception>
    /// <remarks>
    /// This method performs case-insensitive mapping between database email status values and the 
    /// ProcessingLifecycle enum. It's used when processing delivery information from the database
    /// to ensure consistent status representation throughout the notification system.
    /// </remarks>
    internal static ProcessingLifecycle GetEmailLifecycleStage(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (_emailStatusMap.TryGetValue(status.ToLowerInvariant(), out ProcessingLifecycle result))
        {
            return result;
        }

        throw new ArgumentException($"Unknown email notification processing state: '{status}'", nameof(status));
    }

    /// <summary>
    /// Converts a database order processing state to its corresponding <see cref="ProcessingLifecycle"/> enum value.
    /// </summary>
    /// <param name="status">The order status string from the database (from ORDERPROCESSINGSTATE type).</param>
    /// <returns>The corresponding <see cref="ProcessingLifecycle"/> enum value for the order status.</returns>
    /// <exception cref="ArgumentException">Thrown when the status string doesn't match any known order processing state.</exception>
    /// <remarks>
    /// This method performs case-insensitive mapping between database order status values and the 
    /// ProcessingLifecycle enum. It's used when processing delivery information from the database
    /// to ensure consistent status representation throughout the notification system.
    /// </remarks>
    internal static ProcessingLifecycle GetOrderLifecycleStage(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (_orderStatusMap.TryGetValue(status.ToLowerInvariant(), out ProcessingLifecycle result))
        {
            return result;
        }

        throw new ArgumentException($"Unknown order processing state: '{status}'", nameof(status));
    }
}
