using Altinn.Notifications.Core.BackgroundQueue;

namespace Altinn.Notifications.Tests.Notifications.Core.BackgroundQueues;

public class EmailPublishTaskQueueTests : PublishTaskQueueTestsBase
{
    protected override ISendingTimePolicyTaskQueue CreateQueue() => new EmailPublishTaskQueue();
}
