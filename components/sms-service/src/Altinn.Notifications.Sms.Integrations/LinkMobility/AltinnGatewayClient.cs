using LinkMobility.PSWin.Client;
using LinkMobility.PSWin.Client.Model;
using LinkMobility.PSWin.Client.Transports;

using LinkMobilityModel = LinkMobility.PSWin.Client.Model;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
///  Wrapper class for the LinkMobility SMS Gateway client to support DI
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AltinnGatewayClient"/> class.
/// </remarks>
public class AltinnGatewayClient(HttpClient httpClient, SmsGatewaySettings gatewayConfig) : IAltinnGatewayClient
{
    private readonly GatewayClient _client = new(new XmlTransport(gatewayConfig.Username, gatewayConfig.Password, new Uri(gatewayConfig.Endpoint), httpClient));

    /// <inheritdoc/>
    public async Task<MessageResult> SendAsync(LinkMobilityModel.Sms message)
    {
        try
        {
            return await _client.SendAsync(message);
        }
        catch (TaskCanceledException ex)
        {
            throw new SmsGatewayException("SMS gateway request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SmsGatewayException("SMS gateway request failed.", ex);
        }
        catch (SendMessageException ex)
        {
            throw new SmsGatewayException("SMS gateway returned an error response.", ex);
        }
    }
}
