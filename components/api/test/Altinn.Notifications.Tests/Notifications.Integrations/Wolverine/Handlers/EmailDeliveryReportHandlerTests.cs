using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Telemetry;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.Integrations.Wolverine.Handlers;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine.Handlers;

public sealed class EmailDeliveryReportHandlerTests : IDisposable
{
    private readonly DeliveryReportMetrics _metrics;
    private readonly Mock<IEmailNotificationService> _serviceMock;

    public EmailDeliveryReportHandlerTests()
    {
        _metrics = new DeliveryReportMetrics();
        _serviceMock = new Mock<IEmailNotificationService>();
    }

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public async Task Handle_ValidDeliveryReport_EmitsOneMetricWithCorrectTags()
    {
        // Arrange
        string messageId = Guid.NewGuid().ToString();
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

        var command = BuildCommand(messageId, status: "Delivered");

        // Act
        await EmailDeliveryReportHandler.Handle(
            command,
            _serviceMock.Object,
            _metrics,
            NullLogger.Instance);

        // Assert — exactly one metric measurement
        Assert.Equal(1, measurementCount);

        // Assert — channel tag
        Assert.Equal("email", capturedTags["channel"]);

        // Assert — status forwarded
        Assert.Equal("Delivered", capturedTags["email.status"]);

        // Assert — sender and recipient are masked
        Assert.Equal("se***@example.com", capturedTags["email.sender"]);
        Assert.Equal("re***@example.com", capturedTags["email.recipient"]);
    }

    [Fact]
    public async Task Handle_ValidDeliveryReport_ForwardsStatusDetailsToMetric()
    {
        // Arrange
        string messageId = Guid.NewGuid().ToString();
        var capturedTags = new Dictionary<string, object?>();

        using var listener = CreateListener((_, _, tags) =>
        {
            foreach (var tag in tags)
            {
                capturedTags[tag.Key] = tag.Value;
            }
        });

        var command = BuildCommand(messageId, status: "Failed", statusMessage: "550 Mailbox not found", recipientMailServerHostName: "mail.example.com");

        // Act
        await EmailDeliveryReportHandler.Handle(
            command,
            _serviceMock.Object,
            _metrics,
            NullLogger.Instance);

        // Assert
        Assert.Equal("Failed", capturedTags["email.status"]);
        Assert.Equal("550 Mailbox not found", capturedTags["email.status_message"]);
        Assert.Equal("mail.example.com", capturedTags["email.recipient_mail_server"]);
    }

    [Fact]
    public async Task Handle_MissingMessageId_ThrowsAndDoesNotEmitMetric()
    {
        // Arrange
        int measurementCount = 0;

        using var listener = CreateListener((_, _, _) => measurementCount++);

        var command = BuildCommand(messageId: string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDeliveryReportException>(() =>
            EmailDeliveryReportHandler.Handle(
                command,
                _serviceMock.Object,
                _metrics,
                NullLogger.Instance));

        Assert.Equal(0, measurementCount);
    }

    [Fact]
    public async Task Handle_UnhandledSystemEventType_ThrowsAndDoesNotEmitMetric()
    {
        // Arrange
        int measurementCount = 0;

        using var listener = CreateListener((_, _, _) => measurementCount++);

        // Build a valid EventGrid envelope but with a different event type that has no system event mapping
        var command = BuildCommandWithEventType("Microsoft.Communication.SMSDeliveryReportReceived");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDeliveryReportException>(() =>
            EmailDeliveryReportHandler.Handle(
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
        string messageId = Guid.NewGuid().ToString();
        var command = BuildCommand(messageId, status: "Delivered");

        EmailSendOperationResult? captured = null;
        _serviceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Callback<EmailSendOperationResult>(r => captured = r)
            .Returns(Task.CompletedTask);

        // Act
        await EmailDeliveryReportHandler.Handle(
            command,
            _serviceMock.Object,
            _metrics,
            NullLogger.Instance);

        // Assert
        _serviceMock.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(messageId, captured!.OperationId);
        Assert.Equal(EmailNotificationResultType.Delivered, captured.SendResult);
    }

    private static EmailDeliveryReportCommand BuildCommand(
        string messageId,
        string status = "Delivered",
        string statusMessage = "OK",
        string? recipientMailServerHostName = null,
        string sender = "sender@example.com",
        string recipient = "recipient@example.com")
    {
        var eventGridEvent = new
        {
            id = Guid.NewGuid().ToString(),
            subject = $"sender/{sender}/message/{messageId}",
            data = new
            {
                sender,
                recipient,
                messageId,
                status,
                deliveryStatusDetails = new
                {
                    statusMessage,
                    recipientMailServerHostName = recipientMailServerHostName ?? string.Empty
                },
                deliveryAttemptTimeStamp = DateTime.UtcNow.ToString("o")
            },
            eventType = "Microsoft.Communication.EmailDeliveryReportReceived",
            dataVersion = "1.0",
            metadataVersion = "1",
            eventTime = DateTime.UtcNow.ToString("o")
        };

        string body = JsonSerializer.Serialize(eventGridEvent);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body));
        return new EmailDeliveryReportCommand(message);
    }

    private static EmailDeliveryReportCommand BuildCommandWithEventType(string eventType)
    {
        var eventGridEvent = new
        {
            id = Guid.NewGuid().ToString(),
            subject = "sender/test@example.com/message/msg-001",
            data = new { },
            eventType,
            dataVersion = "1.0",
            metadataVersion = "1",
            eventTime = DateTime.UtcNow.ToString("o")
        };

        string body = JsonSerializer.Serialize(eventGridEvent);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body));
        return new EmailDeliveryReportCommand(message);
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
