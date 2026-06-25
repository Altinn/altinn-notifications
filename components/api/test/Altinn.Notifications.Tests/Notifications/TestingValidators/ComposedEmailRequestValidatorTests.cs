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
    private const string _validSasUrl =
        "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
        "?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature";

    private static RecipientComposedEmailExt ValidRecipient(string emailAddress = "recipient@agency.no") => new()
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

    private static ComposedEmailRequestExt ValidRequest(
        string idempotencyId = "order-001",
        DateTime? requestedSendTime = null,
        RecipientComposedEmailExt? recipient = null,
        DialogportenIdentifiersExt? dialogportenAssociation = null) => new()
        {
            SendersReference = "ref-001",
            IdempotencyId = idempotencyId,
            Recipient = recipient ?? ValidRecipient(),
            DialogportenAssociation = dialogportenAssociation,
            RequestedSendTime = requestedSendTime ?? DateTime.UtcNow.AddHours(1)
        };

    private static readonly ComposedEmailRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_NoErrors()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyIdempotencyId_HasError(string idempotencyId)
    {
        var result = _validator.TestValidate(ValidRequest(idempotencyId: idempotencyId));
        result.ShouldHaveValidationErrorFor(r => r.IdempotencyId)
            .WithErrorMessage("IdempotencyId cannot be null or empty.");
    }

    [Fact]
    public void Validate_RequestedSendTimeInThePast_HasError()
    {
        var result = _validator.TestValidate(ValidRequest(requestedSendTime: DateTime.UtcNow.AddDays(-1)));
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("RequestedSendTime must be greater than or equal to now.");
    }

    [Fact]
    public void Validate_AttachmentSasExpiryTooCloseToSendTime_HasError()
    {
        var sendTime = DateTime.UtcNow.AddHours(2);
        var expiryTooSoon = sendTime.AddMinutes(10).ToString("o");
        var sasUrlWithShortExpiry =
            "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
            $"?se={Uri.EscapeDataString(expiryTooSoon)}&sp=r&sr=b&spr=https&sig=fakesignature";

        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@agency.no",
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

        var result = _validator.TestValidate(ValidRequest(requestedSendTime: sendTime, recipient: recipient));
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl must be valid for at least 15 minutes after requestedSendTime.");
    }

    [Fact]
    public void Validate_AttachmentSasUrlMissingSeParameter_HasDistinctError()
    {
        // URL is valid HTTPS with other required params but 'se' is absent — inner validator fires "missing required SAS parameters"
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@agency.no",
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
                        SasUrl = "https://account.blob.core.windows.net/container/file.pdf?sp=r&sr=b&sig=fakesig"
                    }
                ]
            }
        };

        var result = _validator.TestValidate(ValidRequest(recipient: recipient));
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl is missing required SAS parameters (se, sig, sp, sr).");
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_AttachmentSasUrlWithMalformedSe_HasDistinctError()
    {
        // URL has all required params but 'se' is not a valid date — distinct from "expiry too short"
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@agency.no",
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
                        SasUrl = "https://account.blob.core.windows.net/container/file.pdf?se=not-a-date&sp=r&sr=b&sig=fakesig"
                    }
                ]
            }
        };

        var result = _validator.TestValidate(ValidRequest(recipient: recipient));
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl has an invalid 'se' (signed expiry) value.");
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_InvalidRecipientEmailAddress_HasError()
    {
        var result = _validator.TestValidate(ValidRequest(recipient: ValidRecipient(emailAddress: "not-an-email")));
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Invalid email address format.");
    }

    [Fact]
    public void Validate_ValidDialogportenAssociation_NoErrors()
    {
        var association = new DialogportenIdentifiersExt
        {
            DialogId = "dialog-001",
            TransmissionId = "transmission-001"
        };

        var result = _validator.TestValidate(ValidRequest(dialogportenAssociation: association));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
