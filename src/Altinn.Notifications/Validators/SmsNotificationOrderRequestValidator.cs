using System.Text.RegularExpressions;

using Altinn.Notifications.Models;

using FluentValidation;

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
            .Must(recipients => recipients.TrueForAll(a => IsValidPhoneNumber(a.PhoneNumber)))
            .WithMessage("A valid phone number must be provided for all recipients.");

        RuleFor(order => order.RequestedSendTime)
            .Must(sendTime => sendTime.Kind != DateTimeKind.Unspecified)
            .WithMessage("The requested send time value must have specified a time zone.")
            .Must(sendTime => sendTime >= DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Send time must be in the future. Leave blank to send immediately.");

        RuleFor(order => order.Body).NotEmpty();
        RuleFor(order => order.SenderNumber).NotEmpty();
    }

    /// <summary>
    /// Validated as phone number based on the Altinn 2 regex
    /// </summary>
    /// <param name="phoneNumber">The string to validate as an phone number</param>
    /// <returns>A boolean indicating that the phone number is valid or not</returns>
    internal static bool IsValidPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
        {
            return false;
        }

        string phoneNumberRegexPattern = @"^(([0-9]{5})|([0-9]{8})|(00[0-9]{3,})|(+[0-9]{3,}))$";

        Regex regex = new(phoneNumberRegexPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        Match match = regex.Match(phoneNumber);

        return match.Success;
    }
}
