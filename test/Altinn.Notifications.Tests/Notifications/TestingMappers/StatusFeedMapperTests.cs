using System;
using System.Collections.Generic;
using System.Linq;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Delivery;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers
{
    public class StatusFeedMapperTests
    {
        [Fact]
        public void MapToOrderStatusExtList_MapsCorrectly()
        {
            // Arrange
            string jsonContent1 = @"{
              ""Status"": ""Order_Completed"",
              ""Recipients"": [
                {
                  ""Type"": ""Email"",
                  ""Status"": ""Delivered"",
                  ""Destination"": ""navn.navnesen@example.com""
                },
                {
                  ""Type"": ""SMS"",
                  ""Status"": ""Delivered"",
                  ""Destination"": ""+4799999999""
                }
              ],
              ""ShipmentId"": ""f5d51690-87c8-4df8-a980-15f4554337e8"",
              ""LastUpdated"": ""2025-03-28T16:24:17.8182889+01:00"",
              ""ShipmentType"": ""Notification"",
              ""SendersReference"": ""Random-Senders-Reference-55027""
            }";

            string jsonContent2 = @"{
              ""Status"": ""Order_Completed"",
              ""Recipients"": [
                {
                  ""Type"": ""Email"",
                  ""Status"": ""Delivered"",
                  ""Destination"": ""navn.navnesen@example.com""
                },
                {
                  ""Type"": ""SMS"",
                  ""Status"": ""Delivered"",
                  ""Destination"": ""+4799999999""
                }
              ],
              ""ShipmentId"": ""8c3de834-830f-4c81-b548-0983a59f76df"",
              ""LastUpdated"": ""2025-03-31T16:24:17.8182889+01:00"",
              ""ShipmentType"": ""Notification"",
              ""SendersReference"": ""Random-Senders-Reference-55028""
            }";

            using var jsonDocument = System.Text.Json.JsonDocument.Parse(jsonContent1);
            using var jsonDocument2 = System.Text.Json.JsonDocument.Parse(jsonContent2);
            var jsonElement = jsonDocument.RootElement.Clone();
            var jsonElement2 = jsonDocument2.RootElement.Clone();

            var statusFeeds = new List<StatusFeed>
            {
                new StatusFeed { SequenceNumber = 1, OrderStatus = jsonElement },
                new StatusFeed { SequenceNumber = 2, OrderStatus = jsonElement2 }
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
            var result = statusFeeds.MapToOrderStatusExtList();
            
            // Assert
            Assert.NotNull(result);
            Assert.Equivalent(statusFeedExtList, result);
        }
    }
}
