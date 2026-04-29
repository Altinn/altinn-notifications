using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

using Altinn.Notifications.Core.Telemetry;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.Telemetry;

public sealed class DeliveryReportMetricsTests : IDisposable
{
    private readonly DeliveryReportMetrics _sut;

    public DeliveryReportMetricsTests()
    {
        _sut = new DeliveryReportMetrics();
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void RecordEmailDeliveryReport_EmitsOneMeasurementOnCorrectCounter()
    {
        // Arrange
        int measurementCount = 0;
        string? capturedInstrumentName = null;
        long capturedValue = 0;

        using var listener = CreateListener((instrument, value, _) =>
        {
            measurementCount++;
            capturedInstrumentName = instrument.Name;
            capturedValue = value;
        });

        // Act
        _sut.RecordEmailDeliveryReport(
            status: "Delivered",
            statusMessage: "OK",
            recipientMailServerHostName: "mail.example.com",
            sender: "sender@example.com",
            recipient: "recipient@example.com");

        // Assert
        Assert.Equal(1, measurementCount);
        Assert.Equal(DeliveryReportMetrics.DeliveryReportStatusCounterName, capturedInstrumentName);
        Assert.Equal(1L, capturedValue);
    }

    [Fact]
    public void RecordEmailDeliveryReport_TagsContainExpectedDimensions()
    {
        // Arrange
        var capturedTags = new Dictionary<string, object?>();

        using var listener = CreateListener((_, _, tags) =>
        {
            foreach (var tag in tags)
            {
                capturedTags[tag.Key] = tag.Value;
            }
        });

        // Act
        _sut.RecordEmailDeliveryReport(
            status: "Delivered",
            statusMessage: "OK",
            recipientMailServerHostName: "mail.example.com",
            sender: "sender@example.com",
            recipient: "recipient@example.com");

        // Assert
        Assert.Equal("email", capturedTags["channel"]);
        Assert.Equal("Delivered", capturedTags["email.status"]);
        Assert.Equal("OK", capturedTags["email.status_message"]);
        Assert.Equal("mail.example.com", capturedTags["email.recipient_mail_server"]);
        Assert.Equal("se***@example.com", capturedTags["email.sender"]);
        Assert.Equal("re***@example.com", capturedTags["email.recipient"]);
    }

    [Fact]
    public void RecordEmailDeliveryReport_NullValues_TagsDefaultToEmptyString()
    {
        // Arrange
        var capturedTags = new Dictionary<string, object?>();

        using var listener = CreateListener((_, _, tags) =>
        {
            foreach (var tag in tags)
            {
                capturedTags[tag.Key] = tag.Value;
            }
        });

        // Act
        _sut.RecordEmailDeliveryReport(
            status: null,
            statusMessage: null,
            recipientMailServerHostName: null,
            sender: null,
            recipient: null);

        // Assert
        Assert.Equal(string.Empty, capturedTags["email.status"]);
        Assert.Equal(string.Empty, capturedTags["email.status_message"]);
        Assert.Equal(string.Empty, capturedTags["email.recipient_mail_server"]);
        Assert.Equal(string.Empty, capturedTags["email.sender"]);
        Assert.Equal(string.Empty, capturedTags["email.recipient"]);
    }

    [Fact]
    public void RecordSmsDeliveryReport_EmitsOneMeasurementOnCorrectCounter()
    {
        // Arrange
        int measurementCount = 0;
        string? capturedInstrumentName = null;
        long capturedValue = 0;

        using var listener = CreateListener((instrument, value, _) =>
        {
            measurementCount++;
            capturedInstrumentName = instrument.Name;
            capturedValue = value;
        });

        // Act
        _sut.RecordSmsDeliveryReport(
            sendResult: "Delivered");

        // Assert
        Assert.Equal(1, measurementCount);
        Assert.Equal(DeliveryReportMetrics.DeliveryReportStatusCounterName, capturedInstrumentName);
        Assert.Equal(1L, capturedValue);
    }

    [Fact]
    public void RecordSmsDeliveryReport_TagsContainExpectedDimensions()
    {
        // Arrange
        var capturedTags = new Dictionary<string, object?>();

        using var listener = CreateListener((_, _, tags) =>
        {
            foreach (var tag in tags)
            {
                capturedTags[tag.Key] = tag.Value;
            }
        });

        // Act
        _sut.RecordSmsDeliveryReport(
            sendResult: "Delivered");

        // Assert
        Assert.Equal("sms", capturedTags["channel"]);
        Assert.Equal("Delivered", capturedTags["sms.send_result"]);
    }

    [Theory]
    [InlineData("sender@example.com", "se***@example.com")]
    [InlineData("ab@example.com", "***@example.com")]     // local part <= 2 chars
    [InlineData("a@example.com", "***@example.com")]      // local part <= 2 chars
    [InlineData("longaddress@domain.org", "lo***@domain.org")]
    public void MaskEmailAddress_ValidEmail_MasksLocalPartCorrectly(string input, string expected)
    {
        Assert.Equal(expected, DeliveryReportMetrics.MaskEmailAddress(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskEmailAddress_NullOrWhitespace_ReturnsEmptyString(string? input)
    {
        Assert.Equal(string.Empty, DeliveryReportMetrics.MaskEmailAddress(input));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("noatsign")]
    public void MaskEmailAddress_InvalidFormat_ReturnsEmptyString(string input)
    {
        Assert.Equal(string.Empty, DeliveryReportMetrics.MaskEmailAddress(input));
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
