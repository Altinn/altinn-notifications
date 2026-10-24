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
    /// Empty means stub mode: the proxy answers sends itself, by recipient number, and nothing leaves the machine.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Sends every send to <see cref="FallbackUrl"/> instead of answering it, so an operator answers it in the simulator.
    /// Only in stub mode, and only with a <see cref="FallbackUrl"/>.
    /// </summary>
    public bool Interactive { get; set; }

    /// <summary>
    /// Where requests the stub does not recognise go, as <c>{FallbackUrl}linkmobility/{path}</c>, e.g. the simulator's
    /// <c>http://localhost:5096/intercept/</c>. Empty means a plain 404.
    /// </summary>
    public string FallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// The SMS service's delivery report endpoint, e.g. <c>http://localhost:5092/notifications/sms/api/v1/reports</c>.
    /// Empty means no delivery reports.
    /// </summary>
    public string DeliveryReportUrl { get; set; } = string.Empty;

    /// <summary>
    /// The basic auth user the SMS service expects on delivery reports, its <c>SmsDeliveryReportSettings:UserSettings:Username</c>.
    /// </summary>
    public string DeliveryReportUsername { get; set; } = "username";

    /// <summary>
    /// The basic auth password the SMS service expects on delivery reports, its <c>SmsDeliveryReportSettings:UserSettings:Password</c>.
    /// </summary>
    public string DeliveryReportPassword { get; set; } = "password";

    /// <summary>
    /// How long after an accepted send the delivery report is sent.
    /// </summary>
    public int DeliveryReportDelaySeconds { get; set; } = 2;
}
