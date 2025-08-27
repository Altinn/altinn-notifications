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
    private readonly IOptions<StatusFeedConfig> _options = Options.Create(new StatusFeedConfig
    {
        MaxPageSize = 500
    });

    private const string _creatorName = "test-creator";

    [Fact]
    public async Task GetStatusFeed_ValidInput_ReturnsStatusFeedArray()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        statusFeedRepository.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), CancellationToken.None))
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

        var sut = new StatusFeedService(statusFeedRepository.Object, _options, new NullLogger<StatusFeedService>());
        int seq = 1;

        // Act
        var result = await sut.GetStatusFeed(seq, null, _creatorName, CancellationToken.None);

        // Assert
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
    public async Task GetStatusFeed_InvalidSequenceNumber_ReturnsServiceErrorResult()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        var sut = new StatusFeedService(statusFeedRepository.Object, _options, new NullLogger<StatusFeedService>());
        int seq = -1; // Invalid sequence number
        
        // Act
        var result = await sut.GetStatusFeed(seq, null, _creatorName, CancellationToken.None);
        
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
                Assert.Equal("Sequence number cannot be less than 0", error.ErrorMessage);
                return true;
            });
    }

    [Fact]
    public async Task GetStatusFeed_MissingCreatorName_ReturnsError()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        var sut = new StatusFeedService(statusFeedRepository.Object, _options, new NullLogger<StatusFeedService>());
        int seq = 1;
        string creatorName = string.Empty;

        // Act
        var result = await sut.GetStatusFeed(seq, null, creatorName, CancellationToken.None);

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
                Assert.Equal("Creator name cannot be null or empty", error.ErrorMessage);
                return true;
            });
    }

    [Fact]
    public async Task GetStatusFeed_RepositoryThrowsException_ReturnsServiceError()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        statusFeedRepository.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), CancellationToken.None))
            .ThrowsAsync(new Exception("Database error"));
        var sut = new StatusFeedService(statusFeedRepository.Object, _options, new NullLogger<StatusFeedService>());
        int seq = 1;
        string creatorName = "ttd";
        
        // Act
        var result = await sut.GetStatusFeed(seq, null, creatorName, CancellationToken.None);
        
        // Assert
        result.Match(
            success =>
            {
                Assert.Fail("Expected error but got success");
                return false;
            },
            error =>
            {
                Assert.Equal(500, error.ErrorCode);
                Assert.Equal("Failed to retrieve status feed: Database error", error.ErrorMessage);
                return true;
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(600)] // Larger than max of 500
    public async Task GetStatusFeed_OutsideLegalValue_ClipsToMaxPageSizeValue(int? pageSize)
    {
        // Arrange
        const int seq = 0;
        
        var mockRepository = new Mock<IStatusFeedRepository>();
        var expectedStatusFeedEntries = new List<StatusFeed>();
        
        // Setup repository to capture the actual pageSize used
        mockRepository.Setup(x => x.GetStatusFeed(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatusFeedEntries);
        
        var sut = new StatusFeedService(mockRepository.Object, _options, NullLogger<StatusFeedService>.Instance);
        
        // Act
        var result = await sut.GetStatusFeed(seq, pageSize, _creatorName, CancellationToken.None);
        
        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify that the repository was called with the clipped page size (maxPageSize)
        mockRepository.Verify(
            x => x.GetStatusFeed(
            seq,
            _creatorName,
            _options.Value.MaxPageSize,
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetStatusFeed_PageSizeSmallerThanMaxPageSizeValue_UsesRequestedPageSize()
    {
        // Arrange
        const int requestedPageSize = 100; // Smaller than max of 500
        const int seq = 0;
        
        var mockRepository = new Mock<IStatusFeedRepository>();
        var expectedStatusFeedEntries = new List<StatusFeed>();
        
        // Setup repository to capture the actual pageSize used
        mockRepository.Setup(x => x.GetStatusFeed(
                It.IsAny<int>(),
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
