namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Pairs a reminder <see cref="NotificationOrder"/> with the effective send time to persist for it.
/// </summary>
/// <remarks>
/// The <see cref="Order"/>'s own <see cref="NotificationOrder.RequestedSendTime"/> retains the originally
/// requested time (persisted as-is in the jsonb payload), while <see cref="RequestedSendTime"/> is the
/// effective send time that should be stored in the relational <c>requestedsendtime</c> column, which may
/// be postponed to align with a Daytime send condition window.
/// </remarks>
/// <param name="Order">The reminder notification order to persist.</param>
/// <param name="RequestedSendTime">The effective requested send time to store in the database column.</param>
public sealed record ReminderNotificationOrder(NotificationOrder Order, DateTime RequestedSendTime);
