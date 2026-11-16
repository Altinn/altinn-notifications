using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Repository for service owner data.
/// </summary>
public interface IServiceOwnerRepository
{
    /// <summary>
    /// Retrieves the registered email and SMS senders of all service owners.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list with one entry for each registered sender, or an empty list if none are registered.</returns>
    Task<IReadOnlyList<RegisteredSender>> GetRegisteredSenders(CancellationToken cancellationToken = default);
}
