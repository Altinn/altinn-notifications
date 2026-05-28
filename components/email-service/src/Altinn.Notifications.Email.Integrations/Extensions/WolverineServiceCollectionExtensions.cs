using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Integrations.Publishers;
using Altinn.Notifications.Email.Integrations.Wolverine.Policies;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Notifications.Email.Integrations.Extensions;

/// <summary>
/// Extension methods for registering Wolverine with Azure Service Bus in the email service.
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
        WolverineSettings wolverineSettings = wolverineSection.Get<WolverineSettings>() ?? new WolverineSettings();

        services.Configure<WolverineSettings>(wolverineSection);

        services.AddWolverine(opts =>
        {
            opts.ConfigureNotificationsDefaults(hostEnvironment, wolverineSettings.ServiceBusConnectionString);

            opts.Policies.AllSenders(x => x.SendInline());
            opts.Policies.AllListeners(x => x.ProcessInline());

            // Listeners
            AddEmailSendQueueListener(wolverineSettings, opts);
            AddEmailStatusCheckListener(wolverineSettings, opts);

            // Publishers
            AddEmailSendResultPublisher(services, wolverineSettings, opts);
            AddEmailStatusCheckPublisher(services, wolverineSettings, opts);
            AddEmailServiceRateLimitPublisher(services, wolverineSettings, opts);
        });
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email send queue, enabling
    /// the email service to consume <see cref="SendEmailCommand"/> messages.
    /// </summary>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
    private static void AddEmailSendQueueListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.EmailSendListenerCount <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailSendListenerCount)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailSendQueueName)} must be configured.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailSendQueueName)
                        .ListenerCount(wolverineSettings.EmailSendListenerCount);

        wolverineOptions.Policies.Add(new SendEmailCommandHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="EmailSendResultCommand"/>, routing
    /// outbound commands to the Azure Service Bus email sending status queue consumed by the API.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
    private static void AddEmailSendResultPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendResultQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailSendResultQueueName)} must be configured.");
        }

        services.AddSingleton<IEmailSendResultDispatcher, EmailSendResultPublisher>();

        wolverineOptions.PublishMessage<EmailSendResultCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.EmailSendResultQueueName);
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email status check queue, enabling
    /// the email service to consume <see cref="CheckEmailSendStatusCommand"/> messages and
    /// poll Azure Communication Services (ACS) for delivery status.
    /// </summary>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
    private static void AddEmailStatusCheckListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (wolverineSettings.EmailStatusCheckListenerCount <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailStatusCheckListenerCount)} must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailStatusCheckQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailStatusCheckQueueName)} must be configured.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailStatusCheckQueueName)
                        .ListenerCount(wolverineSettings.EmailStatusCheckListenerCount);

        wolverineOptions.Policies.Add(new CheckEmailSendStatusHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="CheckEmailSendStatusCommand"/>, routing
    /// outbound commands to the Azure Service Bus email status check queue.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
    private static void AddEmailStatusCheckPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailStatusCheckQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailStatusCheckQueueName)} must be configured.");
        }

        services.AddSingleton<IEmailStatusCheckDispatcher, EmailStatusCheckPublisher>();

        wolverineOptions.PublishMessage<CheckEmailSendStatusCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.EmailStatusCheckQueueName);
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="EmailServiceRateLimitCommand"/>, routing
    /// outbound commands to the Azure Service Bus service update queue consumed by the API.
    /// </summary>
    private static void AddEmailServiceRateLimitPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailServiceRateLimitQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailServiceRateLimitQueueName)} must be configured.");
        }

        services.AddSingleton<IEmailServiceRateLimitDispatcher, EmailServiceRateLimitPublisher>();

        wolverineOptions.PublishMessage<EmailServiceRateLimitCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.EmailServiceRateLimitQueueName);
    }
}
