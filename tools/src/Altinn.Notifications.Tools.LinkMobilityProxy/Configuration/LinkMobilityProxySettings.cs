namespace Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

/// <summary>
/// Settings for the local LinkMobility proxy.
/// </summary>
public class LinkMobilityProxySettings
{
    /// <summary>
    /// The localhost port the proxy listens on. Point <c>SmsGatewaySettings:Endpoint</c> in the SMS service here.
    /// </summary>
    public int Port { get; set; } = 5093;

    /// <summary>
    /// The LinkMobility gateway to forward to, e.g. <c>https://xml-test.pswin.com</c>.
    /// Empty means stub mode: the proxy answers every request with OK itself and nothing leaves the machine.
    /// </summary>
    public string Target { get; set; } = string.Empty;
}
