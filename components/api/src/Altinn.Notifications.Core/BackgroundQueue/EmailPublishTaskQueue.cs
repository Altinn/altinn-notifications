namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Per-policy signaling with duplicate coalescing for email publishing.
/// </summary>
public class EmailPublishTaskQueue : SendingTimePolicyTaskQueue, IEmailPublishTaskQueue
{
}
