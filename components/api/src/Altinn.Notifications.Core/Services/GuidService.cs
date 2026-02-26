using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Service for generating
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
