using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Tests.TestData;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class StatusFeedServiceTests
{
    [Fact]
    public async Task GetStatusFeed_ValidInput_ReturnsStatusFeedArray()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        statusFeedRepository.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<int>()))
            .ReturnsAsync([new StatusFeed() { SequenceNumber = 1, OrderStatus = TestDataConstants.OrderStatusFeedTestOrderCompleted }]);

        var statusFeedService = new StatusFeedService(statusFeedRepository.Object);
        int seq = 1;
        string creatorName = "ttd";

        // Act
        var result = await statusFeedService.GetStatusFeed(seq, creatorName, CancellationToken.None);

        // Assert
        result.Match(
            success =>
            {
                Assert.NotNull(success);
                Assert.Equal(1, success[0].SequenceNumber);
                return true;
            },
            error =>
            {
                Assert.Fail("Expected success but got error: " + error.ErrorMessage);
                return false;
            });
    }

    [Fact]
    public async Task GetStatusFeed_MissingCreatorName_ReturnsError()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        var statusFeedService = new StatusFeedService(statusFeedRepository.Object);
        int seq = 1;
        string creatorName = string.Empty;
        
        // Act
        var result = await statusFeedService.GetStatusFeed(seq, creatorName, CancellationToken.None);
        
        // Assert
        result.Match(
            success =>
            {
                Assert.Fail("Expected error but got success");
                return false;
            },
            error =>
            {
                Assert.Equal(400, error.ErrorCode);
                Assert.Equal("Missing creator", error.ErrorMessage);
                return true;
            });
    }
}
