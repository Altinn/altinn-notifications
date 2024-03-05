using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Mappers;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers
{
    public class EmailNotificationSummaryMapperTests
    {
        [Fact]
        public void MapToEmailNotificationWithResultExt_EmptyList_AreEquivalent()
        {
            // Arrange
            List<EmailNotificationWithResult> input = new();

            // Act
            var actual = input.MapToEmailNotificationWithResultExt();

            // Assert
            Assert.Empty(actual);
        }

        [Fact]
        public void MapToEmailNotificationWithResultExt_NotificationWithFailedResult_AreEquivalent()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            DateTime timestamp = DateTime.UtcNow;
            EmailNotificationWithResultExt expected = new()
            {
                Id = id,
                Succeeded = false,
                Recipient = new()
                {
                    EmailAddress = "recipient@domain.com"
                },
                SendStatus = new()
                {
                    LastUpdate = timestamp,
                    Status = "Failed_RecipientNotIdentified",
                    StatusDescription = "Failed to send. Could not identify recipient."
                }
            };

            EmailNotificationWithResult input = new(
                id,
                false,
                new EmailRecipient()
                {
                    NationalIdentityNumber = "16069412345",
                    ToAddress = "recipient@domain.com"
                },
                new NotificationResult<EmailNotificationResultType>(
                EmailNotificationResultType.Failed_RecipientNotIdentified,
                timestamp));

            input.ResultStatus.SetResultDescription("Failed to send. Could not identify recipient.");

            // Act
            var actual = input.MapToEmailNotificationWithResultExt();

            // Assert
            Assert.Equivalent(expected, actual, false);
        }

        [Fact]
        public void MapToEmailNotificationWithResultExt_NotificationWithSuccessResult_AreEquivalent()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            DateTime timestamp = DateTime.UtcNow;
            EmailNotificationWithResultExt expected = new()
            {
                Id = id,
                Succeeded = true,
                Recipient = new()
                {
                    EmailAddress = "recipient@domain.com"
                },
                SendStatus = new()
                {
                    LastUpdate = timestamp,
                    Status = "Delivered",
                    StatusDescription = "The email was delivered to the recipient. No errors reported, making it likely it was received by the recipient."
                }
            };

            EmailNotificationWithResult input = new(
                id,
                true,
                new EmailRecipient()
                {
                    OrganisationNumber = "12345678910",
                    ToAddress = "recipient@domain.com"
                },
                new NotificationResult<EmailNotificationResultType>(
                EmailNotificationResultType.Delivered,
                timestamp));

            input.ResultStatus.SetResultDescription("The email was delivered to the recipient. No errors reported, making it likely it was received by the recipient.");

            // Act
            var actual = input.MapToEmailNotificationWithResultExt();

            // Assert
            Assert.Equivalent(expected, actual, false);
        }
    }
}
