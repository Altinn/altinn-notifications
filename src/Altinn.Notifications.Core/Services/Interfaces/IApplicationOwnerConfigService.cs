using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// This interface is a description of an application owner configuration service implementation.
/// Such a service will be required to have methods needed to register, change and retrieve application
/// owner configuration.
/// </summary>
public interface IApplicationOwnerConfigService
{
    /// <summary>
    /// Get the full set of configuration options for a specified application owner.
    /// </summary>
    /// <param name="orgId">The unique id of an application owner.</param>
    /// <returns>A <see cref="Task{ApplicationOwnerConfig}"/> representing the result of the asynchronous operation.</returns>
    Task<ApplicationOwnerConfig> GetApplicationOwnerConfig(string orgId);

    /// <summary>
    /// Write a full <see cref="ApplicationOwnerConfig"/> object to storage.
    /// </summary>
    /// <param name="applicationOwnerConfig">The configuration object to write to storage.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    Task WriteApplicationOwnerConfig(ApplicationOwnerConfig applicationOwnerConfig);
}
