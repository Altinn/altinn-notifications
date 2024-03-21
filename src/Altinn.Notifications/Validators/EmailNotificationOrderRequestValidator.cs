using System.Text.RegularExpressions;

using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Class containing validation logic for the <see cref="EmailNotificationOrderRequestExt"/> model
/// </summary>
public class EmailNotificationOrderRequestValidator : AbstractValidator<EmailNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrderRequestValidator"/> class.
    /// </summary>
    public EmailNotificationOrderRequestValidator()
    {
        RuleFor(order => order.Recipients)
            .Must(r => r?.Count is >= 1 and <= 50) // Azure Communication Services has max 10 recipients
            .WithMessage("1-50 recipients is required.")
            .Must(recipients => recipients.TrueForAll(a => IsValidEmail(a.EmailAddress)))
            .WithMessage("A valid email address must be provided for all recipients.");

        RuleFor(order => order.RequestedSendTime)
            .Must(sendTime => sendTime.Kind != DateTimeKind.Unspecified)
            .WithMessage("The requested send time value must have specified a time zone.")
            .Must(sendTime => sendTime >= DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Send time must be in the future. Leave blank to send immediately.");

        // Azure Communication Services imposes limit on total email request size: <= 10MB
        // https://learn.microsoft.com/en-us/azure/communication-services/concepts/service-limits#size-limits
        RuleFor(order => order.Body)
            .NotEmpty()
            .MaximumLength(8_000_000) // This is a heuristic, to accurately measure we would need the complete serialized payload. This leaves ~2m bytes for subject, recipients etc
            .WithMessage("The email body is too large. Maximum size is 20m characters.");

        RuleFor(order => order.Subject)
            .NotEmpty()
            .MaximumLength(10_000)
            .WithMessage("The email subject is too large. Maximum size is 1000 characters.");
    }

    /// <summary>
    /// Validated as email address based on the Altinn 2 regex
    /// </summary>
    /// <param name="email">The string to validate as an email address</param>
    /// <returns>A boolean indicating that the email is valid or not</returns>
    internal static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return false;
        }

        string emailRegexPattern = @"((&quot;[^&quot;]+&quot;)|(([a-zA-Z0-9!#$%&amp;'*+\-=?\^_`{|}~])+(\.([a-zA-Z0-9!#$%&amp;'*+\-=?\^_`{|}~])+)*))@((((([a-zA-Z0-9æøåÆØÅ]([a-zA-Z0-9\-æøåÆØÅ]{0,61})[a-zA-Z0-9æøåÆØÅ]\.)|[a-zA-Z0-9æøåÆØÅ]\.){1,9})([a-zA-Z]{2,14}))|((\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})))";

        Regex regex = new(emailRegexPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        Match match = regex.Match(email);

        return match.Success;
    }
}
