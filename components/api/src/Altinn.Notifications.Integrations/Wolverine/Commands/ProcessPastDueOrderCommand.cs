using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Integrations.Wolverine.Commands;

/// <summary>
/// Command published to the ASB queue when a past-due order is ready for processing.
/// Consumed by <see cref="Handlers.ProcessPastDueOrderHandler"/> within the same API instance.
/// </summary>
public sealed record ProcessPastDueOrderCommand
{
    /// <summary>
    /// Gets the notification order to be processed.
    /// </summary>
    public NotificationOrder Order { get; init; } = null!;
}
