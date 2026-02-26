namespace Altinn.Notifications.Email.Core;

/// <summary>
/// Interface describing a dateTime service
/// </summary>
public interface IDateTimeService
{
    /// <summary>
    /// Provides DateTime UtcNow
    /// </summary>
    public DateTime UtcNow();
}
