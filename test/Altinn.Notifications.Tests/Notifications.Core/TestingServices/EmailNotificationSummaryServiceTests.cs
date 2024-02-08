using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices
{
    public class EmailNotificationSummaryServiceTests
    {
        [Theory]
        [InlineData(EmailNotificationResultType.New, false)]
        [InlineData(EmailNotificationResultType.Sending, false)]
        [InlineData(EmailNotificationResultType.Succeeded, true)]
        [InlineData(EmailNotificationResultType.Delivered, true)]
        [InlineData(EmailNotificationResultType.Failed_RecipientNotIdentified, false)]
        [InlineData(EmailNotificationResultType.Failed_InvalidEmailFormat, false)]
        public void IsSuccessResult_CheckResultForAllEnums(EmailNotificationResultType result, bool expectedIsSuccess)
        {
            bool actualIsSuccess = EmailNotificationSummaryService.IsSuccessResult(result);
            Assert.Equal(expectedIsSuccess, actualIsSuccess);
        }

        [Theory]
        [InlineData(EmailNotificationResultType.New, "The email has been created, but has not been picked up for processing yet.")]
        [InlineData(EmailNotificationResultType.Sending, "The email is being processed and will be attempted sent shortly.")]
        [InlineData(EmailNotificationResultType.Succeeded, "The email has been accepted by the third party email service and will be sent shortly.")]
        [InlineData(EmailNotificationResultType.Delivered, "The email was delivered to the recipient. No errors reported, making it likely it was received by the recipient.")]
        [InlineData(EmailNotificationResultType.Failed, "The email was not sent due to an unspecified failure.")]
        [InlineData(EmailNotificationResultType.Failed_RecipientNotIdentified, "The email was not sent because the recipient's email address was not found.")]
        [InlineData(EmailNotificationResultType.Failed_InvalidEmailFormat, "The email was not sent because the recipient’s email address is in an invalid format.")]
        public void GetResultDescription_ExpectedDescription(EmailNotificationResultType result, string expected)
        {
            string actual = EmailNotificationSummaryService.GetResultDescription(result);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetResultDescription_AllResultTypesHaveDescriptions()
        {
            foreach (EmailNotificationResultType resultType in Enum.GetValues(typeof(EmailNotificationResultType)))
            {
                string resultDescrption = EmailNotificationSummaryService.GetResultDescription(resultType);
                Assert.NotEmpty(resultDescrption);
            }
        }

        [Fact]
        public void ProcessNotificationResults_1_generated_1_successful()
        {
            // Arrange
            EmailNotificationSummary summary = new(Guid.NewGuid())
            {
                Notifications = new()
                {
                    new EmailNotificationWithResult(
                    Guid.NewGuid(),
                    new Altinn.Notifications.Core.Models.Recipients.EmailRecipient(),
                    new NotificationResult<EmailNotificationResultType>(EmailNotificationResultType.Succeeded, DateTime.UtcNow))
                }
            };

            // Act 
            EmailNotificationSummaryService.ProcessNotificationResults(summary);
            
            // Assert
            Assert.Equal(1, summary.Generated);
            Assert.Equal(1, summary.Succeeded);
        }

        [Fact]
        public void ProcessNotificationResults_1_generated_0_successful()
        {
            // Arrange
            EmailNotificationSummary summary = new(Guid.NewGuid())
            {
                Notifications = new()
                {
                    new EmailNotificationWithResult(
                    Guid.NewGuid(),
                    new Altinn.Notifications.Core.Models.Recipients.EmailRecipient(),
                    new NotificationResult<EmailNotificationResultType>(EmailNotificationResultType.Failed_RecipientNotIdentified, DateTime.UtcNow))
                }
            };

            // Act 
            EmailNotificationSummaryService.ProcessNotificationResults(summary);

            // Assert
            Assert.Equal(1, summary.Generated);
            Assert.Equal(0, summary.Succeeded);
        }
    }
}
