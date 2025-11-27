using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class StatusFeedServiceTests
{
    private const int _maxPageSize = 500;
    private readonly IOptions<NotificationConfig> _options = Options.Create(new NotificationConfig
    {
        StatusFeedMaxPageSize = _maxPageSize
    });

    private const string _creatorName = "test-creator";

    [Fact]
    public async Task GetStatusFeed_ValidInput_ReturnsStatusFeedArray()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        statusFeedRepository.Setup(x => x.GetStatusFeed(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StatusFeed()
            {
                SequenceNumber = 1,
                OrderStatus = new OrderStatus
                {
                    LastUpdated = DateTime.UtcNow,
                    SendersReference = "ref123",
                    ShipmentId = Guid.NewGuid(),
                    Recipients = new List<Recipient>
                    {
                        new() 
                        {
                            Destination = "noreply@altinn.no",
                            Status = ProcessingLifecycle.Order_Completed,
                            LastUpdate = DateTime.UtcNow
                        }
                    }.ToImmutableList(),
                }
            }
                ]);

        var sut = new StatusFeedService(statusFeedRepository.Object, _options, NullLogger<StatusFeedService>.Instance);
        long seq = 1;

        // Act
        var result = await sut.GetStatusFeed(seq, null, _creatorName, CancellationToken.None);
        
        // Assert
        statusFeedRepository.Verify(
        x => x.GetStatusFeed(seq, _creatorName, _maxPageSize, It.IsAny<CancellationToken>()),
        Times.Once);

        result.Match(
            success =>
            {
                Assert.NotNull(success);
                var entry = Assert.Single(success);
                Assert.Equal(1, entry.SequenceNumber);
                return true;
            },
            error =>
            {
                Assert.Fail("Expected success but got error: " + error.ErrorMessage);
                return false;
            });
    }

    [Fact]
    public async Task GetStatusFeed_RepositoryThrowsException_ReturnsServiceError()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        statusFeedRepository.Setup(x => x.GetStatusFeed(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));
        var sut = new StatusFeedService(statusFeedRepository.Object, _options, NullLogger<StatusFeedService>.Instance);
        long seq = 1;
        string creatorName = "ttd";
        
        // Act
        var result = await sut.GetStatusFeed(seq, null, creatorName, CancellationToken.None);
        
        // Assert
        statusFeedRepository.Verify(
            x => x.GetStatusFeed(seq, creatorName, _maxPageSize, It.IsAny<CancellationToken>()),
            Times.Once);

        result.Match(
            success =>
            {
                Assert.Fail("Expected error but got success");
                return false;
            },
            error =>
            {
                Assert.Equal(500, error.ErrorCode);
                Assert.StartsWith("Failed to retrieve status feed", error.ErrorMessage);
                return true;
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(600)]
    [InlineData(0)]
    public async Task GetStatusFeed_OutsideLegalValue_ClipsToMaxPageSizeOrMinimumValue(int? pageSize)
    {
        // Arrange
        const long seq = 0;

        var mockRepository = new Mock<IStatusFeedRepository>();
        var expectedStatusFeedEntries = new List<StatusFeed>();

        // Setup repository to capture the actual pageSize used
        mockRepository.Setup(x => x.GetStatusFeed(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatusFeedEntries);

        var sut = new StatusFeedService(mockRepository.Object, _options, NullLogger<StatusFeedService>.Instance);

        int expected = CalculateExpectedPageSize(pageSize);

        // Act
        var result = await sut.GetStatusFeed(seq, pageSize, _creatorName, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        // Verify that the repository was called with the clipped page size (maxPageSize)
        mockRepository.Verify(
            x => x.GetStatusFeed(
            seq,
            _creatorName,
            expected,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private int CalculateExpectedPageSize(int? pageSize)
    {
        // Compute expected from configured bounds
        int max = _options.Value.StatusFeedMaxPageSize;
        int value = pageSize ?? max;
        if (value < 1)
        {
            return 1;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    [Fact]
    public async Task GetStatusFeed_PageSizeSmallerThanMaxPageSizeValue_UsesRequestedPageSize()
    {
        // Arrange
        const int requestedPageSize = 100; // Smaller than max of 500
        const long seq = 0;
        
        var mockRepository = new Mock<IStatusFeedRepository>();
        var expectedStatusFeedEntries = new List<StatusFeed>();
        
        // Setup repository to capture the actual pageSize used
        mockRepository.Setup(x => x.GetStatusFeed(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatusFeedEntries);
        
        var sut = new StatusFeedService(mockRepository.Object, _options, NullLogger<StatusFeedService>.Instance);
        
        // Act
        var result = await sut.GetStatusFeed(seq, requestedPageSize, _creatorName, CancellationToken.None);
        
        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify that the repository was called with the requested page size (not the max)
        mockRepository.Verify(
            x => x.GetStatusFeed(
            seq,
            _creatorName,
            requestedPageSize, // Should use the requested value of 100, not the max of 500
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
