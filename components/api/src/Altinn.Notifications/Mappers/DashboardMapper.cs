using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Models.Dashboard;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between dashboard domain models and their external representations.
/// </summary>
public static class DashboardMapper
{
    /// <summary>
    /// Maps a list of <see cref="DashboardNotification"/> to their external representation.
    /// </summary>
    /// <param name="notifications">The list of dashboard notifications to map.</param>
    /// <returns>A list of <see cref="DashboardNotificationExt"/>.</returns>
    public static List<DashboardNotificationExt> MapToDashboardNotificationExtList(this List<DashboardNotification> notifications)
    {
        return [.. notifications.Select(MapToDashboardNotificationExt)];
    }

    private static DashboardNotificationExt MapToDashboardNotificationExt(DashboardNotification notification)
    {
        return new DashboardNotificationExt
        {
            ShipmentId = notification.ShipmentId,
            CreatorName = notification.CreatorName,
            ResourceId = notification.ResourceId,
            SendersReference = notification.SendersReference,
            RequestedSendTime = notification.RequestedSendTime,
            NotificationChannel = notification.NotificationChannel,
            NotificationType = notification.NotificationType,
            DeliveryAttempts = [.. notification.DeliveryAttempts.Select(r => new DashboardDeliveryAttemptExt
            {
                NationalIdentityNumber = r.NationalIdentityNumber,
                Channel = r.Channel,
                EmailAddress = r.EmailAddress,
                MobileNumber = r.MobileNumber,
                Result = r.Result,
                ResultTime = r.ResultTime,
            })],
        };
    }
}
