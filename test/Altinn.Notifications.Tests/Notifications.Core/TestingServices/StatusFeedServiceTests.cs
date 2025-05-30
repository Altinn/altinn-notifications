using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;
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

        var statusFeedService = new StatusFeedService(statusFeedRepository.Object, new NullLogger<StatusFeedService>());
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
    public async Task GetStatusFeed_InvalidSequenceNumber_ReturnsServiceErrorResult()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        var sut = new StatusFeedService(statusFeedRepository.Object, new NullLogger<StatusFeedService>());
        int seq = -1; // Invalid sequence number
        string creatorName = "ttd";
        
        // Act
        var result = await sut.GetStatusFeed(seq, creatorName, CancellationToken.None);
        
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
        var statusFeedService = new StatusFeedService(statusFeedRepository.Object, new NullLogger<StatusFeedService>());
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
                Assert.Equal("Creator name cannot be null or empty", error.ErrorMessage);
                return true;
            });
    }

    [Fact]
    public async Task GetStatusFeed_RepositoryThrowsException_ReturnsServiceError()
    {
        // Arrange
        Mock<IStatusFeedRepository> statusFeedRepository = new();
        statusFeedRepository.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<int>()))
            .ThrowsAsync(new Exception("Database error"));
        var statusFeedService = new StatusFeedService(statusFeedRepository.Object, new NullLogger<StatusFeedService>());
        int seq = 1;
        string creatorName = "ttd";
        
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
                Assert.Equal(500, error.ErrorCode);
                Assert.Equal("Failed to retrieve status feed: Database error", error.ErrorMessage);
                return true;
            });
    }
}
