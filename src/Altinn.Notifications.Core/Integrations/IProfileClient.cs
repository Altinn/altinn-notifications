using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Interface describing a client for the profile service
/// </summary>
public interface IProfileClient
{
    /// <summary>
    /// Retrieves contact points for a list of users corresponding to a list of national identity numbers
    /// </summary>
    /// <param name="nationalIdentityNumbers">A list of national identity numbers to look up contact points for</param>
    /// <returns>A list of contact points for the provided national identity numbers </returns>
    public Task<List<UserContactPoints>> GetUserContactPoints(List<string> nationalIdentityNumbers);
}
