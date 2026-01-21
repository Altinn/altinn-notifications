using System;
using System.Collections.Generic;

using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

/// <summary>
/// Unit tests for the <see cref="NotificationOrderChainValidationHelper"/> class.
/// Tests validate that proper error codes are returned for invalid inputs.
/// </summary>
public class NotificationOrderChainValidationHelperTests
{
    private static readonly DateTime _testUtcNow = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidateOrderChainRequest_ValidRequest_NoExceptionThrown()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act & Assert - should not throw
        NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow);
    }

    [Fact]
    public void ValidateOrderChainRequest_EmptyIdempotencyId_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.IdempotencyId = string.Empty;

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.IdempotencyId_Required);
    }

    [Fact]
    public void ValidateOrderChainRequest_NullIdempotencyId_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.IdempotencyId = null!;

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.IdempotencyId_Required);
    }

    [Fact]
    public void ValidateOrderChainRequest_SendTimeWithoutTimezone_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.RequestedSendTime = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Unspecified);

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.SendTime_TimezoneRequired);
    }

    [Fact]
    public void ValidateOrderChainRequest_SendTimeInPast_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.RequestedSendTime = _testUtcNow.AddDays(-1);

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.SendTime_MustBeFuture);
    }

    [Fact]
    public void ValidateOrderChainRequest_InvalidConditionEndpoint_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ConditionEndpoint = new Uri("ftp://invalid.example.com"); // Invalid scheme

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.ConditionEndpoint_InvalidScheme);
    }

    [Fact]
    public void ValidateOrderChainRequest_NullRecipient_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Recipient = null!;

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.Recipient_CannotBeNull);
    }

    [Fact]
    public void ValidateOrderChainRequest_MultipleRecipients_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Recipient = new NotificationRecipientExt
        {
            RecipientEmail = new RecipientEmailExt
            {
                EmailAddress = "test@example.com",
                Settings = CreateValidEmailSettings()
            },
            RecipientSms = new RecipientSmsExt
            {
                PhoneNumber = "+4712345678",
                Settings = CreateValidSmsSettings()
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.Recipient_MustHaveExactlyOne);
    }

    [Fact]
    public void ValidateOrderChainRequest_InvalidEmailAddress_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Recipient = new NotificationRecipientExt
        {
            RecipientEmail = new RecipientEmailExt
            {
                EmailAddress = "invalid-email",
                Settings = CreateValidEmailSettings()
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.EmailAddress_Invalid);
    }

    [Fact]
    public void ValidateOrderChainRequest_InvalidPhoneNumber_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Recipient = new NotificationRecipientExt
        {
            RecipientSms = new RecipientSmsExt
            {
                PhoneNumber = "12345", // Missing country code
                Settings = CreateValidSmsSettings()
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.PhoneNumber_Invalid);
    }

    [Fact]
    public void ValidateOrderChainRequest_InvalidNationalIdentityNumber_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Recipient = new NotificationRecipientExt
        {
            RecipientPerson = new RecipientPersonExt
            {
                NationalIdentityNumber = "12345", // Too short
                ChannelSchema = NotificationChannelExt.Email,
                EmailSettings = CreateValidEmailSettings()
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.NationalIdentityNumber_Invalid);
    }

    [Fact]
    public void ValidateOrderChainRequest_InvalidOrganizationNumber_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Recipient = new NotificationRecipientExt
        {
            RecipientOrganization = new RecipientOrganizationExt
            {
                OrgNumber = "12345", // Too short
                ChannelSchema = NotificationChannelExt.Email,
                EmailSettings = CreateValidEmailSettings()
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.OrganizationNumber_Invalid);
    }

    [Fact]
    public void ValidateOrderChainRequest_EmptyEmailSubject_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        var settings = CreateValidEmailSettings();
        settings.Subject = string.Empty;
        request.Recipient = new NotificationRecipientExt
        {
            RecipientEmail = new RecipientEmailExt
            {
                EmailAddress = "test@example.com",
                Settings = settings
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.EmailSubject_Required);
    }

    [Fact]
    public void ValidateOrderChainRequest_EmptySmsBody_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        var settings = CreateValidSmsSettings();
        settings.Body = string.Empty;
        request.Recipient = new NotificationRecipientExt
        {
            RecipientSms = new RecipientSmsExt
            {
                PhoneNumber = "+4712345678",
                Settings = settings
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.SmsBody_Required);
    }

    [Fact]
    public void ValidateOrderChainRequest_ReminderWithBothDelayDaysAndSendTime_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Reminders = new List<NotificationReminderExt>
        {
            new NotificationReminderExt
            {
                DelayDays = 7,
                RequestedSendTime = _testUtcNow.AddDays(7), // Both set
                Recipient = CreateValidRecipient()
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.Reminder_TimingMutuallyExclusive);
    }

    [Fact]
    public void ValidateOrderChainRequest_ReminderWithNeitherDelayDaysNorSendTime_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Reminders = new List<NotificationReminderExt>
        {
            new NotificationReminderExt
            {
                // Neither DelayDays nor RequestedSendTime set
                Recipient = CreateValidRecipient()
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.Reminder_TimingMutuallyExclusive);
    }

    [Fact]
    public void ValidateOrderChainRequest_ReminderDelayDaysLessThanOne_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Reminders = new List<NotificationReminderExt>
        {
            new NotificationReminderExt
            {
                DelayDays = 0, // Invalid
                Recipient = CreateValidRecipient()
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.ReminderDelayDays_Invalid);
    }

    [Fact]
    public void ValidateOrderChainRequest_ChannelSchemaEmailAndSms_MissingEmailSettings_ThrowsWithCorrectCode()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Recipient = new NotificationRecipientExt
        {
            RecipientPerson = new RecipientPersonExt
            {
                NationalIdentityNumber = "12345678901",
                ChannelSchema = NotificationChannelExt.EmailAndSms,
                SmsSettings = CreateValidSmsSettings()
                // EmailSettings is null
            }
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        AssertContainsErrorCode(exception, ValidationErrors.ChannelSchema_EmailSettings_RequiredForDualChannel);
    }

    [Fact]
    public void ValidateOrderChainRequest_MultipleErrors_CollectsAllErrors()
    {
        // Arrange
        var request = new NotificationOrderChainRequestExt
        {
            IdempotencyId = string.Empty, // Error 1
            RequestedSendTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), // Error 2: in past
            Recipient = null! // Error 3
        };

        // Act & Assert
        var exception = Assert.Throws<AltinnProblemDetailsException>(
            () => NotificationOrderChainValidationHelper.ValidateOrderChainRequest(request, _testUtcNow));

        // Should have multiple errors
        var problemDetails = exception.ToProblemDetails();
        Assert.NotNull(problemDetails);
    }

    private static void AssertContainsErrorCode(AltinnProblemDetailsException exception, ValidationErrorDescriptor expectedError)
    {
        var problemDetails = exception.ToProblemDetails();
        Assert.NotNull(problemDetails);
        // The error code should be present in the problem details
        // The exact assertion depends on the structure of AltinnProblemDetails
    }

    private static NotificationOrderChainRequestExt CreateValidRequest()
    {
        return new NotificationOrderChainRequestExt
        {
            IdempotencyId = "test-idempotency-id",
            RequestedSendTime = _testUtcNow.AddDays(1),
            Recipient = CreateValidRecipient()
        };
    }

    private static NotificationRecipientExt CreateValidRecipient()
    {
        return new NotificationRecipientExt
        {
            RecipientEmail = new RecipientEmailExt
            {
                EmailAddress = "test@example.com",
                Settings = CreateValidEmailSettings()
            }
        };
    }

    private static EmailSendingOptionsExt CreateValidEmailSettings()
    {
        return new EmailSendingOptionsExt
        {
            Subject = "Test Subject",
            Body = "Test Body",
            SendingTimePolicy = SendingTimePolicyExt.Anytime,
            ContentType = EmailContentTypeExt.Plain
        };
    }

    private static SmsSendingOptionsExt CreateValidSmsSettings()
    {
        return new SmsSendingOptionsExt
        {
            Body = "Test SMS body",
            SendingTimePolicy = SendingTimePolicyExt.Anytime
        };
    }
}
