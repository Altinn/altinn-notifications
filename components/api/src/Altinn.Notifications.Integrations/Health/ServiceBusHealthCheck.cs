using Altinn.Notifications.Integrations.Configuration;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Integrations.Health;

/// <summary>
/// Health check verifying connectivity to the Azure Service Bus queues used by the application.
/// Each queue is probed in the direction the application uses it, without consuming or sending any message:
/// queues the application listens on are peeked, and queues it publishes to are opened with a sender link.
/// A failure indicates an unreachable broker, invalid credentials, a missing queue or a missing permission.
/// </summary>
/// <param name="client">The Service Bus client used for the probes. It is disposed together with this check.</param>
/// <param name="settings">The Wolverine settings that define the queue names.</param>
public sealed class ServiceBusHealthCheck(ServiceBusClient client, WolverineSettings settings) : IHealthCheck, IAsyncDisposable
{
    private const string _sendDirection = "send";

    private const string _listenDirection = "listen";

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        IEnumerable<string> listenQueues =
        [
            settings.SmsSendResultQueueName,
            settings.EmailSendResultQueueName,
            settings.SmsDeliveryReportQueueName,
            settings.EmailDeliveryReportQueueName,
            settings.EmailServiceRateLimitQueueName
        ];

        IEnumerable<string> sendQueues =
        [
            settings.SendSmsQueueName,
            settings.EmailSendQueueName,
            settings.ComposedEmailSendQueueName
        ];

        IEnumerable<Task<string?>> probes = listenQueues
            .Select(queue => ProbeAsync(queue, _listenDirection, PeekAsync, cancellationToken))
            .Concat(sendQueues.Select(queue => ProbeAsync(queue, _sendDirection, OpenSenderAsync, cancellationToken)));

        string?[] results = await Task.WhenAll(probes);
        List<string> failures = [.. results.OfType<string>()];

        return failures.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"Service Bus queues not reachable: {string.Join("; ", failures)}");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
    }

    private static async Task<string?> ProbeAsync(
        string queue,
        string direction,
        Func<string, CancellationToken, Task> probe,
        CancellationToken cancellationToken)
    {
        try
        {
            await probe(queue, cancellationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"{queue} ({direction}): {ex.GetType().Name}";
        }
    }

    private async Task PeekAsync(string queue, CancellationToken cancellationToken)
    {
        await using ServiceBusReceiver receiver = client.CreateReceiver(queue);

        await receiver.PeekMessageAsync(cancellationToken: cancellationToken);
    }

    private async Task OpenSenderAsync(string queue, CancellationToken cancellationToken)
    {
        await using ServiceBusSender sender = client.CreateSender(queue);

        using ServiceBusMessageBatch batch = await sender.CreateMessageBatchAsync(cancellationToken);
    }
}
