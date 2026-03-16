#nullable enable
using Azure.Messaging.ServiceBus;

namespace Altinn.Notifications.Shared.TestInfrastructure.Utils;

/// <summary>
/// Utility methods for working with Azure Service Bus in integration tests.
/// </summary>
public static class ServiceBusTestUtils
{
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
}
