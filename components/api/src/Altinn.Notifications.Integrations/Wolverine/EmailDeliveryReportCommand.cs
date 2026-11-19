using Azure.Messaging.ServiceBus;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Command representing an email delivery report received from the ASB queue.
/// </summary>
/// <param name="Message">The received Azure Service Bus message.</param>
public record EmailDeliveryReportCommand(ServiceBusReceivedMessage Message);
