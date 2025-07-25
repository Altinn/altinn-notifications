using Altinn.Notifications.Core.Models.Parties;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines a contract for interacting with the register service.
/// </summary>
public interface IRegisterClient
{
    /// <summary>
    /// Asynchronously retrieves party details for the specified persons and/or organizations.
    /// </summary>
    /// <param name="organizationNumbers">A collection of organization numbers for which party details are requested.</param>
    /// <param name="socialSecurityNumbers">A collection of social security numbers for which party details are requested.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. 
    /// The task result contains a list of <see cref="PartyDetails"/> representing the details of the specified individuals and organizations.
    /// </returns>
    Task<List<PartyDetails>> GetPartyDetails(List<string>? organizationNumbers, List<string>? socialSecurityNumbers);
}
