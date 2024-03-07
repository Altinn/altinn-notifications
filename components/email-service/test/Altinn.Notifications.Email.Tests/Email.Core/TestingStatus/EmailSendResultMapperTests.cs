using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Mappers;
using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Core.TestingStatus;

public class EmailSendResultMapperTests
{
    [Theory]
    [InlineData(null, EmailSendResult.Failed)]
    [InlineData("Bounced", EmailSendResult.Failed_Bounced)]
    [InlineData("Delivered", EmailSendResult.Delivered)]
    [InlineData("Failed", EmailSendResult.Failed)]
    [InlineData("FilteredSpam", EmailSendResult.Failed_FilteredSpam)]
    [InlineData("Quarantined", EmailSendResult.Failed_Quarantined)]
    [InlineData("Suppressed", EmailSendResult.Failed_SupressedRecipient)]
    public void ParseDeliveryStatus_ReturnsCorrectResult(string? deliveryStatus, EmailSendResult expectedResult)
    {
        var result = EmailSendResultMapper.ParseDeliveryStatus(deliveryStatus);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void ParseDeliveryStatus_UnhandledStatus_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => EmailSendResultMapper.ParseDeliveryStatus("unhandledStatus"));
    }
}
