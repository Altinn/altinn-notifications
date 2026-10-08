using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Represents a queue for coordinating SMS publish operations per <see cref="SendingTimePolicy"/>.
/// At most one work item per policy can be active or pending at any given time.
/// </summary>
public interface ISmsPublishTaskQueue : ISendingTimePolicyTaskQueue
{
}
