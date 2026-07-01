using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Files;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Recipient;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class RecipientComposedEmailValidatorTests
{
    private readonly RecipientComposedEmailValidator _validator = new();

    private const string _validSasUrl =
        "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
        "?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature";

    private static RecipientComposedEmailExt RecipientWithFileReference(SasFileReferenceExt fileReference) => new()
    {
        EmailAddress = "recipient@altinnxyz.no",
        Settings = new ComposedEmailSendingOptionsExt
        {
            Subject = "Decision from Altinn",
            Body = "Please see the attached document.",
            Attachments = [fileReference]
        }
    };

    private static RecipientComposedEmailExt ValidRecipientWithoutFileReferences() => new()
    {
        EmailAddress = "recipient@altinnxyz.no",
        Settings = new ComposedEmailSendingOptionsExt
        {
            Subject = "Decision from Altinn",
            Body = "Please see the attached document."
        }
    };

    private static RecipientComposedEmailExt ValidRecipientWithSingleFileReference() => new()
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
        // Arrange
        var recipient = ValidRecipientWithoutFileReferences();

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyEmailAddress_HasError(string emailAddress)
    {
        // Arrange
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = emailAddress,
            Settings = ValidRecipientWithSingleFileReference().Settings
        };

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r!.EmailAddress);
    }

    [Theory]
    [InlineData("missing@")]
    [InlineData("notanemail")]
    [InlineData("@altinnxyz.no")]
    public void Validate_InvalidEmailAddress_HasError(string emailAddress)
    {
        // Arrange
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = emailAddress,
            Settings = ValidRecipientWithSingleFileReference().Settings
        };

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r!.EmailAddress)
            .WithErrorMessage("Invalid email address format.");
    }

    [Theory]
    [InlineData("user@altinnxyz.no")]
    [InlineData("caseworker@altinnxyz.no")]
    [InlineData("recipient+test@altinnxyz.no")]
    public void Validate_ValidEmailAddresses_NoError(string emailAddress)
    {
        // Arrange
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = emailAddress,
            Settings = ValidRecipientWithSingleFileReference().Settings
        };

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r!.EmailAddress);
    }

    [Fact]
    public void Validate_NullSettings_HasError()
    {
        // Arrange
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@altinnxyz.no",
            Settings = null!
        };

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r!.Settings)
            .WithErrorMessage("Recipient email settings cannot be null.");
    }

    [Fact]
    public void Validate_AttachmentWithInvalidFilename_HasError()
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "../../etc/passwd",
            MimeType = "application/pdf",
            SasUrl = _validSasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment '../../etc/passwd': filename must not contain path separators or traversal sequences, and must include a file extension.");
    }

    [Fact]
    public void Validate_AttachmentWithUnsupportedMimeType_HasError()
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "script.sh",
            MimeType = "application/x-sh",
            SasUrl = _validSasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'script.sh': mimeType is not supported. Refer to ACS documentation for the list of accepted MIME types.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_AttachmentEmptyOrWhitespaceSasUrl_HasError(string sasUrl)
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = sasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl must not be empty.");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("relative/path/file.pdf")]
    [InlineData("//account.blob.core.windows.net/container/file.pdf")]
    [InlineData("ftp://account.blob.core.windows.net/container/file.pdf?se=2099-01-01T00%3A00%3A00Z&sig=x")]
    [InlineData("http://account.blob.core.windows.net/container/file.pdf?se=2099-01-01T00%3A00%3A00Z&sig=x")]
    [InlineData("http://localhost:10000/devstoreaccount1/attachments/file.pdf?se=2099-01-01T00%3A00%3A00Z&sig=x")]
    public void Validate_AttachmentInvalidSasUrlSchemeOrFormat_HasError(string sasUrl)
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = sasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl must be an absolute HTTPS URI.");
    }

    [Theory]
    [InlineData("https://evil.com/file.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fake")]
    [InlineData("https://attacker.example.com/file.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fake")]
    [InlineData("https://notazure.blob.example.com/container/file.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fake")]
    public void Validate_AttachmentSasUrlNonAzureBlobHost_HasError(string sasUrl)
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            SasUrl = sasUrl,
            Filename = "contract.pdf",
            MimeType = "application/pdf"
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl host must be within Azure Blob Storage (*.blob.core.windows.net).");
    }

    [Theory]
    [InlineData("https://account.blob.core.windows.net/container/file.pdf?se=2020-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fakesig")]
    public void Validate_AttachmentWithValidSasUrl_NoSasUrlErrors(string sasUrl)
    {
        // Arrange
        // A URL that passes all SAS checks at the recipient level (HTTPS, required params,
        // parseable se, read permission). The expiry relative to requestedSendTime is
        // intentionally not checked here — that is the order-level validator's responsibility.
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = sasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("\t")]
    [InlineData("   ")]
    public void Validate_AttachmentEmptyOrWhitespaceFilename_HasError(string filename)
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = filename,
            MimeType = "application/pdf",
            SasUrl = _validSasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment filename must not be empty.");
    }

    [Theory]
    [InlineData("a/b/file.pdf")]
    [InlineData("../secret.pdf")]
    [InlineData("C:\\Users\\file.pdf")]
    [InlineData("../../etc/passwd.txt")]
    [InlineData("folder/../contract.pdf")]
    [InlineData("subfolder/contract.pdf")]
    [InlineData("subfolder\\contract.pdf")]
    public void Validate_AttachmentFilenameWithPathSeparatorOrTraversal_HasError(string filename)
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = filename,
            MimeType = "application/pdf",
            SasUrl = _validSasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage($"Attachment '{filename}': filename must not contain path separators or traversal sequences, and must include a file extension.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_AttachmentEmptyOrWhitespaceMimeType_HasError(string mimeType)
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "contract.pdf",
            MimeType = mimeType,
            SasUrl = _validSasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': mimeType must not be empty.");
    }

    [Theory]
    [InlineData("image/jpg")]
    [InlineData("text/html")]
    [InlineData("notamimetype")]
    [InlineData("application/x-sh")]
    [InlineData("application/x-zip")]
    [InlineData("application/x-msdownload")]
    [InlineData("application/octet-stream")]
    public void Validate_AttachmentUnsupportedMimeType_HasError(string mimeType)
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "file.bin",
            MimeType = mimeType,
            SasUrl = _validSasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage($"Attachment 'file.bin': mimeType is not supported. Refer to ACS documentation for the list of accepted MIME types.");
    }

    [Theory]
    [InlineData("IMAGE/PNG")]
    [InlineData("Image/Jpeg")]
    [InlineData("APPLICATION/PDF")]
    [InlineData("Application/Pdf")]
    public void Validate_AttachmentMimeTypeCaseVariants_NoError(string mimeType)
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "file.pdf",
            MimeType = mimeType,
            SasUrl = _validSasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_AttachmentNullItem_HasError()
    {
        // Arrange
        var recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@altinnxyz.no",
            Settings = new ComposedEmailSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments = [null!]
            }
        };

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment item must not be null.");
    }

    [Fact]
    public void Validate_AttachmentSasUrlMissingRequiredParameters_HasError()
    {
        // Arrange
        // HTTPS URL missing 'se' and 'sig'
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = "https://account.blob.core.windows.net/container/file.pdf?sp=r&sr=b"
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl is missing required SAS parameters (se, sig, sp, sr).");
    }

    [Fact]
    public void Validate_AttachmentSasUrlMalformedSeParameter_HasError()
    {
        // Arrange
        // All required params present but 'se' is not a valid ISO 8601 date
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = "https://account.blob.core.windows.net/container/file.pdf?se=not-a-date&sp=r&sr=b&sig=fakesig"
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl has an invalid 'se' (signed expiry) value.");
    }

    [Fact]
    public void Validate_AttachmentSasUrlMissingReadPermission_HasError()
    {
        // Arrange
        // All required params valid, but 'sp' does not contain 'r'
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = "https://account.blob.core.windows.net/container/file.pdf?se=2099-01-01T00%3A00%3A00Z&sp=wdl&sr=b&sig=fakesig"
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldHaveValidationErrors()
            .WithErrorMessage("Attachment 'contract.pdf': sasUrl does not grant read permission ('r' must be present in 'sp').");
    }

    [Theory]
    [InlineData("data.csv", "text/csv")]
    [InlineData("video.mp4", "video/mp4")]
    [InlineData("bilde.png", "image/png")]
    [InlineData("bilde.jpg", "image/jpeg")]
    [InlineData("lydklipp.mp3", "audio/mpeg")]
    [InlineData("tekstfil.txt", "text/plain")]
    [InlineData("arkiv.zip", "application/zip")]
    [InlineData("rapport.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("vedlegg.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("presentasjon.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    public void Validate_AttachmentAllCommonAcsSupportedTypes_NoErrors(string filename, string mimeType)
    {
        // Arrange
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = filename,
            MimeType = mimeType,
            SasUrl = _validSasUrl
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_AttachmentServiceOwnerPdfContractForCitizen_NoErrors()
    {
        // Arrange
        // Typical Altinn use case: org attaches a signed contract
        var recipient = RecipientWithFileReference(new SasFileReferenceExt
        {
            Filename = "vedtak_2025_123456.pdf",
            MimeType = "application/pdf",
            SasUrl =
                "https://org123storage.blob.core.windows.net/outgoing/vedtak_2025_123456.pdf" +
                "?se=2025-12-31T23%3A59%3A59Z&sp=r&sr=b&spr=https&sv=2023-11-03&sig=abc123"
        });

        // Act
        var result = _validator.TestValidate(recipient);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
