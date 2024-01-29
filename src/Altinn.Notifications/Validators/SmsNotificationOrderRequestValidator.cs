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
            .WithMessage("A valid mobile number must be provided for all recipients.");

        RuleFor(order => order.RequestedSendTime)
            .Must(sendTime => sendTime.Kind != DateTimeKind.Unspecified)
            .WithMessage("The requested send time value must have specified a time zone.")
            .Must(sendTime => sendTime >= DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Send time must be in the future. Leave blank to send immediately.");

        RuleFor(order => order.Body).NotEmpty();
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

        string mobileNumberRegexPattern = @"^(?:(\+47[49]\d{7})|(0047[49]\d{7})|(?!((\+47[0-9]*)|(0047[0-9]*)))(([0-9]{8})|(00[0-9]{3,})|(\+[0-9]{3,})))$";

        Regex regex = new(mobileNumberRegexPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        Match match = regex.Match(mobileNumber);

        if (!match.Success)
        {
            return false;
        }

        PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();
        PhoneNumber phoneNumber = phoneNumberUtil.Parse(mobileNumber, null);
        return phoneNumberUtil.IsValidNumber(phoneNumber);
    }
}
