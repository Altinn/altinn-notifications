using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Extensions;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingExtensions;

public class NotificationResultTypeExtensionsTests
{
    private static readonly SmsNotificationResultType[] _pendingSmsResults =
    [
        SmsNotificationResultType.New,
        SmsNotificationResultType.Sending,
        SmsNotificationResultType.Accepted
    ];

    private static readonly EmailNotificationResultType[] _pendingEmailResults =
    [
        EmailNotificationResultType.New,
        EmailNotificationResultType.Sending,
        EmailNotificationResultType.Succeeded
    ];

    public static IEnumerable<object[]> PendingSmsResults() => _pendingSmsResults.Select(r => new object[] { r });

    public static IEnumerable<object[]> FinalSmsResults() => Enum.GetValues<SmsNotificationResultType>().Except(_pendingSmsResults).Select(r => new object[] { r });

    public static IEnumerable<object[]> PendingEmailResults() => _pendingEmailResults.Select(r => new object[] { r });

    public static IEnumerable<object[]> FinalEmailResults() => Enum.GetValues<EmailNotificationResultType>().Except(_pendingEmailResults).Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(PendingEmailResults))]
    public void IsFinal_PendingEmailResult_ReturnsFalse(EmailNotificationResultType result)
    {
        Assert.False(result.IsFinal());
    }

    [Theory]
    [MemberData(nameof(FinalEmailResults))]
    public void IsFinal_FinalEmailResult_ReturnsTrue(EmailNotificationResultType result)
    {
        Assert.True(result.IsFinal());
    }

    [Theory]
    [MemberData(nameof(PendingSmsResults))]
    public void IsFinal_PendingSmsResult_ReturnsFalse(SmsNotificationResultType result)
    {
        Assert.False(result.IsFinal());
    }

    [Theory]
    [MemberData(nameof(FinalSmsResults))]
    public void IsFinal_FinalSmsResult_ReturnsTrue(SmsNotificationResultType result)
    {
        Assert.True(result.IsFinal());
    }

    [Fact]
    public void IsFinal_EmailDeliveredAndExpired_AreFinal()
    {
        Assert.True(EmailNotificationResultType.Delivered.IsFinal());
        Assert.True(EmailNotificationResultType.Failed_TTL.IsFinal());
    }

    [Fact]
    public void IsFinal_SmsDeliveredAndExpired_AreFinal()
    {
        Assert.True(SmsNotificationResultType.Delivered.IsFinal());
        Assert.True(SmsNotificationResultType.Failed_TTL.IsFinal());
    }

    [Fact]
    public void IsFinal_UndefinedEmailValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((EmailNotificationResultType)999).IsFinal());
    }

    [Fact]
    public void IsFinal_UndefinedSmsValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((SmsNotificationResultType)999).IsFinal());
    }
}
