using System.Text.RegularExpressions;

using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators.Rules;

/// <summary>
/// Provides validation rules for recipient collections in notification order requests.
/// </summary>
/// <remarks>
/// This class contains extension methods for FluentValidation to validate collections of <see cref="RecipientExt"/> objects.
/// It ensures that each recipient has valid email addresses, mobile numbers, national identity numbers, and organization numbers.
/// </remarks>
public static class RecipientRules
{
    /// <summary>
    /// Required lenth for a national identity number
    /// </summary>
    private const int _nationalIdentityNumberLength = 11;

    /// <summary>
    /// Required length for an organization number
    /// </summary>
    private const int _organizationNumberLength = 9;

    /// <summary>
    /// Validates a collection of recipients in a notification order request.
    /// </summary>
    /// <typeparam name="T">The type of the collection, which must be an enumerable of <see cref="RecipientExt"/>.</typeparam>
    /// <param name="ruleBuilder">The rule builder to which the validation rules will be added.</param>
    /// <returns>The rule builder options with the added validation rules.</returns>
    /// <remarks>
    /// This method ensures that each recipient in the collection has valid email addresses, mobile numbers, national identity numbers, and organization numbers.
    /// It also ensures that the collection is not empty.
    /// </remarks>
    public static IRuleBuilderOptions<T, IEnumerable<RecipientExt>> ValidateRecipients<T>(this IRuleBuilderInitial<T, IEnumerable<RecipientExt>> ruleBuilder)
    {
        return ruleBuilder
            .ChildRules(recipients =>
            {
                recipients.RuleFor(recipients => recipients)
                    .NotEmpty()
                    .WithMessage("One or more recipient is required.");

                recipients.RuleForEach(recipient => recipient)
                    .ChildRules(recipient =>
                    {
                        recipient.RuleFor(r => r.EmailAddress)
                           .Must(email => IsValidEmail(email))
                           .When(r => !string.IsNullOrEmpty(r.EmailAddress))
                           .WithMessage("Invalid email address format.");

                        recipient.RuleFor(r => r.MobileNumber)
                            .Must(mobileNumber => MobileNumberHelper.IsValidMobileNumber(mobileNumber))
                            .When(r => !string.IsNullOrEmpty(r.MobileNumber))
                            .WithMessage("Invalid mobile number format.");

                        recipient.RuleFor(r => r.NationalIdentityNumber)
                            .Must(nin => nin?.Length == _nationalIdentityNumberLength && nin.All(char.IsDigit))
                            .When(r => !string.IsNullOrEmpty(r.NationalIdentityNumber))
                            .WithMessage($"National identity number must be {_nationalIdentityNumberLength} digits long.");

                        recipient.RuleFor(r => r)
                            .Must(r => string.IsNullOrEmpty(r.EmailAddress) && string.IsNullOrEmpty(r.MobileNumber) && string.IsNullOrEmpty(r.OrganizationNumber))
                            .When(r => !string.IsNullOrEmpty(r.NationalIdentityNumber))
                            .WithMessage("National identity number cannot be combined with email address, mobile number, or organization number.");

                        recipient.RuleFor(r => r.OrganizationNumber)
                            .Must(on => on?.Length == _organizationNumberLength && on.All(char.IsDigit))
                            .When(r => !string.IsNullOrEmpty(r.OrganizationNumber))
                            .WithMessage($"Organization number must be {_organizationNumberLength} digits long.");

                        recipient.RuleFor(r => r)
                            .Must(r => string.IsNullOrEmpty(r.EmailAddress) && string.IsNullOrEmpty(r.MobileNumber) && string.IsNullOrEmpty(r.NationalIdentityNumber))
                            .When(r => !string.IsNullOrEmpty(r.OrganizationNumber))
                            .WithMessage("Organization number cannot be combined with email address, mobile number or national identity number.");
                    });
            });
    }

    /// <summary>
    /// Validated as email address based on the Altinn 2 regex
    /// </summary>
    /// <param name="email">The string to validate as an email address</param>
    /// <returns>A boolean indicating that the email is valid or not</returns>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return false;
        }

        string emailRegexPattern = @"((&quot;[^&quot;]+&quot;)|(([a-zA-Z0-9!#$%&amp;'*+\-=?\^_`{|}~])+(\.([a-zA-Z0-9!#$%&amp;'*+\-=?\^_`{|}~])+)*))@((((([a-zA-Z0-9Ê¯Â∆ÿ≈]([a-zA-Z0-9\-Ê¯Â∆ÿ≈]{0,61})[a-zA-Z0-9Ê¯Â∆ÿ≈]\.)|[a-zA-Z0-9Ê¯Â∆ÿ≈]\.){1,9})([a-zA-Z]{2,14}))|((\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})))";

        Regex regex = new(emailRegexPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        Match match = regex.Match(email);

        return match.Success;
    }
}
