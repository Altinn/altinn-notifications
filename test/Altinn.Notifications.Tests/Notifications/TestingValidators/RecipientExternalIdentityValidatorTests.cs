using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Recipient;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class RecipientExternalIdentityValidatorTests
{
    private readonly RecipientExternalIdentityValidator _validator = new();

    [Fact]
    public void Validate_EmailChannelWithEmailSettings_NoError()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ChannelSchema = NotificationChannelExt.Email,
            ExternalIdentity = "urn:altinn:person:idporten-email:user@example.com",
            EmailSettings = new EmailSendingOptionsExt
            {
                Body = "Test body",
                Subject = "Test subject"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(r => r.SmsSettings);
        actual.ShouldNotHaveValidationErrorFor(r => r.EmailSettings);
    }

    [Fact]
    public void Validate_EmailPreferredMissingSettings_ReturnsError()
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

    [Theory]
    [InlineData("", "ExternalIdentity cannot be null or empty.")]
    [InlineData(null, "ExternalIdentity cannot be null or empty.")]
    public void Validate_EmptyExternalIdentity_ReturnsError(string? externalIdentity, string errorMessage)
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
    public void Validate_InvalidExternalIdentityFormat_ReturnsError(string externalIdentity)
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

    [Fact]
    public void Validate_InvalidResourceIdPrefix_ReturnsError()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ChannelSchema = NotificationChannelExt.Email,
            ResourceId = "burn:altinn:resource:some-resource",
            ExternalIdentity = "urn:altinn:person:idporten-email:user@example.com",
            EmailSettings = new EmailSendingOptionsExt
            {
                Body = "Test body",
                Subject = "Test subject"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldHaveValidationErrorFor(r => r.ResourceId).WithErrorMessage("ResourceId must have a valid syntax.");
    }

    [Fact]
    public void Validate_NullResourceId_NoError()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ResourceId = null,
            ChannelSchema = NotificationChannelExt.Email,
            ExternalIdentity = "urn:altinn:person:idporten-email:user@example.com",
            EmailSettings = new EmailSendingOptionsExt
            {
                Body = "Test body",
                Subject = "Test subject"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(r => r.ResourceId);
    }

    [Fact]
    public void Validate_SmsChannelWithSmsSettings_NoError()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ChannelSchema = NotificationChannelExt.Sms,
            ExternalIdentity = "urn:altinn:person:legacy-selfidentified:johndoe",
            SmsSettings = new SmsSendingOptionsExt
            {
                Body = "Test body",
                Sender = "Test sender"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(r => r.SmsSettings);
        actual.ShouldNotHaveValidationErrorFor(r => r.EmailSettings);
    }

    [Fact]
    public void Validate_SmsPreferredMissingSettings_ReturnsError()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ChannelSchema = NotificationChannelExt.SmsPreferred,
            ExternalIdentity = "urn:altinn:person:legacy-selfidentified:johndoe"
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
    public void Validate_SmsPreferredWithBothSettings_NoError()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ChannelSchema = NotificationChannelExt.SmsPreferred,
            ExternalIdentity = "urn:altinn:person:legacy-selfidentified:johndoe",
            EmailSettings = new EmailSendingOptionsExt
            {
                Body = "Test body",
                Subject = "Test subject"
            },
            SmsSettings = new SmsSendingOptionsExt
            {
                Body = "Test body",
                Sender = "Test sender"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(e => e.SmsSettings);
        actual.ShouldNotHaveValidationErrorFor(e => e.EmailSettings);
    }

    [Theory]
    [InlineData("urn:altinn:username:john.doe")]
    [InlineData("urn:altinn:party:username:user123")]
    [InlineData("urn:altinn:person:legacy-selfidentified:user123")]
    [InlineData("urn:altinn:person:legacy-selfidentified:john.doe")]
    [InlineData("urn:altinn:person:idporten-email:user@example.com")]
    public void Validate_ValidExternalIdentity_NoError(string externalIdentity)
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
        actual.ShouldNotHaveValidationErrorFor(r => r.ExternalIdentity);
    }

    [Fact]
    public void Validate_ValidResourceIdPrefix_NoError()
    {
        // Arrange
        var recipient = new RecipientExternalIdentityExt
        {
            ChannelSchema = NotificationChannelExt.Email,
            ResourceId = "urn:altinn:resource:some-resource",
            ExternalIdentity = "urn:altinn:person:idporten-email:user@example.com",
            EmailSettings = new EmailSendingOptionsExt
            {
                Body = "Test body",
                Subject = "Test subject"
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(r => r.ResourceId);
    }
}
