using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Repository.Interfaces;

/// <summary>
/// This interface defines the required methods for a repository class working with settings
/// </summary>
public interface IApplicationOwnerConfigRepository
{
    /// <summary>
    /// Retrieve the application owner specific settings from the database.
    /// </summary>
    /// <param name="orgId">The unique short name for an application owner.</param>
    /// <returns>The identified application owner settings if found. Otherwise null.</returns>
    Task<ApplicationOwnerConfig?> GetOrgSettings(string orgId);
}
