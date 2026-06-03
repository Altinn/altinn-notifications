using Altinn.Notifications.Integrations.Wolverine;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class EnvelopeExtensionsTests
{
    [Fact]
    public void HasEnqueuedAt_WhenHeaderNotSet_ReturnsFalse()
    {
        var envelope = new Envelope();

        Assert.False(envelope.HasEnqueuedAt());
    }

    [Fact]
    public void HasEnqueuedAt_WhenHeaderIsSet_ReturnsTrue()
    {
        var envelope = new Envelope();
        envelope.SetEnqueuedAt(DateTimeOffset.UtcNow);

        Assert.True(envelope.HasEnqueuedAt());
    }

    [Fact]
    public void SetEnqueuedAt_StoresUtcTicksAsString()
    {
        var envelope = new Envelope();
        var enqueuedAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

        envelope.SetEnqueuedAt(enqueuedAt);

        Assert.True(envelope.Headers.TryGetValue(EnvelopeExtensions.EnqueuedAtHeaderKey, out var raw));
        Assert.Equal(enqueuedAt.UtcTicks.ToString(), raw);
    }

    [Fact]
    public void EnqueuedAt_WhenHeaderIsSet_ReturnsStoredTime()
    {
        var envelope = new Envelope();
        var expected = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        envelope.SetEnqueuedAt(expected);

        var result = envelope.EnqueuedAt();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void EnqueuedAt_WhenHeaderNotSet_FallsBackToSentAt()
    {
        var sentAt = new DateTimeOffset(2024, 5, 15, 8, 0, 0, TimeSpan.Zero);
        var envelope = new Envelope { SentAt = sentAt };

        var result = envelope.EnqueuedAt();

        Assert.Equal(sentAt, result);
    }

    [Fact]
    public void EnqueuedAt_WhenHeaderIsCorrupt_FallsBackToSentAt()
    {
        var sentAt = new DateTimeOffset(2024, 5, 15, 8, 0, 0, TimeSpan.Zero);
        var envelope = new Envelope { SentAt = sentAt };
        envelope.Headers[EnvelopeExtensions.EnqueuedAtHeaderKey] = "not-a-number";

        var result = envelope.EnqueuedAt();

        Assert.Equal(sentAt, result);
    }

    [Fact]
    public void SetEnqueuedAt_ConvertsNonUtcOffsetToUtcTicks()
    {
        var envelope = new Envelope();
        var enqueuedAt = new DateTimeOffset(2024, 6, 1, 14, 0, 0, TimeSpan.FromHours(2));

        envelope.SetEnqueuedAt(enqueuedAt);
        var result = envelope.EnqueuedAt();

        Assert.Equal(enqueuedAt.UtcDateTime, result.UtcDateTime);
    }
}
