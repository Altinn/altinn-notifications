using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Service for generating globally unique identifiers (GUIDs)
/// </summary>
[ExcludeFromCodeCoverage]
public class GuidService : IGuidService
{
    /// <inheritdoc/>
    public Guid NewGuid()
    {
        return Guid.NewGuid();
    }
}
