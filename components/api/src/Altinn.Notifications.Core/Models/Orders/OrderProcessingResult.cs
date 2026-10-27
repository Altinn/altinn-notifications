using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the combined in-memory result of processing a notification order across all channels.
/// Returned by the channel processing services and handed to the repository as a single unit of work
/// to be persisted atomically within one database transaction.
/// </summary>
public sealed record OrderProcessingResult(
    EmailOrderProcessingResult EmailOrderProcessingResult,
    SmsOrderProcessingResult SmsOrderProcessingResult);
