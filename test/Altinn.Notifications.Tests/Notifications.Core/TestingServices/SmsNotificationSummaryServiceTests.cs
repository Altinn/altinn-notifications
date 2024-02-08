using System;
using System.Security.Policy;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Shared;

using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices
{
    public class SmsNotificationSummaryServiceTests
    {
        [Theory]
        [InlineData(SmsNotificationResultType.Accepted, true)]
        [InlineData(SmsNotificationResultType.New, false)]
        [InlineData(SmsNotificationResultType.Sending, false)]
        [InlineData(SmsNotificationResultType.Failed_RecipientNotIdentified, false)]
        [InlineData(SmsNotificationResultType.Failed_InvalidRecipient, false)]
        [InlineData(SmsNotificationResultType.Failed, false)]
        public void IsSuccessResult_CheckResultForAllEnums(SmsNotificationResultType result, bool expectedIsSuccess)
        {
            bool actualIsSuccess = SmsNotificationSummaryService.IsSuccessResult(result);
            Assert.Equal(expectedIsSuccess, actualIsSuccess);
        }

        [Theory]
        [InlineData(SmsNotificationResultType.New, "The SMS has been created, but has not been picked up for processing yet.")]
        [InlineData(SmsNotificationResultType.Sending, "The SMS is being processed and will be attempted sent shortly.")]
        [InlineData(SmsNotificationResultType.Accepted, "The SMS has been accepted by the gateway service and will be sent shortly.")]
        [InlineData(SmsNotificationResultType.Failed, "The SMS was not sent due to an unspecified failure.")]
        [InlineData(SmsNotificationResultType.Failed_RecipientNotIdentified, "The SMS was not sent because the recipient's SMS address was not found.")]
        [InlineData(SmsNotificationResultType.Failed_InvalidRecipient, "The SMS not sent because the recipient number was invalid.")]
        public void GetResultDescription_ExpectedDescription(SmsNotificationResultType result, string expected)
        {
            string actual = SmsNotificationSummaryService.GetResultDescription(result);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetResultDescription_AllResultTypesHaveDescriptions()
        {
            foreach (SmsNotificationResultType resultType in Enum.GetValues(typeof(SmsNotificationResultType)))
            {
                string resultDescrption = SmsNotificationSummaryService.GetResultDescription(resultType);
                Assert.NotEmpty(resultDescrption);
            }
        }

        [Fact]
        public void ProcessNotificationResults_1_generated_1_successful()
        {
            // Arrange
            SmsNotificationSummary summary = new(Guid.NewGuid())
            {
                Notifications = new()
                {
                    new SmsNotificationWithResult(
                    Guid.NewGuid(),
                    new Altinn.Notifications.Core.Models.Recipients.SmsRecipient(),
                    new NotificationResult<SmsNotificationResultType>(SmsNotificationResultType.Accepted, DateTime.UtcNow))
                }
            };

            // Act 
            SmsNotificationSummaryService.ProcessNotificationResults(summary);

            // Assert
            Assert.Equal(1, summary.Generated);
            Assert.Equal(1, summary.Succeeded);
        }

        [Fact]
        public void ProcessNotificationResults_1_generated_0_successful()
        {
            // Arrange
            SmsNotificationSummary summary = new(Guid.NewGuid())
            {
                Notifications = new()
                {
                    new SmsNotificationWithResult(
                    Guid.NewGuid(),
                    new Altinn.Notifications.Core.Models.Recipients.SmsRecipient(),
                    new NotificationResult<SmsNotificationResultType>(SmsNotificationResultType.Failed_RecipientNotIdentified, DateTime.UtcNow))
                }
            };

            // Act 
            SmsNotificationSummaryService.ProcessNotificationResults(summary);

            // Assert
            Assert.Equal(1, summary.Generated);
            Assert.Equal(0, summary.Succeeded);
        }

        [Fact]
        public async Task GetSmsSummary_NoMatchInDBForOrder()
        {
            // Arrange
            Mock<INotificationSummaryRepository> repoMock = new();
            repoMock.Setup(r => r.GetSmsSummary(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync((SmsNotificationSummary?)null);

            var service = new SmsNotificationSummaryService(repoMock.Object);

            // Act
            var result = await service.GetSummary(Guid.NewGuid(), "ttd");

            // Assert
            result.Match(
                success => throw new Exception("No success value should be returned if db returns null"),
                actuallError =>
                {
                    Assert.IsType<ServiceError>(actuallError);
                    Assert.Equal(404, actuallError.ErrorCode);
                    return true;
                });
        }
    }
}
