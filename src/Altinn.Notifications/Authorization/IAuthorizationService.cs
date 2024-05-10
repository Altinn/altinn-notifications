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
    /// <param name="orgRightHolders">The list organizations with associated right holders.</param>
    /// <param name="resourceId">The id of the resource.</param>
    /// <returns>A task</returns>
    Task<Dictionary<string, Dictionary<string, bool>>> AuthorizeUsersForResource(Dictionary<int, List<int>> orgRightHolders, string resourceId);
}
