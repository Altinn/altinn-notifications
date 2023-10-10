using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for all operations related to application owner configuration
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationConstants.POLICY_SCOPE_CONFIG_ADMIN)]
[Route("notifications/api/v1/organisation/config")]
public class ApplicationOwnerConfigController : ControllerBase
{
    private readonly IApplicationOwnerConfigService _applicationOwnerConfigService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationOwnerConfigController"/> class.
    /// </summary>
    public ApplicationOwnerConfigController(IApplicationOwnerConfigService applicationOwnerConfigService)
    {
        _applicationOwnerConfigService = applicationOwnerConfigService;
    }

    /// <summary>
    /// Endpoint for retrieving the current configuration for the authenticated application owner.
    /// </summary>
    /// <returns>The current configuration details.</returns>
    [HttpGet]
    public async Task<ActionResult<ApplicationOwnerConfigExt>> GetApplicationOwnerConfigurationForAuthenticatedOrg()
    {
        string? expectedCreator = User.GetOrg();
        if (expectedCreator is null)
        {
            return Forbid();
        }

        ApplicationOwnerConfig config = await _applicationOwnerConfigService.GetApplicationOwnerConfig(expectedCreator);
        
        return config.ToApplicationOwnerConfigExt();
    }

    /// <summary>
    /// Endpoint for retrieving the current configuration for the authenticated application owner.
    /// </summary>
    /// <returns>The current configuration details.</returns>
    [HttpGet]
    public async Task<ActionResult> SetApplicationOwnerConfigurationForAuthenticatedOrg(ApplicationOwnerConfigExt config)
    {
        string? expectedCreator = User.GetOrg();
        if (expectedCreator is null || expectedCreator != config.OrgId)
        {
            return Forbid();
        }

        await _applicationOwnerConfigService.WriteApplicationOwnerConfig(config.ToApplicationOwnerConfig());

        return NoContent();
    }
}
