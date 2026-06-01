using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Telemetry;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.Integrations.Wolverine.Commands;
using Altinn.Notifications.Integrations.Wolverine.Policies;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
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
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="hostEnvironment">The host environment (used for dev/prod ASB emulator detection).</param>
    public static void AddWolverineServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        IConfigurationSection wolverineSection = configuration.GetSection(nameof(WolverineSettings));
        WolverineSettings wolverineSettings =
            wolverineSection.Get<WolverineSettings>() ?? throw new ArgumentNullException(nameof(configuration), "Required WolverineSettings is missing from application configuration");

        services
            .AddSingleton(wolverineSettings)
            .AddSingleton<DeliveryReportMetrics>()
            .Configure<WolverineSettings>(wolverineSection);

        services.AddWolverine(opts =>
        {
            opts.ConfigureNotificationsDefaults(hostEnvironment, wolverineSettings.ServiceBusConnectionString);

            opts.Policies.AllSenders(x => x.SendInline());
            opts.Policies.AllListeners(x => x.ProcessInline());

            // Listeners
            AddEmailSendResultListener(wolverineSettings, opts);
            AddSmsSendResultListener(wolverineSettings, opts);
            AddSmsDeliveryReportListener(wolverineSettings, opts);
            AddEmailDeliveryReportListener(wolverineSettings, opts);
            AddEmailServiceRateLimitListener(wolverineSettings, opts);
            AddPastDueOrderListener(wolverineSettings, opts);

            // Publishers
            AddSendSmsPublisher(services, wolverineSettings, opts);
            AddSendEmailPublisher(services, wolverineSettings, opts);
            AddPastDueOrderPublisher(services, wolverineSettings, opts);
        });
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email send result queue,
    /// enabling the API to consume <see cref="EmailSendResultCommand"/> messages
    /// published by the email service.
    /// </summary>
    private static void AddEmailSendResultListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.EmailSendResultListenerCount <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailSendResultListenerCount)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendResultQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailSendResultQueueName)} must be configured.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailSendResultQueueName)
                        .ListenerCount(wolverineSettings.EmailSendResultListenerCount);

        wolverineOptions.Policies.Add(new EmailSendResultHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus SMS send result queue,
    /// enabling the API to consume <see cref="SmsSendResultCommand"/> messages
    /// published by the SMS service.
    /// </summary>
    private static void AddSmsSendResultListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.SmsSendResultListenerCount <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SmsSendResultListenerCount)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SmsSendResultQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SmsSendResultQueueName)} must be configured.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.SmsSendResultQueueName)
                        .ListenerCount(wolverineSettings.SmsSendResultListenerCount);

        wolverineOptions.Policies.Add(new SmsSendResultHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email delivery report queue.
    /// Uses <see cref="EventGridEnvelopeMapper"/> to interop with Event Grid message format.
    /// </summary>
    private static void AddEmailDeliveryReportListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.EmailDeliveryReportListenerCount <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailDeliveryReportListenerCount)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailDeliveryReportQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailDeliveryReportQueueName)} must be configured.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailDeliveryReportQueueName)
                        .InteropWith(new EventGridEnvelopeMapper())
                        .ListenerCount(wolverineSettings.EmailDeliveryReportListenerCount);

        wolverineOptions.Policies.Add(new EmailDeliveryReportHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus SMS delivery report queue.
    /// </summary>
    private static void AddSmsDeliveryReportListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.SmsDeliveryReportListenerCount <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SmsDeliveryReportListenerCount)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SmsDeliveryReportQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SmsDeliveryReportQueueName)} must be configured.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.SmsDeliveryReportQueueName)
                        .ListenerCount(wolverineSettings.SmsDeliveryReportListenerCount);

        wolverineOptions.Policies.Add(new SmsDeliveryReportHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus service update queue.
    /// Published by the email service when Azure Communication Services returns HTTP 429.
    /// </summary>
    private static void AddEmailServiceRateLimitListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.EmailServiceRateLimitListenerCount <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailServiceRateLimitListenerCount)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailServiceRateLimitQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailServiceRateLimitQueueName)} must be configured.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailServiceRateLimitQueueName)
                        .ListenerCount(wolverineSettings.EmailServiceRateLimitListenerCount);

        wolverineOptions.Policies.Add(new EmailServiceRateLimitHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Configures Wolverine to publish <see cref="SendEmailCommand"/> messages
    /// to the Azure Service Bus email send queue and registers
    /// <see cref="EmailCommandPublisher"/> as the <see cref="IEmailCommandPublisher"/> implementation.
    /// </summary>
    private static void AddSendEmailPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.EmailPublishConcurrency <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailPublishConcurrency)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailSendQueueName)} must be configured.");
        }

        wolverineOptions.PublishMessage<SendEmailCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.EmailSendQueueName);

        services.AddSingleton<IEmailCommandPublisher, EmailCommandPublisher>();
    }

    /// <summary>
    /// Configures Wolverine to publish <see cref="SendSmsCommand"/> messages
    /// to the Azure Service Bus SMS send queue and registers
    /// <see cref="SendSmsCommandPublisher"/> as the <see cref="ISendSmsPublisher"/> implementation.
    /// </summary>
    private static void AddSendSmsPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.SmsPublishConcurrency <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SmsPublishConcurrency)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SendSmsQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SendSmsQueueName)} must be configured.");
        }

        services.AddSingleton<ISendSmsPublisher, SendSmsCommandPublisher>();

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
        if (wolverineSettings.PastDueOrdersListenerCount <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.PastDueOrdersListenerCount)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.PastDueOrdersQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.PastDueOrdersQueueName)} must be configured.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.PastDueOrdersQueueName)
                        .ListenerCount(wolverineSettings.PastDueOrdersListenerCount);

        wolverineOptions.Policies.Add(new ProcessPastDueOrderHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Configures Wolverine to publish <see cref="ProcessPastDueOrderCommand"/> messages
    /// to the Azure Service Bus past-due orders queue and registers
    /// <see cref="PastDueOrderPublisher"/> as the <see cref="IPastDueOrderPublisher"/> implementation.
    /// </summary>
    private static void AddPastDueOrderPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.PastDueOrdersPublishConcurrency <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.PastDueOrdersPublishConcurrency)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.PastDueOrdersQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.PastDueOrdersQueueName)} must be configured.");
        }

        services.AddSingleton<IPastDueOrderPublisher, PastDueOrderPublisher>();

        wolverineOptions.PublishMessage<ProcessPastDueOrderCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.PastDueOrdersQueueName);
    }
}
