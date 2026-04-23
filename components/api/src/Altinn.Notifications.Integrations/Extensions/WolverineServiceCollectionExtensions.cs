using System.Diagnostics.CodeAnalysis;
using Altinn.Notifications.Core.Integrations;

using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.Integrations.Wolverine.Commands;
using Altinn.Notifications.Integrations.Wolverine.Policies;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Notifications.Integrations.Extensions;

/// <summary>
/// Extension methods for registering Wolverine with Azure Service Bus in the Notifications API.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WolverineServiceCollectionExtensions
{
    /// <summary>
    /// Adds Wolverine with Azure Service Bus transport.
    /// Each listener/publisher queue is individually enabled via its own flag.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="hostEnvironment">The host environment (used for dev/prod ASB emulator detection).</param>
    public static void AddWolverineServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        IConfigurationSection wolverineSection = configuration.GetSection(nameof(WolverineSettings));
        WolverineSettings wolverineSettings = wolverineSection.Get<WolverineSettings>() ?? new WolverineSettings();
        if (!wolverineSettings.EnableWolverine)
        {
            return;
        }

        services.Configure<WolverineSettings>(wolverineSection);

        services.AddWolverine(opts =>
        {
            opts.ConfigureNotificationsDefaults(hostEnvironment, wolverineSettings.ServiceBusConnectionString);

            opts.Policies.AllSenders(x => x.SendInline());
            opts.Policies.AllListeners(x => x.ProcessInline());

            // Listeners
            AddEmailSendResultListener(wolverineSettings, opts);
            AddSmsDeliveryReportListener(wolverineSettings, opts);
            AddEmailDeliveryReportListener(wolverineSettings, opts);
            AddEmailServiceRateLimitListener(wolverineSettings, opts);
            AddPastDueOrderListener(wolverineSettings, opts);

            // Publishers
            AddSendSmsPublisher(wolverineSettings, opts);
            AddSendEmailPublisher(wolverineSettings, opts);
            AddPastDueOrderPublisher(wolverineSettings, opts);
        });
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email send result queue,
    /// enabling the API to consume <see cref="EmailSendResultCommand"/> messages
    /// published by the email service.
    /// </summary>
    private static void AddEmailSendResultListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableEmailSendResultListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendResultQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailSendResultQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailSendResultListener)} is enabled.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailSendResultQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);

        wolverineOptions.Policies.Add(new EmailSendResultHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email delivery report queue.
    /// Uses <see cref="EventGridEnvelopeMapper"/> to interop with Event Grid message format.
    /// </summary>
    private static void AddEmailDeliveryReportListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableEmailDeliveryReportListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailDeliveryReportQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailDeliveryReportQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailDeliveryReportListener)} is enabled.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailDeliveryReportQueueName)
                        .InteropWith(new EventGridEnvelopeMapper())
                        .ListenerCount(wolverineSettings.ListenerCount);

        wolverineOptions.Policies.Add(new EmailDeliveryReportHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus SMS delivery report queue.
    /// </summary>
    private static void AddSmsDeliveryReportListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSmsDeliveryReportListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SmsDeliveryReportQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SmsDeliveryReportQueueName)} must be configured when {nameof(WolverineSettings.EnableSmsDeliveryReportListener)} is enabled.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.SmsDeliveryReportQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);

        wolverineOptions.Policies.Add(new SmsDeliveryReportHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus service update queue.
    /// Published by the email service when Azure Communication Services returns HTTP 429.
    /// </summary>
    private static void AddEmailServiceRateLimitListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableEmailServiceRateLimitListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailServiceRateLimitQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailServiceRateLimitQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailServiceRateLimitListener)} is enabled.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailServiceRateLimitQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);

        wolverineOptions.Policies.Add(new EmailServiceRateLimitHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="SendEmailCommand"/>,
    /// routing outbound commands to the Azure Service Bus email send queue.
    /// Only active when <see cref="WolverineSettings.EnableSendEmailPublisher"/> is <c>true</c>.
    /// The <see cref="IEmailCommandPublisher"/> DI registration is handled separately.
    /// </summary>
    private static void AddSendEmailPublisher(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSendEmailPublisher)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailSendQueueName)} must be configured when {nameof(WolverineSettings.EnableSendEmailPublisher)} is enabled.");
        }

        wolverineOptions.PublishMessage<SendEmailCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.EmailSendQueueName);
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="SendSmsCommand"/>,
    /// routing outbound commands to the Azure Service Bus SMS send queue.
    /// Only active when <see cref="WolverineSettings.EnableSendSmsPublisher"/> is <c>true</c>.
    /// The <see cref="ISendSmsPublisher"/> DI registration is handled separately.
    /// </summary>
    private static void AddSendSmsPublisher(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSendSmsPublisher)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SendSmsQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SendSmsQueueName)} must be configured when {nameof(WolverineSettings.EnableSendSmsPublisher)} is enabled.");
        }

        wolverineOptions.PublishMessage<SendSmsCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.SendSmsQueueName);
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus past-due orders queue,
    /// enabling the API to consume <see cref="ProcessPastDueOrderCommand"/> messages
    /// it published itself.
    /// </summary>
    private static void AddPastDueOrderListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnablePastDueOrderListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.PastDueOrdersQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.PastDueOrdersQueueName)} must be configured when {nameof(WolverineSettings.EnablePastDueOrderListener)} is enabled.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.PastDueOrdersQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);

        wolverineOptions.Policies.Add(new ProcessPastDueOrderHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="ProcessPastDueOrderCommand"/>,
    /// routing outbound commands to the Azure Service Bus past-due orders queue.
    /// Only active when <see cref="WolverineSettings.EnablePastDueOrderPublisher"/> is <c>true</c>.
    /// The <see cref="IPastDueOrderPublisher"/> DI registration is handled separately.
    /// </summary>
    private static void AddPastDueOrderPublisher(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnablePastDueOrderPublisher)
        {
            return;
        }

        if (!wolverineSettings.EnablePastDueOrderListener)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EnablePastDueOrderListener)} must be enabled when {nameof(WolverineSettings.EnablePastDueOrderPublisher)} is enabled.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.PastDueOrdersQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.PastDueOrdersQueueName)} must be configured when {nameof(WolverineSettings.EnablePastDueOrderPublisher)} is enabled.");
        }

        wolverineOptions.PublishMessage<ProcessPastDueOrderCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.PastDueOrdersQueueName);
    }
}
