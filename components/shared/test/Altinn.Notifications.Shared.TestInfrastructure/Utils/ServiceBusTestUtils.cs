using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace Altinn.Notifications.Shared.TestInfrastructure.Utils;

/// <summary>
/// Utility methods for working with Azure Service Bus in integration tests.
/// </summary>
public static class ServiceBusTestUtils
{
    /// <summary>
    /// Sends a message to the specified Azure Service Bus queue.
    /// </summary>
    /// <param name="connectionString">The connection string for the Azure Service Bus namespace.</param>
    /// <param name="queueName">The name of the queue to send the message to.</param>
    /// <param name="message">The message to send.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    public static async Task SendMessage(string connectionString, string queueName, object message)
    {
        await using var client = new ServiceBusClient(connectionString);
        ServiceBusSender sender = client.CreateSender(queueName);

        var busMessage = new ServiceBusMessage(
            JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }))
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
            ApplicationProperties =
            {
                { "conversation-id", Guid.NewGuid().ToString() },
                { "source", "Altinn.Notifications.Email.Integrations" },
                { "reply-uri", "asb://queue/altinn.notifications.composedemail.send" },
                { "sent-at", DateTime.UtcNow.ToString("o") },
                { "attempts", 0 },
                { "wolverine-protocol-version", "1.0" },
                { "accepted-content-types", "application/json" },
                { "Diagnostic-Id", "00-c03d7491b87f32b5a4b3985f9504a463-f2122e353c32834f-01" }
            }
        };

        await sender.SendMessageAsync(busMessage);
    }

    /// <summary>
    /// Waits for a message to arrive on the specified queue and completes it.
    /// </summary>
    public static async Task<ServiceBusReceivedMessage?> WaitForMessageAsync(
        string connectionString,
        string queueName,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(10);
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);
        using var cts = new CancellationTokenSource(actualTimeout);

        ServiceBusReceivedMessage? message;
        try
        {
            message = await receiver.ReceiveMessageAsync(actualTimeout, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (message == null)
        {
            return null;
        }

        await receiver.CompleteMessageAsync(message);
        return message;
    }

    /// <summary>
    /// Waits for a message to arrive on the dead letter queue.
    /// </summary>
    public static Task<ServiceBusReceivedMessage?> WaitForDeadLetterMessageAsync(
        string connectionString,
        string queueName,
        TimeSpan? timeout = null)
        => WaitForMessageAsync(connectionString, $"{queueName}/$deadletterqueue", timeout);

    /// <summary>
    /// Waits until the specified queue is empty (no messages waiting).
    /// </summary>
    public static async Task<bool> WaitForEmptyAsync(
        string connectionString,
        string queueName,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var pollInterval = TimeSpan.FromMilliseconds(100);
        var maxAttempts = Math.Max(
            1,
            (int)Math.Ceiling(actualTimeout.TotalMilliseconds / pollInterval.TotalMilliseconds));

        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);

        return await WaitForUtils.WaitForAsync(
            async () => await receiver.PeekMessageAsync() == null,
            maxAttempts,
            (int)pollInterval.TotalMilliseconds);
    }

    /// <summary>
    /// Waits until the dead letter queue is empty.
    /// </summary>
    public static Task<bool> WaitForDeadLetterEmptyAsync(
        string connectionString,
        string queueName,
        TimeSpan? timeout = null)
        => WaitForEmptyAsync(connectionString, $"{queueName}/$deadletterqueue", timeout);

    /// <summary>
    /// Drains all currently available messages from the specified queue.
    /// </summary>
    /// <returns>The number of completed messages.</returns>
    public static async Task<int> DrainQueueAsync(
        string connectionString,
        string queueName,
        TimeSpan? idleTimeout = null)
    {
        var timeout = idleTimeout ?? TimeSpan.FromMilliseconds(200);
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);

        int drained = 0;
        while (true)
        {
            var message = await receiver.ReceiveMessageAsync(timeout);
            if (message == null)
            {
                return drained;
            }

            await receiver.CompleteMessageAsync(message);
            drained++;
        }
    }
}
