using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting.ApplicationModel;

using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Adds commands for the local Service Bus emulator.
/// </summary>
public static class ServiceBusEmulatorBuilderExtensions
{
    /// <summary>
    /// The name of the queue argument of the Set queue status command.
    /// </summary>
    private const string QueueArgument = "queue";

    /// <summary>
    /// The name of the status argument of the Set queue status command.
    /// </summary>
    private const string StatusArgument = "status";

    /// <summary>
    /// Adds <b>Set queue status</b>, which suspends a queue in the emulator to show how the services handle a queue that is
    /// <c>ReceiveDisabled</c>, <c>SendDisabled</c> or <c>Disabled</c>. It is enabled while the resource is healthy.
    /// </summary>
    /// <param name="serviceBus">The resource that stands for the emulator.</param>
    /// <param name="emulatorConfigPath">The emulator's <c>config.json</c>, which lists the queues to choose from.</param>
    /// <param name="managementPort">The emulator's HTTP port, the one its health check uses.</param>
    public static IResourceBuilder<T> WithQueueStatusCommand<T>(this IResourceBuilder<T> serviceBus, string emulatorConfigPath, int managementPort)
        where T : IResource
    {
        var queues = QueueStatusSetter.ReadQueues(File.ReadAllText(emulatorConfigPath))
            .Select(queue => KeyValuePair.Create(queue, queue))
            .ToList();
        var statuses = QueueStatusSetter.Statuses
            .Select(status => KeyValuePair.Create(status, status))
            .ToList();
        var setter = QueueStatusSetter.ForEmulator(managementPort);

        return serviceBus.WithCommand(
            "set-queue-status",
            "Set queue status",
            context => setter.SetAsync(context.Arguments.GetString(QueueArgument)!, context.Arguments.GetString(StatusArgument), context.CancellationToken),
            new CommandOptions
            {
                Description = "Disables send, receive or both on a queue, to see how the services handle a suspended queue. The emulator sets every queue back to Active when it restarts.",
                IconName = "Pause",
                Arguments =
                [
                    new InteractionInput { Name = QueueArgument, Label = "Queue", InputType = InputType.Choice, Required = true, Options = queues },
                    new InteractionInput { Name = StatusArgument, Label = "Status", InputType = InputType.Choice, Required = true, Options = statuses, Value = EntityStatus.ReceiveDisabled.ToString() },
                ],
                UpdateState = context => IsEnabled(context.ResourceSnapshot.HealthStatus),
            });
    }

    /// <summary>
    /// The command needs the emulator, so it is enabled only while the health check passes.
    /// </summary>
    /// <param name="health">The resource's health.</param>
    internal static ResourceCommandState IsEnabled(HealthStatus? health) =>
        health == HealthStatus.Healthy ? ResourceCommandState.Enabled : ResourceCommandState.Disabled;
}
