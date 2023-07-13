using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the GuidServiceS
/// </summary>
public class GuidService : IGuidService
{
    /// <inheritdoc/>
    public string NewGuidAsString()
    {
        return Guid.NewGuid().ToString();
    }
}