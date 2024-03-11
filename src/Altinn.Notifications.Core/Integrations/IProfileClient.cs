using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Integrations
{
    /// <summary>
    /// Interface describing a client for the profile service
    /// </summary>
    public interface IProfileClient
    {
        /// <summary>
        /// Retrieves contact points for a user 
        /// </summary>
        /// <returns></returns>
        public Task<List<UserContactPoints>> GetUserContactPoints(List<string> nationalIdentityNumbers);
    }
}
