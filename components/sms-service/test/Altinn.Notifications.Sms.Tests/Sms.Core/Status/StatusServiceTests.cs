using System.Text.Json;

using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;

using LinkMobility.PSWin.Receiver.Model;
using Moq;

namespace Altinn.Notifications.Sms.Tests.Sms.Core.Status;

public class StatusServiceTests
{
    [Fact]
    public async Task UpdateStatusAsync_NullMessage_ThrowsArgumentNullException()
    {
        var publisherMock = new Mock<ISmsDeliveryReportPublisher>();
        var sut = new StatusService(publisherMock.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UpdateStatusAsync(null!));
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidMessage_PublishesResult()
    {
        var publisherMock = new Mock<ISmsDeliveryReportPublisher>();
        var sut = new StatusService(publisherMock.Object);

        var message = new DrMessage("ref-123", "+4799999999", DeliveryState.DELIVRD, "20260420103000");

        await sut.UpdateStatusAsync(message);

        publisherMock.Verify(p => p.PublishAsync(It.IsAny<SendOperationResult>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidMessage_SerializesDeliveryReportWithExpectedSchema()
    {
        const string rawDeliveryTime = "20260420103000";
        var message = new DrMessage("ref-abc", "+4712345678", DeliveryState.DELIVRD, rawDeliveryTime);

        SendOperationResult? captured = null;
        var publisherMock = new Mock<ISmsDeliveryReportPublisher>();
        publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<SendOperationResult>()))
            .Callback<SendOperationResult>(r => captured = r)
            .Returns(Task.CompletedTask);

        var sut = new StatusService(publisherMock.Object);
        await sut.UpdateStatusAsync(message);

        Assert.NotNull(captured?.DeliveryReport);

        using var doc = JsonDocument.Parse(captured.DeliveryReport);
        var root = doc.RootElement;

        Assert.Equal("ref-abc", root.GetProperty("reference").GetString());
        Assert.Equal("+4712345678", root.GetProperty("receiver").GetString());
        Assert.Equal(DeliveryState.DELIVRD.ToString(), root.GetProperty("state").GetString());
        Assert.Equal(rawDeliveryTime, root.GetProperty("deliveryTime").GetString());
    }
}
