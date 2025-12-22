using Azure.Messaging.EventGrid;

using Npgsql;

using Tools.EventGrid;

namespace Tools;

internal static class EventGridUtil
{
    internal static async Task ProcessAndPostEventsAsync(
        IEnumerable<dynamic> operationResults,
        NpgsqlDataSource dataSource,
        IEventGridClient eventGridClient)
    {
        foreach (var result in operationResults)
        {
            if (!await ShouldProcessNotificationAsync(dataSource, result))
            {
                continue;
            }

            var eventGridEvent = CreateEventGridEvent(result);
            await Task.Delay(100);

            await PostEventToGridAsync(eventGridClient, eventGridEvent, result);
        }
    }

    internal static async Task<bool> ShouldProcessNotificationAsync(NpgsqlDataSource dataSource, dynamic result)
    {
        var isInSucceededState = await PostgresUtil.IsEmailNotificationInSucceededState(dataSource, result.OperationId);
        if (!isInSucceededState)
        {
            Console.WriteLine($"Notification {result.NotificationId} with OperationId {result.OperationId} is already marked as Delivered or final. Skipping Event Grid post.");
            return false;
        }

        return true;
    }

    internal static EventGridEvent CreateEventGridEvent(dynamic result)
    {
        var bd = BinaryData.FromObjectAsJson(new
        {
            messageId = result.OperationId,
            status = Util.MapStatus(result)
        });

        var subject = $"sender/senderid@azure.com/message/{result.OperationId}";
        var eventGridEvent = new EventGridEvent(subject, "Microsoft.Communication.EmailDeliveryReportReceived", "1.0", bd);
        eventGridEvent.EventTime = DateTime.UtcNow;

        return eventGridEvent;
    }

    internal static async Task PostEventToGridAsync(IEventGridClient eventGridClient, EventGridEvent eventGridEvent, dynamic result)
    {
        var eventArray = new[] { eventGridEvent };

        await eventGridClient.PostEventsAsync(eventArray, CancellationToken.None);

        Console.WriteLine($"✓ Event Grid event posted for notification {result.NotificationId} with OperationId {result.OperationId}");
    }
}
