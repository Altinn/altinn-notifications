using System;

using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Files;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class ComposedEmailRequestValidatorTests
{
    private static readonly Uri _validSasUrl = new(
        "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
        "?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature");

    private static RecipientComposedEmailExt RecipientWithSingleFileReference(string emailAddress = "recipient@altinnxyz.no") => new()
    {
        EmailAddress = emailAddress,
        Settings = new ComposedEmailSendingOptionsExt
        {
            Subject = "Decision from Altinn",
            Body = "Please see the attached document.",
            Attachments =
            [
                new SasFileReferenceExt
                {
                    Filename = "contract.pdf",
                    MimeType = "application/pdf",
                    SasUrl = _validSasUrl
                }
            ]
        }
    };

    private static ComposedEmailRequestExt ValidComposedEmailRequest(
        string idempotencyId = "order-001",
        DateTime? requestedSendTime = null,
        RecipientComposedEmailExt? recipient = null,
        DialogportenIdentifiersExt? dialogportenAssociation = null) => new()
        {
            SendersReference = "ref-001",
            IdempotencyId = idempotencyId,
            DialogportenAssociation = dialogportenAssociation,
            Recipient = recipient ?? RecipientWithSingleFileReference(),
            RequestedSendTime = requestedSendTime ?? DateTime.UtcNow.AddHours(1)
        };

    private static readonly ComposedEmailRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_NoErrors()
    {
        // Arrange
        var request = ValidComposedEmailRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyIdempotencyId_HasError(string idempotencyId)
    {
        // Arrange
        var request = ValidComposedEmailRequest(idempotencyId: idempotencyId);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.IdempotencyId)
            .WithErrorMessage("IdempotencyId cannot be null or empty.");
    }

    [Fact]
    public void Validate_RequestedSendTimeInThePast_HasError()
    {
        // Arrange
        var request = ValidComposedEmailRequest(requestedSendTime: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("RequestedSendTime must be greater than or equal to now.");
    }

    [Fact]
    public void Validate_AttachmentSasExpiryTooCloseToSendTime_HasError()
    {
        // Arrange
        var sendTime = DateTime.UtcNow.AddHours(2);
        var expiryTooSoon = sendTime.AddMinutes(10).ToString("o");
        var sasUrlWithShortExpiry = new Uri(
            "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
            $"?se={Uri.EscapeDataString(expiryTooSoon)}&sp=r&sr=b&spr=https&sig=fakesignature");

        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@altinnxyz.no",
            Settings = new ComposedEmailSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments =
                [
                    new SasFileReferenceExt
                    {
                        Filename = "contract.pdf",
                        MimeType = "application/pdf",
                        SasUrl = sasUrlWithShortExpiry
                    }
                ]
            }
        };

        // Act
        var result = _validator.TestValidate(ValidComposedEmailRequest(requestedSendTime: sendTime, recipient: recipient));

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl must be valid for at least 15 minutes after requestedSendTime.");
    }

    [Fact]
    public void Validate_AttachmentSasUrlMissingSeParameter_HasDistinctError()
    {
        // Arrange
        // URL is valid HTTPS with other required params but 'se' is absent — inner validator fires "missing required SAS parameters"
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@altinnxyz.no",
            Settings = new ComposedEmailSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments =
                [
                    new SasFileReferenceExt
                    {
                        Filename = "contract.pdf",
                        MimeType = "application/pdf",
                        SasUrl = new Uri("https://account.blob.core.windows.net/container/file.pdf?sp=r&sr=b&sig=fakesig")
                    }
                ]
            }
        };

        // Act
        var result = _validator.TestValidate(ValidComposedEmailRequest(recipient: recipient));

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl is missing required SAS parameters (se, sig, sp, sr).");
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_AttachmentSasUrlWithMalformedSe_HasDistinctError()
    {
        // Arrange
        // URL has all required params but 'se' is not a valid date — distinct from "expiry too short"
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@altinnxyz.no",
            Settings = new ComposedEmailSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments =
                [
                    new SasFileReferenceExt
                    {
                        Filename = "contract.pdf",
                        MimeType = "application/pdf",
                        SasUrl = new Uri("https://account.blob.core.windows.net/container/file.pdf?se=not-a-date&sp=r&sr=b&sig=fakesig")
                    }
                ]
            }
        };

        // Act
        var result = _validator.TestValidate(ValidComposedEmailRequest(recipient: recipient));

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl has an invalid 'se' (signed expiry) value.");
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_InvalidRecipientEmailAddress_HasError()
    {
        // Arrange
        var request = ValidComposedEmailRequest(recipient: RecipientWithSingleFileReference(emailAddress: "not-an-email"));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Invalid email address format.");
    }

    [Fact]
    public void Validate_ValidDialogportenAssociation_NoErrors()
    {
        // Arrange
        var association = new DialogportenIdentifiersExt
        {
            DialogId = "dialog-001",
            TransmissionId = "transmission-001"
        };

        // Act
        var result = _validator.TestValidate(ValidComposedEmailRequest(dialogportenAssociation: association));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_AttachmentWithNullSasUrl_SkipsExpiryCheck()
    {
        // Arrange
        var sendTime = DateTime.UtcNow.AddHours(1);
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@altinnxyz.no",
            Settings = new ComposedEmailSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments =
                [
                    new SasFileReferenceExt
                    {
                        Filename = "contract.pdf",
                        MimeType = "application/pdf",
                        SasUrl = null!
                    }
                ]
            }
        };

        // Act
        var result = _validator.TestValidate(ValidComposedEmailRequest(requestedSendTime: sendTime, recipient: recipient));

        // Assert — the order-level expiry check must not have fired; other sasUrl errors are expected
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage.Contains("at least 15 minutes"));
    }
}
