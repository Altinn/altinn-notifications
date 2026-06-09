using System;
using System.Collections.Generic;
using System.Linq;

using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Mappers;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class NotificationOrderChainResponseMapperTests
{
    [Fact]
    public void MapToNotificationOrderChainResponseExt_WithCompleteResponse_MapsCorrectly()
    {
        // Arrange
        var mainSendersReference = "6D3A7540-9F14-485F-9D2D-89E0EB497A18";
        var orderChainId = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F");
        var mainShipmentId = Guid.Parse("8D26FF1D-3A77-4A98-983B-8E7958DA9BFE");

        var expectedReminders = new List<(string SendersReference, Guid ShipmentId)>
        {
            ("reminder2-reference-2F3A4B5C6D7E", Guid.Parse("2B3C4D5E-6F7A-8B9C-0D1E-2F3A4B5C6D7E")),
            ("reminder1-reference-1E2F3A4B5C6D", Guid.Parse("1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D"))
        };

        var response = new NotificationOrderChainResponse
        {
            OrderChainId = orderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = mainShipmentId,
                SendersReference = mainSendersReference,
                Reminders =
                [.. expectedReminders.Select(e => new NotificationOrderChainShipment
                    {
                        ShipmentId = e.ShipmentId,
                        SendersReference = e.SendersReference,
                    })
                ]
            }
        };

        // Act
        var result = response.MapToNotificationOrderChainResponseExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);

        Assert.NotNull(result.OrderChainReceipt);

        Assert.Equal(mainShipmentId, result.OrderChainReceipt.ShipmentId);
        Assert.Equal(mainSendersReference, result.OrderChainReceipt.SendersReference);

        Assert.NotNull(result.OrderChainReceipt.Reminders);
        Assert.Equal(expectedReminders.Count, result.OrderChainReceipt.Reminders.Count);

        for (int i = 0; i < expectedReminders.Count; i++)
        {
            Assert.Equal(expectedReminders[i].ShipmentId, result.OrderChainReceipt.Reminders[i].ShipmentId);
            Assert.Equal(expectedReminders[i].SendersReference, result.OrderChainReceipt.Reminders[i].SendersReference);
        }
    }

    [Fact]
    public void MapToNotificationOrderChainResponseExt_WithNullReminders_MapsCorrectly()
    {
        // Arrange
        var expectedSendersReference = "A22DE111-9CD7-400A-B2B5-892CB37E6EC7";
        var expectedShipmentId = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F");
        var expectedOrderChainId = Guid.Parse("3D8C1088-96CB-4CC7-A38B-FD09A0BBFE0F");

        var response = new NotificationOrderChainResponse
        {
            OrderChainId = expectedOrderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = expectedShipmentId,
                SendersReference = expectedSendersReference
            }
        };

        // Act
        var result = response.MapToNotificationOrderChainResponseExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedOrderChainId, result.OrderChainId);

        Assert.NotNull(result.OrderChainReceipt);
        Assert.Equal(expectedShipmentId, result.OrderChainReceipt.ShipmentId);
        Assert.Equal(expectedSendersReference, result.OrderChainReceipt.SendersReference);

        Assert.Null(result.OrderChainReceipt.Reminders);
    }

    [Fact]
    public void MapToNotificationOrderChainResponseExt_WithEmptyReminders_MapsCorrectly()
    {
        // Arrange
        var sendersReference = "4A88E1EB-D6C1-43FD-B81B-4E4A78585217";
        var shipmentId = Guid.Parse("8D26FF1D-3A77-4A98-983B-8E7958DA9BFE");
        var orderChainId = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F");

        var response = new NotificationOrderChainResponse
        {
            OrderChainId = orderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                Reminders = [],
                ShipmentId = shipmentId,
                SendersReference = sendersReference
            }
        };

        // Act
        var result = response.MapToNotificationOrderChainResponseExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);

        Assert.NotNull(result.OrderChainReceipt);
        Assert.Equal(shipmentId, result.OrderChainReceipt.ShipmentId);
        Assert.Equal(sendersReference, result.OrderChainReceipt.SendersReference);

        Assert.NotNull(result.OrderChainReceipt.Reminders);
        Assert.Empty(result.OrderChainReceipt.Reminders);
    }

    [Fact]
    public void MapToNotificationOrderChainResponseExt_WithNullSendersReference_MapsCorrectly()
    {
        // Arrange
        var shipmentId = Guid.Parse("FC6C2D3B-3608-4550-8F67-8C11800410AF");
        var orderChainId = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F");
        var reminderShipmentId = Guid.Parse("A7A3AE42-192E-412C-98BE-DD614DFC36D9");

        var response = new NotificationOrderChainResponse
        {
            OrderChainId = orderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                SendersReference = null,
                ShipmentId = shipmentId,
                Reminders =
                [
                    new NotificationOrderChainShipment
                {
                    SendersReference = null,
                    ShipmentId = reminderShipmentId
                }
                ]
            }
        };

        // Act
        var result = response.MapToNotificationOrderChainResponseExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);

        Assert.NotNull(result.OrderChainReceipt);
        Assert.Null(result.OrderChainReceipt.SendersReference);
        Assert.Equal(shipmentId, result.OrderChainReceipt.ShipmentId);

        Assert.NotNull(result.OrderChainReceipt.Reminders);
        Assert.Single(result.OrderChainReceipt.Reminders);

        var mappedReminder = result.OrderChainReceipt.Reminders[0];
        Assert.Null(mappedReminder.SendersReference);
        Assert.Equal(reminderShipmentId, mappedReminder.ShipmentId);
    }
}
