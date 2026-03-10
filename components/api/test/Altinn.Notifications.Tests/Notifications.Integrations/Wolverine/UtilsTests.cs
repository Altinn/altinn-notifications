using System;

using Altinn.Notifications.Core.Enums;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class UtilsTests
{
    [Theory]
    [InlineData(null, EmailNotificationResultType.Failed)]
    [InlineData("Bounced", EmailNotificationResultType.Failed_Bounced)]
    [InlineData("Delivered", EmailNotificationResultType.Delivered)]
    [InlineData("Failed", EmailNotificationResultType.Failed)]
    [InlineData("FilteredSpam", EmailNotificationResultType.Failed_FilteredSpam)]
    [InlineData("Quarantined", EmailNotificationResultType.Failed_Quarantined)]
    [InlineData("Suppressed", EmailNotificationResultType.Failed_SupressedRecipient)]
    public void ParseDeliveryStatus_ReturnsCorrectResult(string? deliveryStatus, EmailNotificationResultType expectedResult)
    {
        // Act
        var result = Altinn.Notifications.Integrations.Wolverine.Utils.ParseDeliveryStatus(deliveryStatus);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UnknownStatus")]
    [InlineData("bounced")]
    public void ParseDeliveryStatus_UnhandledStatus_ThrowsArgumentException(string deliveryStatus)
    {
        Assert.Throws<ArgumentException>(() => Altinn.Notifications.Integrations.Wolverine.Utils.ParseDeliveryStatus(deliveryStatus));
    }
}
