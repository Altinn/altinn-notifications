using LinkMobility.PSWin.Receiver.Model;

namespace Altinn.Notifications.Sms.Core.Status;

/// <summary>
/// Describes the required public method of the status service.
/// </summary>
public interface IStatusService
{
    /// <summary>
    /// Update the status of an sms
    /// </summary>
    /// <param name="message">DeliveryReport from Link Mobility</param>
    Task UpdateStatusAsync(DrMessage message);
}
