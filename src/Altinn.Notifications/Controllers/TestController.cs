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
        public async Task<Dictionary<string, bool>> Authorize([FromBody]AuthZ authz)
        {
            return await _authorizationService.AuthorizeUsersForResource(authz.UserIds, authz.ResourceId, authz.ResourceOwnerId);
        }
    }

    /// <summary>
    /// Request input
    /// </summary>
    public class AuthZ
    {
        /// <summary>
        /// List of users
        /// </summary>
        public List<int> UserIds { get; set; } = [];

        /// <summary>
        /// Resource
        /// </summary>
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// Resource
        /// </summary>
        public int ResourceOwnerId { get; set; }
    }
}
