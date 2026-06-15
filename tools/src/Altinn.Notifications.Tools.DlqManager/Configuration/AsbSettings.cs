namespace Altinn.Notifications.Tools.DlqManager.Configuration;

/// <summary>
/// Azure Service Bus connection settings for the DLQ Manager tool.
/// </summary>
public class AsbSettings
{
    /// <summary>
    /// Full connection string for the Azure Service Bus namespace.
    /// Injected via user secrets or environment variable — never committed.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Name of the SMS send queue. Default matches the value in appsettings.json.
    /// </summary>
    public string SmsSendQueueName { get; set; } = "altinn.notifications.sms.send";

    /// <summary>
    /// Name of the past due orders queue. Default matches the value in appsettings.json.
    /// </summary>
    public string PastDueOrdersQueueName { get; set; } = "altinn.notifications.orders.pastdue";
}
