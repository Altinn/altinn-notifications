namespace Altinn.Notifications.Email.Integrations.Clients.AzureCommunicationServices;

/// <summary>
/// Constants for error types returned from Azure Communication Services (ACS)
/// </summary>
public static class ErrorTypes
{
    /// <summary>
    /// The error code returned when the call volume has exceeded the assigned quota for our Azure subscription
    /// </summary>
    public const string ExcessiveCallVolumeErrorCode = "TooManyRequests";

    /// <summary>
    /// The error code returned when the email recipient is found on the suppression list of ACS
    /// </summary>
    public const string RecipientsSuppressedErrorCode = "EmailDroppedAllRecipientsSuppressed";
    
    /// <summary>
    /// The error message returned when the recipient email format is invalid
    /// </summary>
    public const string InvalidEmailFormatErrorMessage = "Invalid format for email address";

    /// <summary>
    /// The error message returned when the email payload exceeds the 10 MB limit
    /// </summary>
    public const string PayloadTooLargeErrorMessage = "Email payload must be less than 10 MB.";
}
