using Altinn.Notifications.Core.Enums;
using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Command representing an email delivery report received from the ASB queue.
/// </summary>
public record EmailDeliveryReportCommand(
    string Message);
