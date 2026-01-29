using System.Text.RegularExpressions;

using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Provides validation methods for notification order chain requests using ValidationErrorBuilder.
/// Throws <see cref="ProblemInstanceException"/> when validation fails.
/// </summary>
public static class NotificationOrderChainValidationHelper
{
    private const int NationalIdentityNumberLength = 11;
    private const int OrganizationNumberLength = 9;
    private static readonly string[] AllowedUriSchemes = ["https", "http"];

    /// <summary>
    /// Validates a <see cref="NotificationOrderChainRequestExt"/> request and throws if validation fails.
    /// </summary>
    /// <param name="request">The notification order chain request to validate.</param>
    /// <param name="utcNow">The current UTC time for time-based validations.</param>
    /// <exception cref="ProblemInstanceException">Thrown when validation fails.</exception>
    public static void ValidateOrderChainRequest(NotificationOrderChainRequestExt request, DateTime utcNow)
    {
        var errors = default(ValidationErrorBuilder);

        ValidateIdempotencyId(request.IdempotencyId, ref errors);
        ValidateSendTime(request.RequestedSendTime, utcNow, ref errors, "RequestedSendTime");
        ValidateConditionEndpoint(request.ConditionEndpoint, ref errors, "ConditionEndpoint");
        ValidateRecipient(request.Recipient, ref errors, "Recipient");

        if (request.Reminders != null)
        {
            for (int i = 0; i < request.Reminders.Count; i++)
            {
                ValidateReminder(request.Reminders[i], $"Reminders[{i}]", utcNow, ref errors);
            }
        }

        if (errors.TryBuild(out var problemInstance))
        {
            throw new ProblemInstanceException(problemInstance);
        }
    }

    /// <summary>
    /// Validates idempotency identifier.
    /// </summary>
    public static void ValidateIdempotencyId(string? idempotencyId, ref ValidationErrorBuilder errors)
    {
        if (string.IsNullOrWhiteSpace(idempotencyId))
        {
            errors.Add(ValidationErrors.IdempotencyId_Required, "IdempotencyId");
        }
    }

    /// <summary>
    /// Validates send time.
    /// </summary>
    public static void ValidateSendTime(DateTime sendTime, DateTime utcNow, ref ValidationErrorBuilder errors, string path)
    {
        if (sendTime.Kind == DateTimeKind.Unspecified)
        {
            errors.Add(ValidationErrors.SendTime_TimezoneRequired, path);
        }
        else if (sendTime < utcNow)
        {
            errors.Add(ValidationErrors.SendTime_MustBeFuture, path);
        }
    }

    /// <summary>
    /// Validates condition endpoint.
    /// </summary>
    public static void ValidateConditionEndpoint(Uri? conditionEndpoint, ref ValidationErrorBuilder errors, string path)
    {
        if (conditionEndpoint == null)
        {
            return;
        }

        if (!conditionEndpoint.IsAbsoluteUri || !Uri.IsWellFormedUriString(conditionEndpoint.ToString(), UriKind.Absolute))
        {
            errors.Add(ValidationErrors.ConditionEndpoint_InvalidUri, path);
        }
        else if (!AllowedUriSchemes.Contains(conditionEndpoint.Scheme.ToLowerInvariant()))
        {
            errors.Add(ValidationErrors.ConditionEndpoint_InvalidScheme, path);
        }
    }

    /// <summary>
    /// Validates recipient specification.
    /// </summary>
    public static void ValidateRecipient(NotificationRecipientExt? recipient, ref ValidationErrorBuilder errors, string path)
    {
        if (recipient == null)
        {
            errors.Add(ValidationErrors.Recipient_CannotBeNull, path);
            return;
        }

        // Check exactly one recipient is set
        var recipientCount = new object?[]
        {
            recipient.RecipientEmail,
            recipient.RecipientSms,
            recipient.RecipientPerson,
            recipient.RecipientOrganization
        }.Count(r => r != null);

        if (recipientCount != 1)
        {
            errors.Add(ValidationErrors.Recipient_MustHaveExactlyOne, path);
            return;
        }

        if (recipient.RecipientEmail != null)
        {
            ValidateRecipientEmail(recipient.RecipientEmail, ref errors, $"{path}.RecipientEmail");
        }

        if (recipient.RecipientSms != null)
        {
            ValidateRecipientSms(recipient.RecipientSms, ref errors, $"{path}.RecipientSms");
        }

        if (recipient.RecipientPerson != null)
        {
            ValidateRecipientPerson(recipient.RecipientPerson, ref errors, $"{path}.RecipientPerson");
        }

        if (recipient.RecipientOrganization != null)
        {
            ValidateRecipientOrganization(recipient.RecipientOrganization, ref errors, $"{path}.RecipientOrganization");
        }
    }

    /// <summary>
    /// Validates email recipient.
    /// </summary>
    public static void ValidateRecipientEmail(RecipientEmailExt recipientEmail, ref ValidationErrorBuilder errors, string path)
    {
        if (!IsValidEmail(recipientEmail.EmailAddress))
        {
            errors.Add(ValidationErrors.EmailAddress_Invalid, $"{path}.EmailAddress");
        }

        ValidateEmailSendingOptions(recipientEmail.Settings, ref errors, $"{path}.Settings");
    }

    /// <summary>
    /// Validates SMS recipient.
    /// </summary>
    public static void ValidateRecipientSms(RecipientSmsExt recipientSms, ref ValidationErrorBuilder errors, string path)
    {
        if (!MobileNumberHelper.IsValidMobileNumber(recipientSms.PhoneNumber))
        {
            errors.Add(ValidationErrors.PhoneNumber_Invalid, $"{path}.PhoneNumber");
        }

        ValidateSmsSendingOptions(recipientSms.Settings, ref errors, $"{path}.Settings");
    }

    /// <summary>
    /// Validates person recipient.
    /// </summary>
    public static void ValidateRecipientPerson(RecipientPersonExt recipientPerson, ref ValidationErrorBuilder errors, string path)
    {
        ValidateNationalIdentityNumber(recipientPerson.NationalIdentityNumber, ref errors, $"{path}.NationalIdentityNumber");

        if (recipientPerson.ResourceId != null && !IsValidResourceId(recipientPerson.ResourceId))
        {
            errors.Add(ValidationErrors.ResourceId_Invalid, $"{path}.ResourceId");
        }

        ValidateChannelSchemaSettings(
            recipientPerson.ChannelSchema,
            recipientPerson.EmailSettings,
            recipientPerson.SmsSettings,
            ref errors,
            path);
    }

    /// <summary>
    /// Validates organization recipient.
    /// </summary>
    public static void ValidateRecipientOrganization(RecipientOrganizationExt recipientOrganization, ref ValidationErrorBuilder errors, string path)
    {
        if (string.IsNullOrWhiteSpace(recipientOrganization.OrgNumber))
        {
            errors.Add(ValidationErrors.OrganizationNumber_Required, $"{path}.OrgNumber");
        }
        else if (!IsValidOrganizationNumber(recipientOrganization.OrgNumber))
        {
            errors.Add(ValidationErrors.OrganizationNumber_Invalid, $"{path}.OrgNumber");
        }

        if (recipientOrganization.ResourceId != null && !IsValidResourceId(recipientOrganization.ResourceId))
        {
            errors.Add(ValidationErrors.ResourceId_Invalid, $"{path}.ResourceId");
        }

        ValidateChannelSchemaSettings(
            recipientOrganization.ChannelSchema,
            recipientOrganization.EmailSettings,
            recipientOrganization.SmsSettings,
            ref errors,
            path);
    }

    /// <summary>
    /// Validates channel schema settings based on the selected channel.
    /// </summary>
    public static void ValidateChannelSchemaSettings(
        NotificationChannelExt channelSchema,
        EmailSendingOptionsExt? emailSettings,
        SmsSendingOptionsExt? smsSettings,
        ref ValidationErrorBuilder errors,
        string path)
    {
        if (!Enum.IsDefined(channelSchema))
        {
            errors.Add(ValidationErrors.ChannelSchema_Invalid, $"{path}.ChannelSchema");
            return;
        }

        switch (channelSchema)
        {
            case NotificationChannelExt.EmailAndSms:
                if (emailSettings == null)
                {
                    errors.Add(ValidationErrors.ChannelSchema_EmailSettings_RequiredForDualChannel, $"{path}.EmailSettings");
                }

                if (smsSettings == null)
                {
                    errors.Add(ValidationErrors.ChannelSchema_SmsSettings_RequiredForDualChannel, $"{path}.SmsSettings");
                }

                break;

            case NotificationChannelExt.EmailPreferred:
            case NotificationChannelExt.SmsPreferred:
                if (emailSettings == null)
                {
                    errors.Add(ValidationErrors.ChannelSchema_EmailSettings_RequiredForFallback, $"{path}.EmailSettings");
                }

                if (smsSettings == null)
                {
                    errors.Add(ValidationErrors.ChannelSchema_SmsSettings_RequiredForFallback, $"{path}.SmsSettings");
                }

                break;

            case NotificationChannelExt.Email:
                if (emailSettings == null)
                {
                    errors.Add(ValidationErrors.ChannelSchema_EmailSettings_RequiredForEmail, $"{path}.EmailSettings");
                }

                break;

            case NotificationChannelExt.Sms:
                if (smsSettings == null)
                {
                    errors.Add(ValidationErrors.ChannelSchema_SmsSettings_RequiredForSms, $"{path}.SmsSettings");
                }

                break;
        }

        if (emailSettings != null)
        {
            ValidateEmailSendingOptions(emailSettings, ref errors, $"{path}.EmailSettings");
        }

        if (smsSettings != null)
        {
            ValidateSmsSendingOptions(smsSettings, ref errors, $"{path}.SmsSettings");
        }
    }

    /// <summary>
    /// Validates email sending options.
    /// </summary>
    public static void ValidateEmailSendingOptions(EmailSendingOptionsExt? settings, ref ValidationErrorBuilder errors, string path)
    {
        if (settings == null)
        {
            errors.Add(ValidationErrors.EmailSettings_Required, path);
            return;
        }

        if (settings.SenderEmailAddress != null && !IsValidEmail(settings.SenderEmailAddress))
        {
            errors.Add(ValidationErrors.SenderEmailAddress_Invalid, $"{path}.SenderEmailAddress");
        }

        if (string.IsNullOrWhiteSpace(settings.Subject))
        {
            errors.Add(ValidationErrors.EmailSubject_Required, $"{path}.Subject");
        }

        if (string.IsNullOrWhiteSpace(settings.Body))
        {
            errors.Add(ValidationErrors.EmailBody_Required, $"{path}.Body");
        }

        if (!Enum.IsDefined(settings.ContentType))
        {
            errors.Add(ValidationErrors.EmailContentType_Invalid, $"{path}.ContentType");
        }

        if (settings.SendingTimePolicy != SendingTimePolicyExt.Anytime)
        {
            errors.Add(ValidationErrors.SendingTimePolicy_Email_Invalid, $"{path}.SendingTimePolicy");
        }
    }

    /// <summary>
    /// Validates SMS sending options.
    /// </summary>
    public static void ValidateSmsSendingOptions(SmsSendingOptionsExt? settings, ref ValidationErrorBuilder errors, string path)
    {
        if (settings == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Body))
        {
            errors.Add(ValidationErrors.SmsBody_Required, $"{path}.Body");
        }

        if (settings.SendingTimePolicy != SendingTimePolicyExt.Anytime &&
            settings.SendingTimePolicy != SendingTimePolicyExt.Daytime)
        {
            errors.Add(ValidationErrors.SendingTimePolicy_Sms_Invalid, $"{path}.SendingTimePolicy");
        }
    }

    /// <summary>
    /// Validates a reminder.
    /// </summary>
    public static void ValidateReminder(NotificationReminderExt reminder, string path, DateTime utcNow, ref ValidationErrorBuilder errors)
    {
        // Check mutual exclusivity of DelayDays and RequestedSendTime
        bool hasDelayDays = reminder.DelayDays.HasValue;
        bool hasSendTime = reminder.RequestedSendTime.HasValue;

        if ((hasDelayDays && hasSendTime) || (!hasDelayDays && !hasSendTime))
        {
            errors.Add(ValidationErrors.Reminder_TimingMutuallyExclusive, path);
        }

        if (hasDelayDays)
        {
            if (reminder.DelayDays < 1)
            {
                errors.Add(ValidationErrors.ReminderDelayDays_Invalid, $"{path}.DelayDays");
            }

            if (hasSendTime)
            {
                errors.Add(ValidationErrors.Reminder_RequestedSendTimeMustBeNull, $"{path}.RequestedSendTime");
            }
        }

        if (hasSendTime)
        {
            if (hasDelayDays)
            {
                errors.Add(ValidationErrors.Reminder_DelayDaysMustBeNull, $"{path}.DelayDays");
            }

            if (reminder.RequestedSendTime!.Value.Kind == DateTimeKind.Unspecified)
            {
                errors.Add(ValidationErrors.ReminderSendTime_TimezoneRequired, $"{path}.RequestedSendTime");
            }
            else if (reminder.RequestedSendTime.Value < utcNow)
            {
                errors.Add(ValidationErrors.ReminderSendTime_MustBeFuture, $"{path}.RequestedSendTime");
            }
        }

        ValidateConditionEndpoint(reminder.ConditionEndpoint, ref errors, $"{path}.ConditionEndpoint");
        ValidateRecipient(reminder.Recipient, ref errors, $"{path}.Recipient");
    }

    /// <summary>
    /// Validates national identity number (11 digits).
    /// </summary>
    public static void ValidateNationalIdentityNumber(string? nin, ref ValidationErrorBuilder errors, string path)
    {
        if (string.IsNullOrWhiteSpace(nin) || nin.Length != NationalIdentityNumberLength || !nin.All(char.IsDigit))
        {
            errors.Add(ValidationErrors.NationalIdentityNumber_Invalid, path);
        }
    }

    /// <summary>
    /// Checks if organization number is valid (9 digits).
    /// </summary>
    private static bool IsValidOrganizationNumber(string orgNumber)
    {
        return orgNumber.Length == OrganizationNumberLength && orgNumber.All(char.IsDigit);
    }

    /// <summary>
    /// Checks if resource ID starts with required prefix.
    /// </summary>
    private static bool IsValidResourceId(string resourceId)
    {
        return resourceId.StartsWith("urn:altinn:resource", StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates email address using Altinn 2 regex pattern.
    /// </summary>
    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return false;
        }

        const string emailRegexPattern = @"((&quot;[^&quot;]+&quot;)|(([a-zA-Z0-9!#$%&amp;'*+\-=?\^_`{|}~])+(\.([a-zA-Z0-9!#$%&amp;'*+\-=?\^_`{|}~])+)*))@((((([a-zA-Z0-9æøåÆØÅ]([a-zA-Z0-9\-æøåÆØÅ]{0,61})[a-zA-Z0-9æøåÆØÅ]\.)|[a-zA-Z0-9æøåÆØÅ]\.){1,9})([a-zA-Z]{2,14}))|((\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})))";

        var regex = new Regex(emailRegexPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        return regex.IsMatch(email);
    }
}
