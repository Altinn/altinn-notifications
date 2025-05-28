using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Status;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class StatusFeedMapperTests
{
    private readonly ILogger<StatusFeedMapperTests> _logger = NullLogger<StatusFeedMapperTests>.Instance;

    [Fact]
    public void MapToOrderStatusExtList_MapsCorrectly()
    {
        var mockShipmentId = Guid.NewGuid();
        var mockShipmentId2 = Guid.NewGuid();   
        var sendersReferenceMock = "ref123";

        // Arrange  
        var statusFeeds = new List<StatusFeed>
        {
            new()
            {
                SequenceNumber = 1,
                OrderStatus = new OrderStatus
                {
                    Recipients = new List<Recipient>
                    {
                        new Recipient
                        {
                            Destination = "noreply@altinn.no",
                            Status = Altinn.Notifications.Core.Enums.ProcessingLifecycle.Order_Completed
                        }
                    }.ToImmutableList(),
                    SendersReference = sendersReferenceMock,
                    ShipmentId = mockShipmentId
                }
            },
            new()
            {
                SequenceNumber = 2,
                OrderStatus = new OrderStatus
                {
                    Recipients = new List<Recipient>
                    {
                        new Recipient
                        {
                            Destination = "noreply@altinn.no",
                            Status = Altinn.Notifications.Core.Enums.ProcessingLifecycle.Email_Failed_TransientError
                        }
                    }.ToImmutableList(),
                    SendersReference = sendersReferenceMock,
                    ShipmentId = mockShipmentId2
                }
            }
        };

        var expectedListResponse = new List<StatusFeedExt>
        {
            new StatusFeedExt
            {
                SequenceNumber = 1,
                ShipmentId = mockShipmentId,
                SendersReference = sendersReferenceMock,
                Recipients = new List<RecipientExt>
                {
                    new RecipientExt
                    {
                        Destination = "noreply@altinn.no",
                        Status = ProcessingLifecycleExt.Order_Completed
                    }
                }.ToImmutableList()
            },
            new StatusFeedExt
            {
                SequenceNumber = 2,
                ShipmentId = mockShipmentId2,
                SendersReference = sendersReferenceMock,
                Recipients = new List<RecipientExt>
                {
                    new RecipientExt
                    {
                        Destination = "noreply@altinn.no",
                        Status = ProcessingLifecycleExt.Email_Failed_TransientError
                    }
                }.ToImmutableList()
            }
        };

        // Act  
        var result = statusFeeds.MapToStatusFeedExtList(_logger);

        // Assert  
        Assert.NotNull(result);
        Assert.Equivalent(expectedListResponse, result);
    }

    [Fact]
    public void MapToOrderStatusExt_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange  
        var statusFeeds = new List<StatusFeed>();

        // Act  
        var results = statusFeeds.MapToStatusFeedExtList(_logger);

        // Assert  
        Assert.NotNull(results);
        Assert.Empty(results);
    }
}
