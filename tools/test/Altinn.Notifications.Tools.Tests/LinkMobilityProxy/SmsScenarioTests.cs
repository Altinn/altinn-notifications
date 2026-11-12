using System.Threading.Tasks;

using Altinn.Notifications.Sms.Core.Status;
using Altinn.Notifications.Tools.LinkMobilityProxy;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.LinkMobilityProxy;

public class SmsScenarioTests
{
    [Theory]
    [InlineData("+4799999999", SmsScenario.Delivered)]
    [InlineData("+4799999900", SmsScenario.Delivered)]
    [InlineData("+4799999901", SmsScenario.Undelivered)]
    [InlineData("4799999902", SmsScenario.Expired)]
    [InlineData("+47 999 999 03", SmsScenario.NoReport)]
    [InlineData("+4799999904", SmsScenario.InvalidRecipient)]
    [InlineData("+4799999905", SmsScenario.Rejected)]
    [InlineData("+4799999906", SmsScenario.Barred)]
    [InlineData("+4799999907", SmsScenario.Deleted)]
    [InlineData("+4799999908", SmsScenario.Failed)]
    [InlineData("+4799999909", SmsScenario.Delivered)]
    [InlineData("1", SmsScenario.Delivered)]
    [InlineData("", SmsScenario.Delivered)]
    public void Pick_UsesLastTwoDigits(string recipient, int expected)
    {
        Assert.Equal((SmsScenario)expected, SmsScenarios.Pick(recipient));
    }

    [Theory]
    [InlineData(SmsScenario.NoReport)]
    [InlineData(SmsScenario.InvalidRecipient)]
    public void DeliveryState_ScenarioWithoutReport_IsNull(int scenario)
    {
        Assert.Null(SmsScenarios.DeliveryState((SmsScenario)scenario));
    }

    /// <summary>
    /// Runs each report through the SMS service's own receiver and mapper, so the states are ones it knows.
    /// </summary>
    [Theory]
    [InlineData(SmsScenario.Delivered, SmsSendResult.Delivered)]
    [InlineData(SmsScenario.Undelivered, SmsSendResult.Failed_Undelivered)]
    [InlineData(SmsScenario.Expired, SmsSendResult.Failed_Expired)]
    [InlineData(SmsScenario.Rejected, SmsSendResult.Failed_Rejected)]
    [InlineData(SmsScenario.Barred, SmsSendResult.Failed_BarredReceiver)]
    [InlineData(SmsScenario.Deleted, SmsSendResult.Failed_Deleted)]
    [InlineData(SmsScenario.Failed, SmsSendResult.Failed)]
    public async Task DeliveryReport_IsParsedBySmsServiceAsSendResult(int scenario, SmsSendResult expected)
    {
        string report = GatewayXml.DeliveryReport("ref-1", "+4799999999", SmsScenarios.DeliveryState((SmsScenario)scenario)!, DateTimeOffset.Now);

        var message = await ProxyHost.ReceiveAsync(report);

        Assert.Equal("ref-1", message.Reference);
        Assert.Equal("+4799999999", message.Receiver);
        Assert.Equal(expected, SmsSendResultMapper.ParseDeliveryState(message.State));
    }
}
