using System.Diagnostics.CodeAnalysis;

using LinkMobility.PSWin.Client;
using LinkMobility.PSWin.Client.Model;
using LinkMobility.PSWin.Client.Transports;

using LinkMobilityModel = LinkMobility.PSWin.Client.Model;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
///  Wrapper class for the LinkMobility SMS Gateway client to support DI
/// </summary>
[ExcludeFromCodeCoverage]
public class AltinnGatewayClient : IAltinnGatewayClient
{
    private readonly GatewayClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AltinnGatewayClient"/> class.
    /// </summary>
    public AltinnGatewayClient(SmsGatewaySettings gatewayConfig)
    {
        _client = new(new XmlTransport(gatewayConfig.Username, gatewayConfig.Password, new Uri(gatewayConfig.Endpoint)));
    }

    /// <inheritdoc/>
    public async Task<MessageResult> SendAsync(LinkMobilityModel.Sms message)
    {
        return await _client.SendAsync(message);
    }
}
