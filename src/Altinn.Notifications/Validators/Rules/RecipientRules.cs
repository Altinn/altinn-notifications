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
    /// Adds validation rules for a collection of preferred recipients in a notification order request.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder to which the validation rules will be added.</param>
    /// <returns>The rule builder options with the added validation rules.</returns>
    /// <remarks>
    /// This method ensures that each recipient in the collection has a valid email address, mobile number, organization number, or national identity number.
    /// It also ensures that the email address, mobile number, national identity number, and organization number are in the correct format and that certain combinations are not allowed.
    /// </remarks>
    public static IRuleBuilderOptions<T, IEnumerable<RecipientExt>> ValidatePreferredRecipients<T>(this IRuleBuilderInitial<T, IEnumerable<RecipientExt>> ruleBuilder)
    {
        return ruleBuilder
        .ChildRules(recipients =>
        {
            recipients.RuleFor(recipients => recipients)
                .MustProvideRecipient();

            recipients.RuleForEach(recipient => recipient)
                .ChildRules(recipient =>
                {
                    recipient
                        .RuleFor(r => r)
                        .Must(r => !string.IsNullOrEmpty(r.EmailAddress) ||
                              !string.IsNullOrEmpty(r.MobileNumber) ||
                              !string.IsNullOrEmpty(r.OrganizationNumber) ||
                              !string.IsNullOrEmpty(r.NationalIdentityNumber))
                        .WithMessage("Either a valid email address, mobile number starting with country code, organization number, or national identity number must be provided for each recipient.");

                    recipient.RuleFor(r => r.EmailAddress).MustBeValidEmail();
                    recipient.RuleFor(r => r.MobileNumber).MustBeValidMobileNumber();
                    recipient.RuleFor(r => r.NationalIdentityNumber).MustBeValidNationalIdentityNumber();
                    recipient.RuleFor(r => r.OrganizationNumber).MustBeValidOrganizationNumber();
                    recipient.RuleFor(r => r).MustNotCombineNationalIdentityNumber();
                    recipient.RuleFor(r => r).MustNotCombineOrganizationNumber();
                });
        });
    }

    /// <summary>
    /// Adds validation rules for a collection of SMS recipients in a notification order request.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder to which the validation rules will be added.</param>
    /// <returns>The rule builder options with the added validation rules.</returns>
    /// <remarks>
    /// This method ensures that each recipient in the collection has a valid mobile number, organization number, or national identity number.
    /// It also ensures that the mobile number, national identity number, and organization number are in the correct format and that certain combinations are not allowed.
    /// </remarks>
    public static IRuleBuilderOptions<T, IEnumerable<RecipientExt>> ValidateSmsRecipients<T>(this IRuleBuilderInitial<T, IEnumerable<RecipientExt>> ruleBuilder)
    {
        return ruleBuilder
        .ChildRules(recipients =>
        {
            recipients.RuleFor(recipients => recipients)
                .MustProvideRecipient();

            recipients.RuleForEach(recipient => recipient)
                .ChildRules(recipient =>
                {
                    recipient.RuleFor(r => r)
                         .Must(r => !string.IsNullOrEmpty(r.MobileNumber) ||
                         !string.IsNullOrEmpty(r.OrganizationNumber) ||
                         !string.IsNullOrEmpty(r.NationalIdentityNumber))
                         .WithMessage("Either a valid mobile number starting with country code, organization number, or national identity number must be provided for each recipient.");

                    recipient.RuleFor(r => r.MobileNumber).MustBeValidMobileNumber();
                    recipient.RuleFor(r => r.NationalIdentityNumber).MustBeValidNationalIdentityNumber();
                    recipient.RuleFor(r => r.OrganizationNumber).MustBeValidOrganizationNumber();
                    recipient.RuleFor(r => r).MustNotCombineNationalIdentityNumber();
                    recipient.RuleFor(r => r).MustNotCombineOrganizationNumber();
                });
        });
    }

    /// <summary>
    /// Adds validation rules for a collection of email recipients in a notification order request.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder to which the validation rules will be added.</param>
    /// <returns>The rule builder options with the added validation rules.</returns>
    /// <remarks>
    /// This method ensures that each recipient in the collection has a valid email address, organization number, or national identity number.
    /// It also ensures that the email address, national identity number, and organization number are in the correct format and that certain combinations are not allowed.
    /// </remarks>
    public static IRuleBuilderOptions<T, IEnumerable<RecipientExt>> ValidateEmailRecipients<T>(this IRuleBuilderInitial<T, IEnumerable<RecipientExt>> ruleBuilder)
    {
        return ruleBuilder
        .ChildRules(recipients =>
        {
            recipients.RuleFor(recipients => recipients)
                .MustProvideRecipient();

            recipients.RuleForEach(recipient => recipient)
                .ChildRules(recipient =>
                {
                    recipient
                     .RuleFor(r => r)
                     .Must(r => !string.IsNullOrEmpty(r.EmailAddress) ||
                     !string.IsNullOrEmpty(r.OrganizationNumber) ||
                     !string.IsNullOrEmpty(r.NationalIdentityNumber))
                     .WithMessage("Either a valid email address, organization number, or national identity number must be provided for each recipient.");

                    recipient.RuleFor(r => r.EmailAddress).MustBeValidEmail();
                    recipient.RuleFor(r => r.NationalIdentityNumber).MustBeValidNationalIdentityNumber();
                    recipient.RuleFor(r => r.OrganizationNumber).MustBeValidOrganizationNumber();
                    recipient.RuleFor(r => r).MustNotCombineNationalIdentityNumber();
                    recipient.RuleFor(r => r).MustNotCombineOrganizationNumber();
                });
        });
    }

    private static IRuleBuilderOptions<T, IEnumerable<RecipientExt>> MustProvideRecipient<T>(this IRuleBuilder<T, IEnumerable<RecipientExt>> ruleBuilder)
    {
        return ruleBuilder
           .ChildRules(recipients =>
           {
               recipients
               .RuleFor(recipients => recipients)
                  .NotEmpty()
                  .WithMessage("One or more recipient is required.");
           });
    }

    private static IRuleBuilderOptions<T, string?> MustBeValidEmail<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
             .ChildRules(email =>
             {
                 email.RuleFor(email => email)
                    .Must(email => IsValidEmail(email))
                    .When(email => !string.IsNullOrEmpty(email))
                    .WithMessage("Invalid email address format.");
             });
    }

    private static IRuleBuilderOptions<T, string?> MustBeValidMobileNumber<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
        .ChildRules(mobileNumber =>
        {
            mobileNumber.RuleFor(mobileNumber => mobileNumber)
                .Must(mobileNumber => MobileNumberHelper.IsValidMobileNumber(mobileNumber))
                .When(mobileNumber => !string.IsNullOrEmpty(mobileNumber))
                .WithMessage("Invalid mobile number format.");
        });
    }

    private static IRuleBuilderOptions<T, string?> MustBeValidNationalIdentityNumber<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
        .ChildRules(nationalIdentityNumber =>
        {
            nationalIdentityNumber.RuleFor(nin => nin)
                .Must(nin => nin?.Length == _nationalIdentityNumberLength && nin.All(char.IsDigit))
                .When(nin => !string.IsNullOrEmpty(nin))
                .WithMessage($"National identity number must be {_nationalIdentityNumberLength} digits long.");
        });
    }

    private static IRuleBuilderOptions<T, string?> MustBeValidOrganizationNumber<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
       .ChildRules(organizationNumber =>
       {
           organizationNumber.RuleFor(on => on)
           .Must(on => on?.Length == _organizationNumberLength && on.All(char.IsDigit))
            .When(on => !string.IsNullOrEmpty(on))
            .WithMessage($"Organization number must be {_organizationNumberLength} digits long.");
       });
    }

    private static IRuleBuilderOptions<T, RecipientExt> MustNotCombineNationalIdentityNumber<T>(this IRuleBuilder<T, RecipientExt> ruleBuilder)
    {
        return ruleBuilder
        .ChildRules(recipient =>
        {
            recipient.RuleFor(r => r)
            .Must(r => string.IsNullOrEmpty(r.EmailAddress) && string.IsNullOrEmpty(r.MobileNumber) && string.IsNullOrEmpty(r.OrganizationNumber))
            .When(r => !string.IsNullOrEmpty(r.NationalIdentityNumber))
            .WithMessage("National identity number cannot be combined with email address, mobile number, or organization number.");
        });
    }

    private static IRuleBuilderOptions<T, RecipientExt> MustNotCombineOrganizationNumber<T>(this IRuleBuilder<T, RecipientExt> ruleBuilder)
    {
        return ruleBuilder
        .ChildRules(recipient =>
        {
            recipient.RuleFor(r => r)
                .Must(r => string.IsNullOrEmpty(r.EmailAddress) && string.IsNullOrEmpty(r.MobileNumber) && string.IsNullOrEmpty(r.NationalIdentityNumber))
                .When(r => !string.IsNullOrEmpty(r.OrganizationNumber))
                .WithMessage("Organization number cannot be combined with email address, mobile number, or national identity number.");
        });
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

        string emailRegexPattern = @"((&quot;[^&quot;]+&quot;)|(([a-zA-Z0-9!#$%&amp;'*+\-=?\^_`{|}~])+(\.([a-zA-Z0-9!#$%&amp;'*+\-=?\^_`{|}~])+)*))@((((([a-zA-Z0-9Ê¯Â∆ÿ≈]([a-zA-Z0-9\-Ê¯Â∆ÿ≈]{0,61})[a-zA-Z0-9Ê¯Â∆ÿ≈]\.)|[a-zA-Z0-9Ê¯Â∆ÿ≈]\.){1,9})([a-zA-Z]{2,14}))|((\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})))";

        Regex regex = new(emailRegexPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        Match match = regex.Match(email);

        return match.Success;
    }
}
