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
    public required NotificationOrder Order { get; init; }

    /// <summary>
    /// Gets a value indicating whether this command represents a retry attempt.
    /// When <see langword="true"/>, the handler delegates to
    /// <see cref="Core.Services.Interfaces.IOrderProcessingService.ProcessOrderRetry"/>
    /// instead of the normal <see cref="Core.Services.Interfaces.IOrderProcessingService.ProcessOrder"/> path.
    /// </summary>
    public bool IsRetry { get; init; } = false;
}
