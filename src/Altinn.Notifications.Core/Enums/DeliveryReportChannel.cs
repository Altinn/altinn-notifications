namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Used to denote what type of delivery report is contained in a <see cref="Models.DeadDeliveryReport"/>
/// </summary>
public enum DeliveryReportChannel
{
    /// <summary>
    /// Azure Email Communication Services
    /// </summary>
    AzureEmailCommunicationServices = 1,

    /// <summary>
    /// Link Mobility SMS
    /// </summary>
    LinkMobility = 2
}
