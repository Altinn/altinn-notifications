using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Email;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class RecipientEmailWithAttachmentsValidatorTests
{
    private readonly RecipientEmailWithAttachmentsValidator _validator = new();

    private const string _validSasUrl =
        "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
        "?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature";

    private static RecipientEmailWithAttachmentsExt ValidRecipient() => new()
    {
        EmailAddress = "recipient@agency.no",
        Settings = new EmailWithAttachmentsSendingOptionsExt
        {
            Subject = "Decision from Altinn",
            Body = "Please see the attached document.",
            Attachments =
            [
                new EmailAttachmentExt
                {
                    Filename = "decision.pdf",
                    MimeType = "application/pdf",
                    SasUrl = _validSasUrl
                }
            ]
        }
    };

    [Fact]
    public void Validate_ValidRecipient_NoErrors()
    {
        var result = _validator.TestValidate(ValidRecipient());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyEmailAddress_HasError(string emailAddress)
    {
        var recipient = new RecipientEmailWithAttachmentsExt
        {
            EmailAddress = emailAddress,
            Settings = ValidRecipient().Settings
        };
        var result = _validator.TestValidate(recipient);
        result.ShouldHaveValidationErrorFor(r => r!.EmailAddress);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain.no")]
    public void Validate_InvalidEmailAddress_HasError(string emailAddress)
    {
        var recipient = new RecipientEmailWithAttachmentsExt
        {
            EmailAddress = emailAddress,
            Settings = ValidRecipient().Settings
        };
        var result = _validator.TestValidate(recipient);
        result.ShouldHaveValidationErrorFor(r => r!.EmailAddress)
            .WithErrorMessage("Invalid email address format.");
    }

    [Theory]
    [InlineData("user@altinn.no")]
    [InlineData("recipient+test@agency.no")]
    [InlineData("caseworker@municipality.no")]
    public void Validate_ValidEmailAddresses_NoError(string emailAddress)
    {
        var recipient = new RecipientEmailWithAttachmentsExt
        {
            EmailAddress = emailAddress,
            Settings = ValidRecipient().Settings
        };
        var result = _validator.TestValidate(recipient);
        result.ShouldNotHaveValidationErrorFor(r => r!.EmailAddress);
    }

    [Fact]
    public void Validate_NullSettings_HasError()
    {
        var recipient = new RecipientEmailWithAttachmentsExt
        {
            EmailAddress = "recipient@agency.no",
            Settings = null!
        };
        var result = _validator.TestValidate(recipient);
        result.ShouldHaveValidationErrorFor(r => r!.Settings)
            .WithErrorMessage("Recipient email settings cannot be null.");
    }

    [Fact]
    public void Validate_EmptyAttachmentsList_HasError()
    {
        var recipient = new RecipientEmailWithAttachmentsExt
        {
            EmailAddress = "recipient@agency.no",
            Settings = new EmailWithAttachmentsSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments = []
            }
        };

        var result = _validator.TestValidate(recipient);
        result.ShouldHaveValidationErrorFor(r => r!.Settings.Attachments)
            .WithErrorMessage("At least one attachment is required.");
    }

    [Fact]
    public void Validate_AttachmentWithInvalidFilename_HasError()
    {
        var recipient = new RecipientEmailWithAttachmentsExt
        {
            EmailAddress = "recipient@agency.no",
            Settings = new EmailWithAttachmentsSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments =
                [
                    new EmailAttachmentExt
                    {
                        Filename = "../../etc/passwd",
                        MimeType = "application/pdf",
                        SasUrl = _validSasUrl
                    }
                ]
            }
        };

        var result = _validator.TestValidate(recipient);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_AttachmentWithUnsupportedMimeType_HasError()
    {
        var recipient = new RecipientEmailWithAttachmentsExt
        {
            EmailAddress = "recipient@agency.no",
            Settings = new EmailWithAttachmentsSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments =
                [
                    new EmailAttachmentExt
                    {
                        Filename = "script.sh",
                        MimeType = "application/x-sh",
                        SasUrl = _validSasUrl
                    }
                ]
            }
        };

        var result = _validator.TestValidate(recipient);
        Assert.NotEmpty(result.Errors);
    }
}
