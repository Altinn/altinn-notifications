using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Email.Core;

/// <summary>
/// Implemntation of a dateTime service
/// </summary>
[ExcludeFromCodeCoverage]
public class DateTimeService : IDateTimeService
{
    /// <inheritdoc/>
    public DateTime UtcNow()
    {
        return DateTime.UtcNow;
    }
}
