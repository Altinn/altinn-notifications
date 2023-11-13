using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices
{
    public class NotificationSummaryServicTests
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
            bool actualIsSuccess = NotificationSummaryService.IsSuccessResult(result);
            Assert.Equal(expectedIsSuccess, actualIsSuccess);
        }

        [Theory]
        [InlineData(EmailNotificationResultType.New, "The email has been created, but has not been picked up for processing yet.")]
        [InlineData(EmailNotificationResultType.Sending, "The email is being processed and will be attempted sent shortly.")]
        [InlineData(EmailNotificationResultType.Succeeded, "The email has been accepted by the third party email service and will be sent shortly.")]
        [InlineData(EmailNotificationResultType.Delivered, "The email was delivered to the recipient. No errors reported, making it likely it was received by the recipient.")]
        [InlineData(EmailNotificationResultType.Failed_RecipientNotIdentified, "Email was not sent because the recipient's email address was not found.")]
        [InlineData(EmailNotificationResultType.Failed_RecipientNotIdentified, "The email was not sent because the recipient's email address was not found.")],
        [InlineData(EmailNotificationResultType.Failed_InvalidEmailFormat, "The email was not sent because the recipient’s email address is in an invalid format.")]
        public void GetResultDescription_ExpectedDescription(EmailNotificationResultType result, string expected)
        {
            string actual = NotificationSummaryService.GetResultDescription(result);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetResultDescription_AllResultTypesHaveDescriptions()
        {
            foreach (EmailNotificationResultType resultType in Enum.GetValues(typeof(EmailNotificationResultType)))
            {
                string resultDescrption = NotificationSummaryService.GetResultDescription(resultType);
                Assert.NotEmpty(resultDescrption);
            }
        }
    }
}
