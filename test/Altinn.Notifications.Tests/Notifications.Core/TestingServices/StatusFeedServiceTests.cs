using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class StatusFeedServiceTests
{
    [Fact]
    public async Task GetStatusFeed_ValidInput_ReturnsStatusFeedArray()
    {
        // Arrange
        string jsonString = @"
        {
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

        // Create the JsonElement
        JsonElement testJsonElement;
        using JsonDocument doc = JsonDocument.Parse(jsonString);
        testJsonElement = doc.RootElement.Clone();

        Mock<IStatusFeedRepository> statusFeedRepository = new();
        statusFeedRepository.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>(), System.Threading.CancellationToken.None))
            .ReturnsAsync(new List<StatusFeed> { new StatusFeed() { SequenceNumber = 1, OrderStatus = testJsonElement } });

        var statusFeedService = new StatusFeedService(statusFeedRepository.Object);
        int seq = 1;
        string creatorName = "test_creator";

        // Act
        var result = await statusFeedService.GetStatusFeed(seq, creatorName);

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
}
