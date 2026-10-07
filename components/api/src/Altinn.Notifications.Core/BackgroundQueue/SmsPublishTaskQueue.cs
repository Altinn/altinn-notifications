namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Per-policy signaling with duplicate coalescing for SMS publishing.
/// </summary>
public class SmsPublishTaskQueue : SendingTimePolicyTaskQueue, ISmsPublishTaskQueue
{
}
