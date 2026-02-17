using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Recipient;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class RecipientExternalIdentityValidatorTests
{
    private readonly RecipientExternalIdentityValidator _validator = new();

    [Theory]
    [InlineData("", "ExternalIdentity cannot be null or empty.")]
    [InlineData(null, "ExternalIdentity cannot be null or empty.")]
    public void Should_Have_Validation_Error_For_ExternalIdentity_When_Empty(string? externalIdentity, string errorMessage)
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ExternalIdentity = externalIdentity!,
            ChannelSchema = NotificationChannelExt.Email,
            EmailSettings = new EmailSendingOptionsExt
            {
                Body = "Test body",
                Subject = "Test subject"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldHaveValidationErrorFor(r => r.ExternalIdentity).WithErrorMessage(errorMessage);
    }

    [Theory]
    [InlineData("invalid-urn-format")]
    [InlineData("urn:altinn:username:")]
    [InlineData("urn:altinn:party:username:")]
    [InlineData("urn:altinn:person:idporten-email:")]
    [InlineData("urn:altinn:person:idporten-email:user@")]
    [InlineData("urn:altinn:person:legacy-selfidentified:")]
    [InlineData("urn:altinn:person:legacy-selfidentified:   ")]
    [InlineData("urn:altinn:person:idporten-email:not-an-email")]
    [InlineData("urn:altinn:person:idporten-email:@example.com")]
    [InlineData("urn:altinn:person:idporten-email:user@example")]
    [InlineData("urn:altinn:person:wrongprefix:user@example.com")]
    public void Should_Have_Validation_Error_For_ExternalIdentity_When_Invalid_Format(string externalIdentity)
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ExternalIdentity = externalIdentity,
            ChannelSchema = NotificationChannelExt.Email,
            EmailSettings = new EmailSendingOptionsExt
            {
                Body = "Test body",
                Subject = "Test subject"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldHaveValidationErrorFor(r => r.ExternalIdentity).WithErrorMessage("Invalid external identity URN format.");
    }

    [Theory]
    [InlineData("urn:altinn:username:john.doe")]
    [InlineData("urn:altinn:party:username:user123")]
    [InlineData("urn:altinn:person:legacy-selfidentified:user123")]
    [InlineData("urn:altinn:person:legacy-selfidentified:john.doe")]
    [InlineData("urn:altinn:person:idporten-email:user@example.com")]
    public void Should_Not_Have_Validation_Error_For_ExternalIdentity_When_Valid(string externalIdentity)
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ExternalIdentity = externalIdentity,
            ChannelSchema = NotificationChannelExt.Email,
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test subject",
                Body = "Test body"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(r => r.ExternalIdentity);
    }

    [Fact]
    public void Should_Not_Have_Validation_Errors_For_Base_Validator_Rules_When_ChannelSchema_Email_And_EmailSettings_Set()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ExternalIdentity = "urn:altinn:person:idporten-email:user@example.com",
            ChannelSchema = NotificationChannelExt.Email,
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test subject",
                Body = "Test body"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(r => r.SmsSettings);
        actual.ShouldNotHaveValidationErrorFor(r => r.EmailSettings);
    }

    [Fact]
    public void Should_Have_Validation_Errors_When_ChannelSchema_EmailPreferred_And_Missing_Settings()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ExternalIdentity = "urn:altinn:person:idporten-email:user@example.com",
            ChannelSchema = NotificationChannelExt.EmailPreferred
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldHaveValidationErrorFor(r => r.EmailSettings)
            .WithErrorMessage("EmailSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred");

        actual.ShouldHaveValidationErrorFor(r => r.SmsSettings)
            .WithErrorMessage("SmsSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_ResourceId_When_Null()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ExternalIdentity = "urn:altinn:person:idporten-email:user@example.com",
            ResourceId = null,
            ChannelSchema = NotificationChannelExt.Email,
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test subject",
                Body = "Test body"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(r => r.ResourceId);
    }

    [Fact]
    public void Should_Have_Validation_Error_For_ResourceId_When_Invalid_Prefix()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ExternalIdentity = "urn:altinn:person:idporten-email:user@example.com",
            ResourceId = "burn:altinn:resource:some-resource",
            ChannelSchema = NotificationChannelExt.Email,
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test subject",
                Body = "Test body"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldHaveValidationErrorFor(r => r.ResourceId).WithErrorMessage("ResourceId must have a valid syntax.");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_ResourceId_When_Valid_Prefix()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ExternalIdentity = "urn:altinn:person:idporten-email:user@example.com",
            ResourceId = "urn:altinn:resource:some-resource",
            ChannelSchema = NotificationChannelExt.Email,
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test subject",
                Body = "Test body"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(r => r.ResourceId);
    }
}
