using Altinn.Authorization.ABAC.Xacml.JsonProfile;

namespace Altinn.Notifications.Authorization;

/// <summary>
/// Describes the necessary functions of an authorization service that can perform
/// notification recipient filtering based on authorization
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Describes a method that can create an authorization request to authorize a set of
    /// users for access to a resource.
    /// </summary>
    /// <param name="userIds">The list of user ids.</param>
    /// <param name="resourceId">The id of the resource.</param>
    /// <param name="resourceOwnerId">The party id of the resource owner.</param>
    /// <returns>A task</returns>
    Task<Dictionary<string, bool>> AuthorizeUsersForResource(List<int> userIds, string resourceId, int resourceOwnerId);
}
