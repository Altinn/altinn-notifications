using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Altinn.Notifications.Aspire.Hosting;

/// <summary>
/// Suspends a queue in the Service Bus emulator, like <see href="https://learn.microsoft.com/azure/service-bus-messaging/entity-suspend"/>.
/// </summary>
/// <remarks>
/// The emulator takes administration client calls on its HTTP port, and resets every queue to <c>Active</c> when it restarts.
/// </remarks>
/// <param name="admin">An administration client for the emulator.</param>
internal sealed class QueueStatusSetter(ServiceBusAdministrationClient admin)
{
    /// <summary>
    /// The statuses a queue can be set to.
    /// </summary>
    internal static readonly string[] Statuses =
    [
        EntityStatus.Active.ToString(),
        EntityStatus.ReceiveDisabled.ToString(),
        EntityStatus.SendDisabled.ToString(),
        EntityStatus.Disabled.ToString(),
    ];

    /// <summary>
    /// Creates a setter for the emulator whose HTTP port is <paramref name="managementPort"/>.
    /// </summary>
    /// <param name="managementPort">The emulator's HTTP port, the one its health check uses.</param>
    public static QueueStatusSetter ForEmulator(int managementPort) =>
        new(new ServiceBusAdministrationClient(
            $"Endpoint=sb://localhost:{managementPort};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"));

    /// <summary>
    /// Reads the names of the queues from the emulator's <c>config.json</c>.
    /// </summary>
    /// <param name="emulatorConfigJson">The content of the emulator's <c>config.json</c>.</param>
    public static IReadOnlyList<string> ReadQueues(string emulatorConfigJson)
    {
        using var config = JsonDocument.Parse(emulatorConfigJson);
        return config.RootElement.GetProperty("UserConfig").GetProperty("Namespaces")[0].GetProperty("Queues")
            .EnumerateArray()
            .Select(queue => queue.GetProperty("Name").GetString()!)
            .ToList();
    }

    /// <summary>
    /// Sets the status of <paramref name="queue"/> and reports what it was before.
    /// </summary>
    /// <param name="queue">The queue's name.</param>
    /// <param name="status">One of <see cref="Statuses"/>.</param>
    /// <param name="cancellationToken">Cancels the calls to the emulator.</param>
    public async Task<ExecuteCommandResult> SetAsync(string queue, string? status, CancellationToken cancellationToken)
    {
        if (!Statuses.Contains(status))
        {
            return CommandResults.Failure($"The status must be one of {string.Join(", ", Statuses)}, not '{status}'.");
        }

        try
        {
            var properties = (await admin.GetQueueAsync(queue, cancellationToken)).Value;
            var previous = properties.Status;
            properties.Status = new EntityStatus(status);
            await admin.UpdateQueueAsync(properties, cancellationToken);
            return CommandResults.Success($"{queue}: {previous} → {properties.Status}");
        }
        catch (Exception e) when (e is ServiceBusException or RequestFailedException)
        {
            return CommandResults.Failure($"Could not set the status of {queue}: {e.Message}");
        }
    }
}
