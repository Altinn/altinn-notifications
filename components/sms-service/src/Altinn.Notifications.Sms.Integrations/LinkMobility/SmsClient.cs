using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Shared;
using Altinn.Notifications.Sms.Core.Status;
using LinkMobility.PSWin.Client.Model;

using LinkMobilityModel = global::LinkMobility.PSWin.Client.Model;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility
{
    /// <summary>
    /// Represents an implementation of <see cref="ISmsClient"/> that will use LinkMobility's
    /// SMS gateway to send text messages.
    /// </summary>
    public class SmsClient : ISmsClient
    {
        private readonly IAltinnGatewayClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmsClient"/> class.
        /// </summary>
        /// <param name="client">Gateway Client</param>
        public SmsClient(IAltinnGatewayClient client)
        {
            _client = client;
        }

        /// <inheritdoc />
        public async Task<Result<string, SmsClientErrorResponse>> SendAsync(Core.Sending.Sms sms)
        {
            MessageResult result = await _client.SendAsync(new LinkMobilityModel.Sms(sms.Sender, sms.Recipient, sms.Message));

            if (result.IsStatusOk)
            {
                return result.GatewayReference;
            }

            if (result.StatusText.StartsWith("Invalid RCV"))
            {
                return new SmsClientErrorResponse { SendResult = SmsSendResult.Failed_InvalidReceiver, ErrorMessage = result.StatusText };
            }

            return new SmsClientErrorResponse { SendResult = SmsSendResult.Failed };
        }
    }
}
