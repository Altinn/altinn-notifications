using LinkMobility.PSWin.Client.Model;

using LinkMobilityModel = LinkMobility.PSWin.Client.Model;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility
{
    /// <summary>
    /// Interface for the gateway client
    /// </summary>
    public interface IAltinnGatewayClient
    {
        /// <summary>
        /// Send sms async
        /// </summary>
        public Task<MessageResult> SendAsync(LinkMobilityModel.Sms message);
    }
}
