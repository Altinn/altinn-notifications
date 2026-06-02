namespace Altinn.Notifications.Tools.SmsDeliveryReporter.Configuration;

public class ReporterSettings
{
    public string EndpointUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string InputFile { get; set; } = "status-vs-gateway.txt";
}
