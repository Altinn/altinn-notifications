using System;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Notifications.Shared.Extensions;

/// <summary>
/// Provides extension methods for configuring <see cref="WolverineOptions"/> with default settings.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WolverineOptionsExtensions
{
    /// <summary>
    /// Configures the <see cref="WolverineOptions"/> instance with default settings,
    /// including Azure Service Bus configuration and environment-specific options.
    /// </summary>
    /// <param name="opts">The <see cref="WolverineOptions"/> to configure.</param>
    /// <param name="env">The host environment.</param>
    /// <param name="azureServiceBusConnectionString">The Azure Service Bus connection string.</param>
    /// <returns>The configured <see cref="WolverineOptions"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="opts"/> or <paramref name="env"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="azureServiceBusConnectionString"/> is null or whitespace.</exception>
    public static WolverineOptions ConfigureNotificationsDefaults(
        this WolverineOptions opts,
        IHostEnvironment env,
        string azureServiceBusConnectionString)
    {
        ArgumentNullException.ThrowIfNull(opts);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentException.ThrowIfNullOrWhiteSpace(azureServiceBusConnectionString);

        opts.Policies.DisableConventionalLocalRouting();
        opts.EnableAutomaticFailureAcks = false;
        opts.EnableRemoteInvocation = false;
        opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;

        // In development (which includes the ASB emulator used in integration tests),
        // shorten TryTimeout so the AMQP drain on shutdown fails in ~3s instead of
        // the default 60s. On shutdown, ServiceBusProcessor sends an AMQP drain frame
        // (FLOW with drain=true) whenever link.LinkCredit > 0 — which is always the case
        // when a processor is idle waiting for the next message. The emulator never
        // acknowledges this frame, so without a short TryTimeout every factory disposal
        // hangs for a full minute.
        var azureBusConfig = env.IsDevelopment()
            ? opts.UseAzureServiceBus(azureServiceBusConnectionString, o => o.RetryOptions.TryTimeout = TimeSpan.FromSeconds(3))
            : opts.UseAzureServiceBus(azureServiceBusConnectionString);

        if (env.IsDevelopment())
        {
            // Disable Wolverine's internal system queues for the Azure Service Bus Emulator.
            // These are temporary queues used for inter-node coordination, leader election, and
            // worker distribution. The emulator doesn't support the Management API needed to
            // create these queues dynamically.
            // Note: Azure Service Bus native dead-letter queues (accessed via /$deadletterqueue)
            // still work in the emulator.
            azureBusConfig.SystemQueuesAreEnabled(false);

            // Auto-purge application queues on startup for clean development sessions.
            azureBusConfig.AutoPurgeOnStartup();
        }
        else
        {
            // In production, enable auto-provisioning which creates all necessary queues
            // automatically, including Wolverine's internal system queues.
            azureBusConfig.AutoProvision();
        }

        return opts;
    }
}
