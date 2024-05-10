using Altinn.Authorization.ABAC.Xacml.JsonProfile;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers
{
    /// <summary>
    /// Temporary adding a controller in order to generate som test data from Authorization.
    /// </summary>
    [ApiController]
    [Route("notifications/api/v1/test")]
    [AllowAnonymous]
    public class TestController(Authorization.IAuthorizationService authorizationService) 
        : ControllerBase
    {
        private readonly Authorization.IAuthorizationService _authorizationService = authorizationService;

        /// <summary>
        /// Test method for authorization.
        /// </summary>
        [HttpPost]
        public async Task<Dictionary<string, Dictionary<string, bool>>> Authorize([FromBody]AuthZ authz)
        {
            Dictionary<int, List<int>> orgRightHolders = new();
            foreach (var item in authz.OrgRightHolders)
            {
                orgRightHolders.Add(item.ResourceOwnerId, item.UserIds);
            }

            return await _authorizationService.AuthorizeUsersForResource(orgRightHolders, authz.ResourceId);
        }
    }

    /// <summary>
    /// Request input
    /// </summary>
    public class AuthZ
    {
        /// <summary>
        /// Resource
        /// </summary>
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// Resource
        /// </summary>
        public List<OrgRightHolders> OrgRightHolders { get; set; } = [];
    }

    /// <summary>
    /// Represent a single resource owner and a list of potential notification recipients.
    /// </summary>
    public class OrgRightHolders
    {
        /// <summary>
        /// The owner of a given resource. 
        /// </summary>
        public int ResourceOwnerId { get; set; }

        /// <summary>
        /// List of users
        /// </summary>
        public List<int> UserIds { get; set; } = [];
    }
}
