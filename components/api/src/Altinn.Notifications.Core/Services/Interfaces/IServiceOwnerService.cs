using Altinn.Authorization.ProblemDetails;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Provides service owner information, such as the sender addresses each service owner is allowed to use, from an in-memory copy of the notifications.serviceowner table.
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
    /// Validates that the senders of a request are registered for the ordering service owner. Each channel is validated only if enforcement is enabled for it.
    /// A <c>null</c> sender is treated as not specified, and the default sender of the channel is always accepted.
    /// An empty or whitespace sender is invalid. All other senders must be registered for the service owner.
    /// </summary>
    /// <param name="serviceOwner">The short name of the service owner placing the order.</param>
    /// <param name="emailSenders">The email sender addresses specified in the request.</param>
    /// <param name="smsSenders">The SMS senders specified in the request.</param>
    /// <returns>A problem naming all invalid senders of the first failing channel, or <c>null</c> if all senders are valid.</returns>
    /// <exception cref="InvalidOperationException">Thrown if enforcement is enabled and the registered senders have not been loaded.</exception>
    ProblemInstance? ValidateSenders(string serviceOwner, IEnumerable<string?> emailSenders, IEnumerable<string?> smsSenders);
}
