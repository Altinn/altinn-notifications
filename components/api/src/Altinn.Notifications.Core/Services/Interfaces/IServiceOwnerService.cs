using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Provides service owner information, such as the sender addresses each service owner is allowed to use, from an in-memory copy of the database table.
/// </summary>
public interface IServiceOwnerService
{
    /// <summary>
    /// Loads the registered senders of all service owners from the database into memory.
    /// Any previously loaded senders are replaced, and the new data is only used once loading has completed.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the registered senders have been loaded.</returns>
    Task LoadSenders(CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a sender may be used by the ordering service owner.
    /// The sender is allowed if enforcement is disabled for the channel, if no sender is specified (<c>null</c>, empty or whitespace,
    /// since the default sender of the channel is used instead), if it is the default sender of the channel, or if it is registered for the service owner.
    /// Service owners and senders are compared case-insensitively.
    /// </summary>
    /// <param name="serviceOwner">The short name of the service owner placing the order.</param>
    /// <param name="addressType">The channel the sender belongs to.</param>
    /// <param name="sender">The sender specified in the request.</param>
    /// <returns><c>true</c> if the sender is allowed; otherwise, <c>false</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the sender needs to be looked up and the registered senders have not been loaded.</exception>
    bool IsSenderAllowed(string serviceOwner, AddressType addressType, string? sender);
}
