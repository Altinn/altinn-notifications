using System;

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
        var response = new NotificationOrderChainResponse
        {
            Id = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F"),
            CreationResult = new NotificationOrderChainReceipt
            {
                SendersReference = "6D3A7540-9F14-485F-9D2D-89E0EB497A18",
                ShipmentId = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F"),
                Reminders =
                [
                    new NotificationOrderChainShipment
                    {
                        SendersReference = "reminder1-reference-DE2463C4",
                        ShipmentId = Guid.Parse("1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D"),
                    },
                    new NotificationOrderChainShipment
                    {
                        SendersReference = "reminder2-reference-E1EA23B8806A",
                        ShipmentId = Guid.Parse("2B3C4D5E-6F7A-8B9C-0D1E-2F3A4B5C6D7E"),
                    }
                ]
            }
        };

        // Act
        var result = response.MapToNotificationOrderChainResponseExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(response.Id, result.Id);
        Assert.NotNull(result.CreationResult);

        // Verify main notification properties
        Assert.Equal(response.Id, result.CreationResult.ShipmentId);
        Assert.Equal("6D3A7540-9F14-485F-9D2D-89E0EB497A18", result.CreationResult.SendersReference);

        // Verify reminders
        Assert.NotNull(result.CreationResult.Reminders);
        Assert.Equal(2, result.CreationResult.Reminders.Count);

        // Verify first reminder
        Assert.Equal("reminder1-reference-DE2463C4", result.CreationResult.Reminders[0].SendersReference);
        Assert.Equal(Guid.Parse("1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D"), result.CreationResult.Reminders[0].ShipmentId);

        // Verify second reminder
        Assert.Equal("reminder2-reference-E1EA23B8806A", result.CreationResult.Reminders[1].SendersReference);
        Assert.Equal(Guid.Parse("2B3C4D5E-6F7A-8B9C-0D1E-2F3A4B5C6D7E"), result.CreationResult.Reminders[1].ShipmentId);
    }

    [Fact]
    public void MapToNotificationOrderChainResponseExt_WithNullReminders_MapsCorrectly()
    {
        // Arrange
        var response = new NotificationOrderChainResponse
        {
            Id = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F"),
            CreationResult = new NotificationOrderChainReceipt
            {
                SendersReference = "A22DE111-9CD7-400A-B2B5-892CB37E6EC7",
                ShipmentId = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F")
            }
        };

        // Act
        var result = response.MapToNotificationOrderChainResponseExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(response.Id, result.Id);
        Assert.NotNull(result.CreationResult);

        Assert.Equal(response.Id, result.CreationResult.ShipmentId);
        Assert.Equal("A22DE111-9CD7-400A-B2B5-892CB37E6EC7", result.CreationResult.SendersReference);

        Assert.Null(result.CreationResult.Reminders);
    }

    [Fact]
    public void MapToNotificationOrderChainResponseExt_WithEmptyReminders_MapsCorrectly()
    {
        // Arrange
        var response = new NotificationOrderChainResponse
        {
            Id = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F"),
            CreationResult = new NotificationOrderChainReceipt
            {
                Reminders = [],
                SendersReference = "4A88E1EB-D6C1-43FD-B81B-4E4A78585217",
                ShipmentId = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F")
            }
        };

        // Act
        var result = response.MapToNotificationOrderChainResponseExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(response.Id, result.Id);
        Assert.NotNull(result.CreationResult);

        Assert.Equal(response.Id, result.CreationResult.ShipmentId);
        Assert.Equal("4A88E1EB-D6C1-43FD-B81B-4E4A78585217", result.CreationResult.SendersReference);

        Assert.NotNull(result.CreationResult.Reminders);
        Assert.Empty(result.CreationResult.Reminders);
    }

    [Fact]
    public void MapToNotificationOrderChainResponseExt_WithNullSendersReference_MapsCorrectly()
    {
        // Arrange
        var response = new NotificationOrderChainResponse
        {
            Id = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F"),
            CreationResult = new NotificationOrderChainReceipt
            {
                SendersReference = null,
                ShipmentId = Guid.Parse("9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F"),
                Reminders =
                [
                    new NotificationOrderChainShipment
                    {
                        SendersReference = null,
                        ShipmentId = Guid.Parse("1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D")
                    }
                ]
            }
        };

        // Act
        var result = response.MapToNotificationOrderChainResponseExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(response.Id, result.Id);
        Assert.NotNull(result.CreationResult);

        // Verify main notification properties
        Assert.Equal(response.Id, result.CreationResult.ShipmentId);
        Assert.Null(result.CreationResult.SendersReference);

        // Verify reminders
        Assert.NotNull(result.CreationResult.Reminders);
        Assert.Single(result.CreationResult.Reminders);

        // Verify reminder with null sender's reference
        Assert.Null(result.CreationResult.Reminders[0].SendersReference);
        Assert.Equal(Guid.Parse("1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D"), result.CreationResult.Reminders[0].ShipmentId);
    }
}
