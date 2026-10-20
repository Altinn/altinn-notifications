using Altinn.Notifications.Core.Models.Parties;
using Altinn.Register.Contracts;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines a contract for interacting with the register service.
/// </summary>
public interface IRegisterClient
{
    /// <summary>
    /// Retrieves parties using the notifications query endpoint.
    /// </summary>
    /// <param name="parties">Up to 100 party identifiers to look up.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching parties, including identifiers and display names. Missing parties are omitted.</returns>
    /// <exception cref="Shared.PlatformDependencyException">Thrown when the register service is unavailable or returns an invalid response.</exception>
    Task<List<Party>> GetParties(IReadOnlyCollection<PartyUrn> parties, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves party details for the specified persons and/or organizations.
    /// </summary>
    /// <param name="organizationNumbers">A collection of organization numbers for which party details are requested.</param>
    /// <param name="socialSecurityNumbers">A collection of social security numbers for which party details are requested.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. 
    /// The task result contains a list of <see cref="PartyDetails"/> representing the details of the specified individuals and organizations.
    /// </returns>
    /// <exception cref="Shared.PlatformDependencyException">Thrown when the register service is unavailable or returns an error.</exception>
    Task<List<PartyDetails>> GetPartyDetails(List<string>? organizationNumbers, List<string>? socialSecurityNumbers);
}
