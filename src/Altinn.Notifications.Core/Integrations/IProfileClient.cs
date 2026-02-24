using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Interface describing a client for the profile service.
/// </summary>
public interface IProfileClient
{
    /// <summary>
    /// Retrieves contact points for a list of external identity users using their external identities.
    /// </summary>
    /// <param name="externalIdentities">A list of external identities (URNs) to look up contact points for.</param>
    /// <returns>A list of contact points for the provided external identities.</returns>
    Task<List<ExternalIdentityContactPoints>> GetExternalIdentityContactPoints(List<string> externalIdentities);

    /// <summary>
    /// Retrieves contact points for a list of users corresponding to a list of national identity numbers
    /// </summary>
    /// <param name="nationalIdentityNumbers">A list of national identity numbers to look up contact points for</param>
    /// <returns>A list of contact points for the provided national identity numbers </returns>
    Task<List<UserContactPoints>> GetUserContactPoints(List<string> nationalIdentityNumbers);

    /// <summary>
    /// Retrieves the user registered contact points for a list of organizations identified by organization numbers
    /// </summary>
    /// <param name="organizationNumbers">The set or organizations to retrieve contact points for</param>
    /// <param name="resourceId">The id of the resource to look up contact points for</param>
    /// <returns>A list of organization contact points containing user registered contact points</returns>
    Task<List<OrganizationContactPoints>> GetUserRegisteredContactPoints(List<string> organizationNumbers, string resourceId);

    /// <summary>
    /// Asynchronously retrieves contact point details for the specified organizations.
    /// </summary>
    /// <param name="organizationNumbers">A collection of organization numbers for which contact point details are requested.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. 
    /// The task result contains a list of <see cref="OrganizationContactPoints"/> representing the contact points of the specified organizations.
    /// </returns>
    Task<List<OrganizationContactPoints>> GetOrganizationContactPoints(List<string> organizationNumbers);
}
