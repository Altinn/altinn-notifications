using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Tools;
using Xunit;

namespace ToolsTests;

    public class UtilTests
    {
        [Fact]
        public void MapToEmailSendOperationResult_ReturnsObject_WhenValidJson()
        {
            var sendOp = new EmailSendOperationResult 
            { 
                NotificationId = Guid.NewGuid(),
                OperationId = "op123",
                SendResult = EmailNotificationResultType.Delivered
            }; 
    
            var report = new DeadDeliveryReport
            {
                FirstSeen = DateTime.UtcNow.AddMinutes(-5),
                LastAttempt = DateTime.UtcNow,
                Resolved = false,
                AttemptCount = 1,
                Channel = DeliveryReportChannel.AzureCommunicationServices,
                DeliveryReport = sendOp.Serialize()
            };

        var result = Util.MapToEmailSendOperationResult(report);

        Assert.NotNull(result);
        Assert.Equal(sendOp.OperationId, result!.OperationId);
        Assert.Equal(sendOp.SendResult, result.SendResult);
    }

    [Fact]
    public void MapToEmailSendOperationResult_ReturnsNull_WhenInvalidJson()
    {
        var report = new DeadDeliveryReport
        {
            FirstSeen = DateTime.UtcNow.AddMinutes(-5),
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1,
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "not a json"
        };

        var result = Util.MapToEmailSendOperationResult(report);

        Assert.Null(result);
    }
}

