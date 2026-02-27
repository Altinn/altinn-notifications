using System;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Orders;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

/// <summary>
/// Tests for mapping flattened email notification order models.
/// </summary>
public class InstantEmailNotificationOrderMapperTests
{
    [Fact]
    public void MapToInstantEmailNotificationOrder_WithValidRequest_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var idempotencyId = "test-idempotency-id";
        var sendersReference = "test-senders-reference";
        var emailAddress = "test@example.com";
        var subject = "Test Email Subject";
        var body = "Test email body content";
        var senderEmailAddress = "sender@altinn.no";

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,
            SendersReference = sendersReference,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = subject,
                    Body = body,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = senderEmailAddress
                }
            }
        };

        // Act
        var result = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created, result.Created);
        Assert.Equal(creatorShortName, result.Creator.ShortName);
        Assert.Equal(idempotencyId, result.IdempotencyId);
        Assert.Equal(sendersReference, result.SendersReference);
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);

        Assert.NotNull(result.InstantEmailDetails);
        Assert.Equal(emailAddress, result.InstantEmailDetails.EmailAddress);

        Assert.NotNull(result.InstantEmailDetails.EmailContent);
        Assert.Equal(subject, result.InstantEmailDetails.EmailContent.Subject);
        Assert.Equal(body, result.InstantEmailDetails.EmailContent.Body);
        Assert.Equal(Altinn.Notifications.Core.Enums.EmailContentType.Plain, result.InstantEmailDetails.EmailContent.ContentType);
        Assert.Equal(senderEmailAddress, result.InstantEmailDetails.EmailContent.FromAddress);
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithHtmlContent_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var emailAddress = "test@example.com";
        var subject = "HTML Email Subject";
        var htmlBody = "<html><body><h1>Test</h1><p>HTML content</p></body></html>";
        var senderEmailAddress = "sender@altinn.no";

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = subject,
                    Body = htmlBody,
                    ContentType = EmailContentTypeExt.Html,
                    SenderEmailAddress = senderEmailAddress
                }
            }
        };

        // Act
        var result = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.InstantEmailDetails.EmailContent);
        Assert.Equal(subject, result.InstantEmailDetails.EmailContent.Subject);
        Assert.Equal(htmlBody, result.InstantEmailDetails.EmailContent.Body);
        Assert.Equal(Altinn.Notifications.Core.Enums.EmailContentType.Html, result.InstantEmailDetails.EmailContent.ContentType);
        Assert.Equal(senderEmailAddress, result.InstantEmailDetails.EmailContent.FromAddress);
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithNullSenderEmailAddress_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var emailAddress = "test@example.com";
        var subject = "Test Subject";
        var body = "Test body";

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = subject,
                    Body = body,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = null // Null sender
                }
            }
        };

        // Act
        var result = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.InstantEmailDetails.EmailContent);
        Assert.Equal(subject, result.InstantEmailDetails.EmailContent.Subject);
        Assert.Equal(body, result.InstantEmailDetails.EmailContent.Body);
        Assert.Null(result.InstantEmailDetails.EmailContent.FromAddress);
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithEmptySenderEmailAddress_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var emailAddress = "test@example.com";
        var subject = "Test Subject";
        var body = "Test body";

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = subject,
                    Body = body,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = string.Empty // Empty sender
                }
            }
        };

        // Act
        var result = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.InstantEmailDetails.EmailContent);
        Assert.Equal(string.Empty, result.InstantEmailDetails.EmailContent.FromAddress);
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithoutSendersReference_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var emailAddress = "test@example.com";

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            SendersReference = null, // No senders reference
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Test Subject",
                    Body = "Test body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SendersReference);
    }

    [Theory]
    [InlineData(EmailContentTypeExt.Plain, Altinn.Notifications.Core.Enums.EmailContentType.Plain)]
    [InlineData(EmailContentTypeExt.Html, Altinn.Notifications.Core.Enums.EmailContentType.Html)]
    public void MapToInstantEmailNotificationOrder_WithDifferentContentTypes_MapsCorrectly(
        EmailContentTypeExt externalContentType,
        Altinn.Notifications.Core.Enums.EmailContentType expectedCoreContentType)
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var emailAddress = "test@example.com";

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Test Subject",
                    Body = "Test body",
                    ContentType = externalContentType,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCoreContentType, result.InstantEmailDetails.EmailContent.ContentType);
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_GeneratesUniqueIds()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Test Subject",
                    Body = "Test body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result1 = request.MapToInstantEmailNotificationOrder(creatorShortName, created);
        var result2 = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotEqual(result1.OrderId, result2.OrderId);
        Assert.NotEqual(result1.OrderChainId, result2.OrderChainId);
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        InstantEmailNotificationOrderRequestExt? request = null;
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            request!.MapToInstantEmailNotificationOrder(creatorShortName, created));
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithNullInstantEmailDetails_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = null!
        };
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            request.MapToInstantEmailNotificationOrder(creatorShortName, created));
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithNullEmailSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = null!
            }
        };
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            request.MapToInstantEmailNotificationOrder(creatorShortName, created));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MapToInstantEmailNotificationOrder_WithInvalidCreatorShortName_ThrowsArgumentException(string? creatorShortName)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Test Subject",
                    Body = "Test body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            request.MapToInstantEmailNotificationOrder(creatorShortName!, created));
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithNullCreatorShortName_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Test Subject",
                    Body = "Test body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            request.MapToInstantEmailNotificationOrder(null!, created));
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithLongContent_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var longSubject = new string('S', 500);
        var longBody = new string('B', 5000);

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = longSubject,
                    Body = longBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(longSubject, result.InstantEmailDetails.EmailContent.Subject);
        Assert.Equal(longBody, result.InstantEmailDetails.EmailContent.Body);
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name+tag@domain.co.uk")]
    [InlineData("simple@test.org")]
    public void MapToInstantEmailNotificationOrder_WithDifferentEmailFormats_MapsCorrectly(string emailAddress)
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Test Subject",
                    Body = "Test body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(emailAddress, result.InstantEmailDetails.EmailAddress);
    }

    [Fact]
    public void MapToInstantEmailNotificationOrder_WithSpecialCharactersInContent_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var specialSubject = "Test with æøå & special chars! 🎉";
        var specialBody = "Content with æøå, special chars: <>&\"' and emojis 🚀✨";

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = specialSubject,
                    Body = specialBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = request.MapToInstantEmailNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(specialSubject, result.InstantEmailDetails.EmailContent.Subject);
        Assert.Equal(specialBody, result.InstantEmailDetails.EmailContent.Body);
    }
}
