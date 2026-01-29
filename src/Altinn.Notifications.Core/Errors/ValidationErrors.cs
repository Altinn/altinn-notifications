using Altinn.Authorization.ProblemDetails;

namespace Altinn.Notifications.Core.Errors;

/// <summary>
/// Validation error descriptors for the Notifications API.
/// Error codes will be in format NOT.VLD-{5-digit-number}.
/// </summary>
public static class ValidationErrors
{
    private static readonly ValidationErrorDescriptorFactory _factory
        = ValidationErrorDescriptorFactory.New("NOT");

    // ============================================================
    // Idempotency and basic request validation (1-9)
    // ============================================================

    /// <summary>
    /// IdempotencyId is required and must be a non-empty string.
    /// </summary>
    public static ValidationErrorDescriptor IdempotencyId_Required { get; }
        = _factory.Create(1, "IdempotencyId cannot be null or empty.");

    // ============================================================
    // Send time validation (10-19)
    // ============================================================

    /// <summary>
    /// The requested send time must have a specified time zone.
    /// </summary>
    public static ValidationErrorDescriptor SendTime_TimezoneRequired { get; }
        = _factory.Create(10, "The requested send time value must have specified a time zone.");

    /// <summary>
    /// The requested send time must be in the future.
    /// </summary>
    public static ValidationErrorDescriptor SendTime_MustBeFuture { get; }
        = _factory.Create(11, "RequestedSendTime must be greater than or equal to now.");

    // ============================================================
    // Condition endpoint validation (20-29)
    // ============================================================

    /// <summary>
    /// ConditionEndpoint must be a valid absolute URI.
    /// </summary>
    public static ValidationErrorDescriptor ConditionEndpoint_InvalidUri { get; }
        = _factory.Create(20, "ConditionEndpoint must be a valid absolute URI or null.");

    /// <summary>
    /// ConditionEndpoint must use http or https scheme.
    /// </summary>
    public static ValidationErrorDescriptor ConditionEndpoint_InvalidScheme { get; }
        = _factory.Create(21, "ConditionEndpoint must use http or https scheme.");

    // ============================================================
    // Recipient validation (30-49)
    // ============================================================

    /// <summary>
    /// Must have exactly one recipient.
    /// </summary>
    public static ValidationErrorDescriptor Recipient_MustHaveExactlyOne { get; }
        = _factory.Create(30, "Must have exactly one recipient.");

    /// <summary>
    /// Recipient specification cannot be null.
    /// </summary>
    public static ValidationErrorDescriptor Recipient_CannotBeNull { get; }
        = _factory.Create(31, "Recipient specification cannot be null.");

    /// <summary>
    /// One or more recipient is required.
    /// </summary>
    public static ValidationErrorDescriptor Recipient_AtLeastOneRequired { get; }
        = _factory.Create(32, "One or more recipient is required.");

    /// <summary>
    /// Invalid email address format.
    /// </summary>
    public static ValidationErrorDescriptor EmailAddress_Invalid { get; }
        = _factory.Create(33, "Invalid email address format.");

    /// <summary>
    /// Invalid sender email address format.
    /// </summary>
    public static ValidationErrorDescriptor SenderEmailAddress_Invalid { get; }
        = _factory.Create(34, "The sender email address is not valid.");

    /// <summary>
    /// Invalid phone number format.
    /// </summary>
    public static ValidationErrorDescriptor PhoneNumber_Invalid { get; }
        = _factory.Create(35, "Mobile number can contain only '+' and numeric characters, and it must adhere to the E.164 standard.");

    /// <summary>
    /// National identity number must be 11 digits.
    /// </summary>
    public static ValidationErrorDescriptor NationalIdentityNumber_Invalid { get; }
        = _factory.Create(36, "National identity number must be 11 digits long.");

    /// <summary>
    /// Organization number must be 9 digits.
    /// </summary>
    public static ValidationErrorDescriptor OrganizationNumber_Invalid { get; }
        = _factory.Create(37, "Organization number must be 9 digits long.");

    /// <summary>
    /// Organization number cannot be null or empty.
    /// </summary>
    public static ValidationErrorDescriptor OrganizationNumber_Required { get; }
        = _factory.Create(38, "OrgNumber cannot be null or empty.");

    /// <summary>
    /// ResourceId must have a valid syntax.
    /// </summary>
    public static ValidationErrorDescriptor ResourceId_Invalid { get; }
        = _factory.Create(39, "ResourceId must have a valid syntax.");

    /// <summary>
    /// National identity number cannot be combined with other identifiers.
    /// </summary>
    public static ValidationErrorDescriptor NationalIdentityNumber_InvalidCombination { get; }
        = _factory.Create(40, "National identity number cannot be combined with email address, mobile number, or organization number.");

    /// <summary>
    /// Organization number cannot be combined with other identifiers.
    /// </summary>
    public static ValidationErrorDescriptor OrganizationNumber_InvalidCombination { get; }
        = _factory.Create(41, "Organization number cannot be combined with email address, mobile number, or national identity number.");

    /// <summary>
    /// Recipient must provide at least one contact method for preferred channel.
    /// </summary>
    public static ValidationErrorDescriptor Recipient_MissingContactInfo_Preferred { get; }
        = _factory.Create(42, "Either a valid email address, mobile number starting with country code, organization number, or national identity number must be provided for each recipient.");

    /// <summary>
    /// Recipient must provide at least one contact method for SMS channel.
    /// </summary>
    public static ValidationErrorDescriptor Recipient_MissingContactInfo_Sms { get; }
        = _factory.Create(43, "Either a valid mobile number starting with country code, organization number, or national identity number must be provided for each recipient.");

    /// <summary>
    /// Recipient must provide at least one contact method for email channel.
    /// </summary>
    public static ValidationErrorDescriptor Recipient_MissingContactInfo_Email { get; }
        = _factory.Create(44, "Either a valid email address, organization number, or national identity number must be provided for each recipient.");

    // ============================================================
    // Email settings validation (50-59)
    // ============================================================

    /// <summary>
    /// Email sending options cannot be null.
    /// </summary>
    public static ValidationErrorDescriptor EmailSettings_Required { get; }
        = _factory.Create(50, "Email sending options cannot be null.");

    /// <summary>
    /// Email subject is required.
    /// </summary>
    public static ValidationErrorDescriptor EmailSubject_Required { get; }
        = _factory.Create(51, "The email subject must not be empty.");

    /// <summary>
    /// Email body is required.
    /// </summary>
    public static ValidationErrorDescriptor EmailBody_Required { get; }
        = _factory.Create(52, "The email body must not be empty.");

    /// <summary>
    /// Invalid email content type.
    /// </summary>
    public static ValidationErrorDescriptor EmailContentType_Invalid { get; }
        = _factory.Create(53, "Email content type must be either Plain or HTML.");

    /// <summary>
    /// Invalid sending time policy for email.
    /// </summary>
    public static ValidationErrorDescriptor SendingTimePolicy_Email_Invalid { get; }
        = _factory.Create(54, "Email only supports send time anytime.");

    // ============================================================
    // SMS settings validation (60-69)
    // ============================================================

    /// <summary>
    /// SMS body is required.
    /// </summary>
    public static ValidationErrorDescriptor SmsBody_Required { get; }
        = _factory.Create(60, "SMS body cannot be null or empty.");

    /// <summary>
    /// Invalid sending time policy for SMS.
    /// </summary>
    public static ValidationErrorDescriptor SendingTimePolicy_Sms_Invalid { get; }
        = _factory.Create(61, "SMS only supports send time daytime and anytime.");

    // ============================================================
    // Reminder validation (70-79)
    // ============================================================

    /// <summary>
    /// Either DelayDays or RequestedSendTime must be defined, but not both.
    /// </summary>
    public static ValidationErrorDescriptor Reminder_TimingMutuallyExclusive { get; }
        = _factory.Create(70, "Either DelayDays or RequestedSendTime must be defined, but not both.");

    /// <summary>
    /// DelayDays must be at least 1.
    /// </summary>
    public static ValidationErrorDescriptor ReminderDelayDays_Invalid { get; }
        = _factory.Create(71, "DelayDays must be greater than or equal to 1 day.");

    /// <summary>
    /// RequestedSendTime must be null when DelayDays is set.
    /// </summary>
    public static ValidationErrorDescriptor Reminder_RequestedSendTimeMustBeNull { get; }
        = _factory.Create(72, "RequestedSendTime must be null when DelayDays is set.");

    /// <summary>
    /// DelayDays must be null when RequestedSendTime is set.
    /// </summary>
    public static ValidationErrorDescriptor Reminder_DelayDaysMustBeNull { get; }
        = _factory.Create(73, "DelayDays must be null when RequestedSendTime is set.");

    /// <summary>
    /// Reminder send time must have a time zone specified.
    /// </summary>
    public static ValidationErrorDescriptor ReminderSendTime_TimezoneRequired { get; }
        = _factory.Create(74, "The RequestedSendTime must have specified a time zone.");

    /// <summary>
    /// Reminder send time must be in the future.
    /// </summary>
    public static ValidationErrorDescriptor ReminderSendTime_MustBeFuture { get; }
        = _factory.Create(75, "RequestedSendTime must be greater than or equal to the current UTC time.");

    // ============================================================
    // Channel validation (80-89)
    // ============================================================

    /// <summary>
    /// Invalid channel schema value.
    /// </summary>
    public static ValidationErrorDescriptor ChannelSchema_Invalid { get; }
        = _factory.Create(80, "Invalid channel scheme value.");

    /// <summary>
    /// EmailSettings must be set when ChannelSchema is EmailAndSms.
    /// </summary>
    public static ValidationErrorDescriptor ChannelSchema_EmailSettings_RequiredForDualChannel { get; }
        = _factory.Create(81, "EmailSettings must be set when ChannelSchema is EmailAndSms.");

    /// <summary>
    /// SmsSettings must be set when ChannelSchema is EmailAndSms.
    /// </summary>
    public static ValidationErrorDescriptor ChannelSchema_SmsSettings_RequiredForDualChannel { get; }
        = _factory.Create(82, "SmsSettings must be set when ChannelSchema is EmailAndSms.");

    /// <summary>
    /// EmailSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred.
    /// </summary>
    public static ValidationErrorDescriptor ChannelSchema_EmailSettings_RequiredForFallback { get; }
        = _factory.Create(83, "EmailSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred.");

    /// <summary>
    /// SmsSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred.
    /// </summary>
    public static ValidationErrorDescriptor ChannelSchema_SmsSettings_RequiredForFallback { get; }
        = _factory.Create(84, "SmsSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred.");

    /// <summary>
    /// SmsSettings must be set when ChannelSchema is Sms.
    /// </summary>
    public static ValidationErrorDescriptor ChannelSchema_SmsSettings_RequiredForSms { get; }
        = _factory.Create(85, "SmsSettings must be set when ChannelSchema is Sms.");

    /// <summary>
    /// EmailSettings must be set when ChannelSchema is Email.
    /// </summary>
    public static ValidationErrorDescriptor ChannelSchema_EmailSettings_RequiredForEmail { get; }
        = _factory.Create(86, "EmailSettings must be set when ChannelSchema is Email.");

    // ============================================================
    // Status feed validation (90-99)
    // ============================================================

    /// <summary>
    /// Sequence number cannot be negative.
    /// </summary>
    public static ValidationErrorDescriptor SequenceNumber_Invalid { get; }
        = _factory.Create(90, "Sequence number cannot be less than 0.");

    // ============================================================
    // Dialogporten validation (100-109)
    // ============================================================

    /// <summary>
    /// DialogId must be a valid non-empty GUID.
    /// </summary>
    public static ValidationErrorDescriptor DialogId_Invalid { get; }
        = _factory.Create(100, "DialogId must be a valid non-empty GUID.");

    /// <summary>
    /// TransmissionId must be a valid non-empty GUID.
    /// </summary>
    public static ValidationErrorDescriptor TransmissionId_Invalid { get; }
        = _factory.Create(101, "TransmissionId must be a valid non-empty GUID.");

    // ============================================================
    // Instant notification validation (110-119)
    // ============================================================

    /// <summary>
    /// SMS delivery details cannot be null.
    /// </summary>
    public static ValidationErrorDescriptor SmsDetails_Required { get; }
        = _factory.Create(110, "SMS details cannot be null.");

    /// <summary>
    /// Email delivery details cannot be null.
    /// </summary>
    public static ValidationErrorDescriptor EmailDetails_Required { get; }
        = _factory.Create(111, "Email details cannot be null.");

    /// <summary>
    /// Recipient email object cannot be null.
    /// </summary>
    public static ValidationErrorDescriptor RecipientEmail_Required { get; }
        = _factory.Create(112, "Recipient email object cannot be null.");

    /// <summary>
    /// Recipient email settings cannot be null.
    /// </summary>
    public static ValidationErrorDescriptor RecipientEmailSettings_Required { get; }
        = _factory.Create(113, "Recipient email settings cannot be null.");
}
