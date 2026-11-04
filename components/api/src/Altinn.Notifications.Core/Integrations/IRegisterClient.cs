using Altinn.Notifications.Core.Models.Parties;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines a contract for interacting with the register service.
/// </summary>
public interface IRegisterClient
{
    /// <summary>
    /// Retrieves party details using the notifications query endpoint.
    /// </summary>
    /// <param name="organizationNumbers">The organization numbers to look up.</param>
    /// <param name="nationalIdentityNumbers">The national identity numbers to look up.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The details of the matching parties. Missing parties and identifiers that are not valid are omitted.
    /// </returns>
    /// <exception cref="Shared.PlatformDependencyException">Thrown when the register service is unavailable or returns an invalid response.</exception>
    Task<List<PartyDetails>> QueryPartyDetails(
        IReadOnlyCollection<string> organizationNumbers,
        IReadOnlyCollection<string> nationalIdentityNumbers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves party details for the specified persons and/or organizations.
    /// </summary>
    /// <remarks>
    /// This method should be deprecated. Use <see cref="QueryPartyDetails"/> instead.
    /// It is kept for backwards compatibility.
    /// </remarks>
    /// <param name="organizationNumbers">A collection of organization numbers for which party details are requested.</param>
    /// <param name="socialSecurityNumbers">A collection of social security numbers for which party details are requested.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. 
    /// The task result contains a list of <see cref="PartyDetails"/> representing the details of the specified individuals and organizations.
    /// </returns>
    /// <exception cref="Shared.PlatformDependencyException">Thrown when the register service is unavailable or returns an error.</exception>
    Task<List<PartyDetails>> GetPartyDetails(List<string>? organizationNumbers, List<string>? socialSecurityNumbers);
}
