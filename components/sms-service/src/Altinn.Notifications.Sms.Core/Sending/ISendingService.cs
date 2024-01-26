namespace Altinn.Notifications.Sms.Core.Sending;

/// <summary>
/// Describes the required public method of the sms service.
/// </summary>
public interface ISendingService
{
    /// <summary>
    /// Send an sms
    /// </summary>
    /// <param name="sms">The details for an sms to be sent.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendAsync(Sms sms);
}
