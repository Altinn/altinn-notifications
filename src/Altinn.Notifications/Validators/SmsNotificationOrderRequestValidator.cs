using System.Text.RegularExpressions;

using Altinn.Notifications.Models;

using FluentValidation;
using PhoneNumbers;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Class containing validation logic for the <see cref="SmsNotificationOrderRequestExt"/> model
/// </summary>
public class SmsNotificationOrderRequestValidator : AbstractValidator<SmsNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationOrderRequestValidator"/> class.
    /// </summary>
    public SmsNotificationOrderRequestValidator()
    {
        RuleFor(order => order.Recipients)
            .NotEmpty()
            .WithMessage("One or more recipient is required.")
            .Must(recipients => recipients.TrueForAll(a => IsValidMobileNumber(a.MobileNumber)))
            .WithMessage("A valid mobile number starting with country code must be provided for all recipients.");

        RuleFor(order => order.RequestedSendTime)
            .Must(sendTime => sendTime.Kind != DateTimeKind.Unspecified)
            .WithMessage("The requested send time value must have specified a time zone.")
            .Must(sendTime => sendTime >= DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Send time must be in the future. Leave blank to send immediately.");

        // LinkMobility which is used for SMS has size limits, see extract from their xml doc:
        // ------------------------------------------------------------------------------------------------
        // Summary:
        //     The message text. For plain text messages, the message length should not exceed
        //     160 characters unless you would like to use concatenated SMS messages. Text messages
        //     exceeding 160 characters will be split up into a maximum of 16 SMS messages,
        //     each of 134 characters. Thus, the maximum length is 16*134=2144 characters. This
        //     is done automatically by the SMS Gateway. Text messages of more than 2144 characters
        //     will be truncated
        // ------------------------------------------------------------------------------------------------
        RuleFor(order => order.Body)
            .NotEmpty()
            .MaximumLength(2144)
            .WithMessage("The SMS body is too large. Maximum size is 2144 characters.");

        RuleFor(order => order.SenderNumber)
            .Must(IsValidSenderNumber)
            .WithMessage("The sender number is invalid.");
    }

    /// <summary>
    /// Validates the sender number based on description of rules from LinkMobility
    /// </summary>
    /// <param name="senderNumber">Sender number</param>
    /// <returns></returns>
    internal static bool IsValidSenderNumber(string? senderNumber) 
    {
        return senderNumber switch 
        {
            string n when n.Length is >= 4 and <= 5 && n.All(char.IsDigit) => true,
            string n when IsValidMobileNumber(n) => true,
            string n when n.Length is >= 2 and <= 11 && Regex.IsMatch(n, "^[A-Za-z0-9 ]+$") && char.IsAsciiLetter(n[0]) => true,
            _ => false,
        };
    }

    /// <summary>
    /// Validated as mobile number based on the Altinn 2 regex
    /// </summary>
    /// <param name="mobileNumber">The string to validate as an mobile number</param>
    /// <returns>A boolean indicating that the mobile number is valid or not</returns>
    internal static bool IsValidMobileNumber(string? mobileNumber)
    {
        if (string.IsNullOrEmpty(mobileNumber) || (!mobileNumber.StartsWith('+') && !mobileNumber.StartsWith("00")))
        {
            return false;
        }

        if (mobileNumber.StartsWith("00"))
        {
            mobileNumber = "+" + mobileNumber.Remove(0, 2);
        }

        PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();
        PhoneNumber phoneNumber = phoneNumberUtil.Parse(mobileNumber, null);
        return phoneNumberUtil.IsValidNumber(phoneNumber);
    }
}
