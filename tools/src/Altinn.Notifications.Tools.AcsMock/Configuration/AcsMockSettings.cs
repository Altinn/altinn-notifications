namespace Altinn.Notifications.Tools.AcsMock.Configuration;

/// <summary>
/// Settings for the local Azure Communication Services Email mock.
/// </summary>
public class AcsMockSettings
{
    /// <summary>
    /// The localhost port the mock listens on. The email service's connection string must point here.
    /// </summary>
    public int Port { get; set; } = 5095;

    /// <summary>
    /// The base64 access key requests must be signed with, the <c>accesskey</c> in the email service's connection string.
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// How long a normal send stays <c>Running</c> before it succeeds or fails.
    /// </summary>
    public int RunningSeconds { get; set; } = 2;

    /// <summary>
    /// How long a send to a <c>slow@</c> recipient stays <c>Running</c>.
    /// </summary>
    public int SlowSeconds { get; set; } = 30;

    /// <summary>
    /// The <c>Retry-After</c> a send to a <c>throttle@</c> recipient gets on its first attempt.
    /// </summary>
    public int ThrottleRetryAfterSeconds { get; set; } = 10;

    /// <summary>
    /// How long after the first poll that sees <c>Succeeded</c> the delivery report is sent.
    /// </summary>
    public int DeliveryReportDelaySeconds { get; set; } = 2;

    /// <summary>
    /// The Service Bus the API reads delivery reports from. Empty means no delivery reports.
    /// </summary>
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The queue Event Grid delivers <c>EmailDeliveryReportReceived</c> events to.
    /// </summary>
    public string DeliveryReportQueueName { get; set; } = "altinn.notifications.email.deliveryreports";

    /// <summary>
    /// Where requests the mock does not recognise go, as <c>{FallbackUrl}acs/{path}</c>, e.g. the simulator's
    /// <c>http://localhost:5096/intercept/</c>. Empty means a plain 404.
    /// </summary>
    public string FallbackUrl { get; set; } = string.Empty;
}
