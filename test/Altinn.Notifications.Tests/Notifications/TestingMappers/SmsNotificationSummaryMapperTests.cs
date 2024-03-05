using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Mappers;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers
{
    public class SmsNotificationSummaryMapperTests
    {
        [Fact]
        public void MapToSmsNotificationWithResultExt_EmptyList_AreEquivalent()
        {
            // Arrange
            List<SmsNotificationWithResult> input = new();

            // Act
            var actual = input.MapToSmsNotificationWithResultExt();

            // Assert
            Assert.Empty(actual);
        }

        [Fact]
        public void MapToSmsNotificationWithResultExt_NotificationWithFailedResult_AreEquivalent()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            DateTime timestamp = DateTime.UtcNow;
            SmsNotificationWithResultExt expected = new()
            {
                Id = id,
                Succeeded = false,
                Recipient = new()
                {
                    MobileNumber = "+4799999999"
                },
                SendStatus = new()
                {
                    LastUpdate = timestamp,
                    Status = "Failed_RecipientNotIdentified",
                    StatusDescription = "Failed to send. Could not identify recipient."
                }
            };

            SmsNotificationWithResult input = new(
                id,
                false,
                new SmsRecipient()
                {
                    OrganisationNumber = "12345678910",
                    MobileNumber = "+4799999999"
                },
                new NotificationResult<SmsNotificationResultType>(
                SmsNotificationResultType.Failed_RecipientNotIdentified,
                timestamp));

            input.ResultStatus.SetResultDescription("Failed to send. Could not identify recipient.");

            // Act
            var actual = input.MapToSmsNotificationWithResultExt();

            // Assert
            Assert.Equivalent(expected, actual, false);
        }

        [Fact]
        public void MapToSmsNotificationWithResultExt_NotificationWithSuccessResult_AreEquivalent()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            DateTime timestamp = DateTime.UtcNow;
            SmsNotificationWithResultExt expected = new()
            {
                Id = id,
                Succeeded = true,
                Recipient = new()
                {
                    MobileNumber = "+4799999999"
                },
                SendStatus = new()
                {
                    LastUpdate = timestamp,
                    Status = "Accepted",
                    StatusDescription = "This is the description"
                }
            };

            SmsNotificationWithResult input = new(
                id,
                true,
                new SmsRecipient()
                {
                    NationalIdentityNumber = "16069412345",
                    MobileNumber = "+4799999999"
                },
                new NotificationResult<SmsNotificationResultType>(
                SmsNotificationResultType.Accepted,
                timestamp));

            input.ResultStatus.SetResultDescription("This is the description");

            // Act
            var actual = input.MapToSmsNotificationWithResultExt();

            // Assert
            Assert.Equivalent(expected, actual, false);
        }
    }
}
