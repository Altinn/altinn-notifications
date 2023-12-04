using Altinn.Notifications.Email.Integrations.Clients;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations
{
    public class EmailServiceClientTests
    {
        [Theory]
        [InlineData("PerSubscriptionPerHourLimitExceeded - Please try again after 3636 seconds.", 3636)]
        [InlineData("PerSubscriptionPerMinuteLimitExceeded - Please try again after 60 seconds.", 60)]
        [InlineData("Random error message not mentioning specific seconds", 60)]
        [InlineData("PerSubscriptionPerHourLimitExceeded - Please try again after 4000 seconds. Status: 429 (Too Many Requests) ErrorCode: TooManyRequests", 4000)]
        public void GetDelayFromString(string input, int expentedDelay)
        {
            // Act
            var result = EmailServiceClient.GetDelayFromString(input);

            // Assert
            Assert.Equal(expentedDelay, result);
        }
    }
}
