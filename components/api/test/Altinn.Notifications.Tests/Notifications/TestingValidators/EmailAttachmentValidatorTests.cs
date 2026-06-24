using System;

using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Validators.Email;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class EmailAttachmentValidatorTests
{
    private readonly EmailAttachmentValidator _validator = new();

    /// <summary>
    /// A valid SAS URL with a far-future expiry, used as a baseline in tests that are not
    /// testing the SAS URL field itself.
    /// </summary>
    private const string _validSasUrl =
        "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
        "?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature";

    [Fact]
    public void Validate_ValidPdfAttachment_NoErrors()
    {
        // Arrange
        var emailAttachment = new EmailAttachmentExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = _validSasUrl
        };

        // Act
        var result = _validator.TestValidate(emailAttachment);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
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
    public void Validate_AllCommonAcsSupportedTypes_NoErrors(string filename, string mimeType)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = filename,
            MimeType = mimeType,
            SasUrl = _validSasUrl
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Validate_EmptyOrWhitespaceFilename_HasError(string filename)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = filename,
            MimeType = "application/pdf",
            SasUrl = _validSasUrl
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldHaveValidationErrorFor(a => a.Filename)
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
    public void Validate_FilenameWithPathSeparatorOrTraversal_HasError(string filename)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = filename,
            MimeType = "application/pdf",
            SasUrl = _validSasUrl
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldHaveValidationErrorFor(a => a.Filename)
            .WithErrorMessage("Attachment filename must not contain path separators or traversal sequences, and must include a file extension.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceMimeType_HasError(string mimeType)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = "contract.pdf",
            MimeType = mimeType,
            SasUrl = _validSasUrl
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldHaveValidationErrorFor(a => a.MimeType)
            .WithErrorMessage("Attachment mimeType must not be empty.");
    }

    [Theory]
    [InlineData("image/jpg")]
    [InlineData("text/html")]
    [InlineData("notamimetype")]
    [InlineData("application/x-sh")]
    [InlineData("application/x-zip")]
    [InlineData("application/x-msdownload")]
    [InlineData("application/octet-stream")]
    public void Validate_UnsupportedMimeType_HasError(string mimeType)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = "file.bin",
            MimeType = mimeType,
            SasUrl = _validSasUrl
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldHaveValidationErrorFor(a => a.MimeType)
            .WithErrorMessage("Attachment mimeType is not supported. Refer to ACS documentation for the list of accepted MIME types.");
    }

    [Theory]
    [InlineData("IMAGE/PNG")]
    [InlineData("Image/Jpeg")]
    [InlineData("APPLICATION/PDF")]
    [InlineData("Application/Pdf")]
    public void Validate_MimeTypeCaseVariants_NoError(string mimeType)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = "file.pdf",
            MimeType = mimeType,
            SasUrl = _validSasUrl
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldNotHaveValidationErrorFor(a => a.MimeType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceSasUrl_HasError(string sasUrl)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = sasUrl
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldHaveValidationErrorFor(a => a.SasUrl)
            .WithErrorMessage("Attachment sasUrl must not be empty.");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("relative/path/file.pdf")]
    [InlineData("//account.blob.core.windows.net/container/file.pdf")]
    [InlineData("ftp://account.blob.core.windows.net/container/file.pdf?se=2099-01-01T00%3A00%3A00Z&sig=x")]
    [InlineData("http://account.blob.core.windows.net/container/file.pdf?se=2099-01-01T00%3A00%3A00Z&sig=x")]
    [InlineData("http://localhost:10000/devstoreaccount1/attachments/file.pdf?se=2099-01-01T00%3A00%3A00Z&sig=x")]
    public void Validate_InvalidSasUrlSchemeOrFormat_HasError(string sasUrl)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = sasUrl
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldHaveValidationErrorFor(a => a.SasUrl)
            .WithErrorMessage("Attachment sasUrl must be an absolute HTTPS URI.");
    }

    [Fact]
    public void Validate_SasUrlMissingSeParameter_HasError()
    {
        // Arrange — valid HTTPS URL but no 'se' query parameter
        var attachment = new EmailAttachmentExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = "https://account.blob.core.windows.net/container/file.pdf?sp=r&sr=b&sig=fakesig"
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldHaveValidationErrorFor(a => a.SasUrl)
            .WithErrorMessage("Attachment sasUrl must contain a valid 'se' (signed expiry) query parameter.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("tomorrow")]
    [InlineData("not-a-date")]
    public void Validate_SasUrlWithUnparseableSeValue_HasError(string seValue)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = $"https://account.blob.core.windows.net/container/file.pdf?se={Uri.EscapeDataString(seValue)}&sp=r&sr=b&sig=fakesig"
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldHaveValidationErrorFor(a => a.SasUrl)
            .WithErrorMessage("Attachment sasUrl must contain a valid 'se' (signed expiry) query parameter.");
    }

    [Fact]
    public void Validate_SasUrlWithPastExpiry_NoErrorFromThisValidator()
    {
        // Arrange — expiry is 2020 (past), but this validator only checks parseability
        var attachment = new EmailAttachmentExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = "https://account.blob.core.windows.net/container/file.pdf?se=2020-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fakesig"
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert — expiry enforcement is the parent validator's responsibility
        result.ShouldNotHaveValidationErrorFor(a => a.SasUrl);
    }

    [Theory]
    [InlineData("https://account.blob.core.windows.net/c/f.pdf?se=2099-06-01T12%3A00%3A00Z&sig=x")]
    [InlineData("https://account.blob.core.windows.net/c/f.pdf?se=2099-06-01T12%3A00%3A00%2B02%3A00&sig=x")]
    public void Validate_SasUrlWithValidSeFormats_NoError(string sasUrl)
    {
        // Arrange
        var attachment = new EmailAttachmentExt
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = sasUrl
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldNotHaveValidationErrorFor(a => a.SasUrl);
    }

    [Fact]
    public void Validate_ServiceOwnerAttachesPdfContractForCitizen_NoErrors()
    {
        // Arrange — typical Altinn use case: org attaches a signed contract
        var attachment = new EmailAttachmentExt
        {
            Filename = "vedtak_2025_123456.pdf",
            MimeType = "application/pdf",
            SasUrl =
                "https://org123storage.blob.core.windows.net/outgoing/vedtak_2025_123456.pdf" +
                "?se=2025-12-31T23%3A59%3A59Z&sp=r&sr=b&spr=https&sv=2023-11-03&sig=abc123"
        };

        // Act
        var result = _validator.TestValidate(attachment);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
