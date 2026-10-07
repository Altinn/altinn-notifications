using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Tests.Notifications.Core.BackgroundQueues;

namespace Altinn.Notifications.Tests.Notifications.Core.BackgroundQueue;

public class SmsPublishTaskQueueTests : PublishTaskQueueTestsBase
{
    protected override ISendingTimePolicyTaskQueue CreateQueue() => new SmsPublishTaskQueue();
}
