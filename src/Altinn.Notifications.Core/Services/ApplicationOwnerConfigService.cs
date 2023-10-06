using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// An implementation of <see cref="IApplicationOwnerConfigService"/> with the necessary business logic
/// to handle application owner specific configuration in the Notifications application.
/// </summary>
public class ApplicationOwnerConfigService : IApplicationOwnerConfigService
{
    private readonly IApplicationOwnerConfigRepository _applicationOwnerConfigRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationOwnerConfigService"/> class.
    /// </summary>
    /// <param name="applicationOwnerConfigRepository">A repository implementation</param>
    public ApplicationOwnerConfigService(IApplicationOwnerConfigRepository applicationOwnerConfigRepository)
    {
        _applicationOwnerConfigRepository = applicationOwnerConfigRepository;
    }

    /// <inheritdoc/>
    public async Task<ApplicationOwnerConfig> GetApplicationOwnerConfig(string orgId)
    {
        return await _applicationOwnerConfigRepository.GetApplicationOwnerConfig(orgId) 
            ?? new ApplicationOwnerConfig(orgId);
    }

    /// <inheritdoc/>
    public async Task WriteApplicationOwnerConfig(ApplicationOwnerConfig applicationOwnerConfig)
    {
        await _applicationOwnerConfigRepository.WriteApplicationOwnerConfig(applicationOwnerConfig);
    }
}
