using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Telemetry;
using Altinn.Notifications.Integrations.Wolverine.Handlers;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine.Handlers;

public sealed class SmsDeliveryReportHandlerTests : IDisposable
{
    private readonly DeliveryReportMetrics _metrics;
    private readonly Mock<ISmsNotificationService> _serviceMock;

    public SmsDeliveryReportHandlerTests()
    {
        _metrics = new DeliveryReportMetrics();
        _serviceMock = new Mock<ISmsNotificationService>();
    }

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public async Task Handle_ValidDeliveryReport_EmitsOneMetricWithCorrectTags()
    {
        // Arrange
        string gatewayReference = Guid.NewGuid().ToString();
        var capturedTags = new Dictionary<string, object?>();
        int measurementCount = 0;

        using var listener = CreateListener((_, _, tags) =>
        {
            measurementCount++;
            foreach (var tag in tags)
            {
                capturedTags[tag.Key] = tag.Value;
            }
        });

        var command = BuildCommand(gatewayReference, sendResult: "Delivered");

        // Act
        await SmsDeliveryReportHandler.Handle(
            command,
            _serviceMock.Object,
            _metrics,
            NullLogger.Instance);

        // Assert — exactly one metric measurement
        Assert.Equal(1, measurementCount);

        // Assert — channel tag
        Assert.Equal("sms", capturedTags["channel"]);

        // Assert — send result forwarded
        Assert.Equal("Delivered", capturedTags["sms.send_result"]);
    }

    [Fact]
    public async Task Handle_MissingGatewayReference_ThrowsAndDoesNotEmitMetric()
    {
        // Arrange
        int measurementCount = 0;

        using var listener = CreateListener((_, _, _) => measurementCount++);

        var command = BuildCommand(gatewayReference: string.Empty, sendResult: "Delivered");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDeliveryReportException>(() =>
            SmsDeliveryReportHandler.Handle(
                command,
                _serviceMock.Object,
                _metrics,
                NullLogger.Instance));

        Assert.Equal(0, measurementCount);
    }

    [Fact]
    public async Task Handle_UnrecognizedSendResult_ThrowsAndDoesNotEmitMetric()
    {
        // Arrange
        int measurementCount = 0;

        using var listener = CreateListener((_, _, _) => measurementCount++);

        var command = BuildCommand(Guid.NewGuid().ToString(), sendResult: "NotARealStatus");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDeliveryReportException>(() =>
            SmsDeliveryReportHandler.Handle(
                command,
                _serviceMock.Object,
                _metrics,
                NullLogger.Instance));

        Assert.Equal(0, measurementCount);
    }

    [Fact]
    public async Task Handle_ValidDeliveryReport_DelegatesUpdateToService()
    {
        // Arrange
        string gatewayReference = Guid.NewGuid().ToString();
        Guid notificationId = Guid.NewGuid();

        var command = BuildCommand(gatewayReference, sendResult: "Delivered", notificationId: notificationId);

        SmsSendOperationResult? captured = null;
        _serviceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()))
            .Callback<SmsSendOperationResult>(r => captured = r)
            .Returns(Task.CompletedTask);

        // Act
        await SmsDeliveryReportHandler.Handle(
            command,
            _serviceMock.Object,
            _metrics,
            NullLogger.Instance);

        // Assert
        _serviceMock.Verify(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(gatewayReference, captured!.GatewayReference);
        Assert.Equal(notificationId, captured.NotificationId);
        Assert.Equal(SmsNotificationResultType.Delivered, captured.SendResult);
    }

    [Theory]
    [InlineData("Delivered", SmsNotificationResultType.Delivered)]
    [InlineData("Failed", SmsNotificationResultType.Failed)]
    [InlineData("Failed_InvalidRecipient", SmsNotificationResultType.Failed_InvalidRecipient)]
    [InlineData("Failed_BarredReceiver", SmsNotificationResultType.Failed_BarredReceiver)]
    [InlineData("Failed_Deleted", SmsNotificationResultType.Failed_Deleted)]
    [InlineData("Failed_Expired", SmsNotificationResultType.Failed_Expired)]
    [InlineData("Failed_Undelivered", SmsNotificationResultType.Failed_Undelivered)]
    [InlineData("Failed_RecipientNotIdentified", SmsNotificationResultType.Failed_RecipientNotIdentified)]
    [InlineData("Failed_Rejected", SmsNotificationResultType.Failed_Rejected)]
    [InlineData("Failed_TTL", SmsNotificationResultType.Failed_TTL)]
    public async Task Handle_KnownSendResults_ParseAndForwardCorrectly(string rawSendResult, SmsNotificationResultType expected)
    {
        // Arrange
        SmsSendOperationResult? captured = null;
        _serviceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()))
            .Callback<SmsSendOperationResult>(r => captured = r)
            .Returns(Task.CompletedTask);

        var command = BuildCommand(Guid.NewGuid().ToString(), sendResult: rawSendResult);

        // Act
        await SmsDeliveryReportHandler.Handle(
            command,
            _serviceMock.Object,
            _metrics,
            NullLogger.Instance);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(expected, captured!.SendResult);
    }

    private static SmsDeliveryReportCommand BuildCommand(
        string gatewayReference,
        string sendResult = "Delivered",
        Guid? notificationId = null,
        string? deliveryReport = null)
    {
        return new SmsDeliveryReportCommand
        {
            GatewayReference = gatewayReference,
            SendResult = sendResult,
            NotificationId = notificationId,
            DeliveryReport = deliveryReport
        };
    }

    private static MeterListener CreateListener(
        Action<Instrument, long, ReadOnlySpan<KeyValuePair<string, object?>>> onMeasurement)
    {
        var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DeliveryReportMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            onMeasurement(instrument, value, tags));

        listener.Start();
        return listener;
    }
}
