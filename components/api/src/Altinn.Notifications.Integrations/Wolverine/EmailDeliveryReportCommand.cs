using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Command representing an email delivery report received from the ASB queue.
/// </summary>
/// <param name="NotificationId">The notification identifier.</param>
/// <param name="OperationId">The ACS operation identifier.</param>
/// <param name="SendResult">The email send result.</param>
public record EmailDeliveryReportCommand(
    Guid? NotificationId,
    string OperationId,
    EmailNotificationResultType? SendResult);
