using System;
using System.Collections.Generic;
using System.Linq;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Delivery;
using Altinn.Notifications.Tests.TestData;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers
{
    public class StatusFeedMapperTests
    {
        [Fact]
        public void MapToOrderStatusExtList_MapsCorrectly()
        {
            // Arrange
            using var jsonDocument = System.Text.Json.JsonDocument.Parse(TestDataConstants.OrderStatusFeedTestOrderCompleted);
            using var jsonDocument2 = System.Text.Json.JsonDocument.Parse(TestDataConstants.OrderStatusFeedTestOrderCompleted2);
            var jsonElement = jsonDocument.RootElement.Clone();
            var jsonElement2 = jsonDocument2.RootElement.Clone();

            var statusFeeds = new List<StatusFeed>
            {
                new StatusFeed { SequenceNumber = 1, OrderStatus = TestDataConstants.OrderStatusFeedTestOrderCompleted },
                new StatusFeed { SequenceNumber = 2, OrderStatus = TestDataConstants.OrderStatusFeedTestOrderCompleted2 }
            };

            var statusFeedExtList = new List<StatusFeedExt>
            {
                new StatusFeedExt
                {
                    SequenceNumber = 1,
                    OrderStatus = jsonElement
                },
                new StatusFeedExt
                {
                    SequenceNumber = 2,
                    OrderStatus = jsonElement2
                }
            };

            // Act
            var result = statusFeeds.MapToStatusFeedExtList(NullLogger<StatusFeedMapperTests>.Instance);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equivalent(statusFeedExtList, result);
        }

        [Fact]
        public void MapToOrderStatusExt_WithInvalidJson_ReturnsEmptyJsonElement()
        {
            // Arrange
            var statusFeeds = new List<StatusFeed> { new() { SequenceNumber = 1, OrderStatus = "Invalid JSON" } };

            // Act
            var results = statusFeeds.MapToStatusFeedExtList(NullLogger<StatusFeedMapperTests>.Instance);

            // Assert
            Assert.NotNull(results);
            var singleResult = Assert.Single(results);
            Assert.Equal(1, singleResult.SequenceNumber);
            Assert.Equal("{}", singleResult.OrderStatus.ToString()); // Check if the OrderStatus is an empty JSON object
        }
    }
}
