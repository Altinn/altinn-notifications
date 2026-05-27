using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Clients;
using Altinn.Notifications.Email.Integrations.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// This class is responsible for holding extension methods for program startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add necessary integration services and configuration to the service collection.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="config">The application configuration.</param>
    /// <returns>The given service collection.</returns>
    public static IServiceCollection AddIntegrationServices(this IServiceCollection services, IConfiguration config)
    {
        CommunicationServicesSettings communicationServicesSettings = config!.GetSection(nameof(CommunicationServicesSettings)).Get<CommunicationServicesSettings>()!;

        if (communicationServicesSettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required communication services settings are missing from application configuration");
        }

        EmailServiceAdminSettings emailServiceAdminSettings = config!.GetSection(nameof(EmailServiceAdminSettings)).Get<EmailServiceAdminSettings>()!;

        if (emailServiceAdminSettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required email service admin settings are missing from application configuration");
        }

        services
            .AddSingleton<IEmailServiceClient, EmailServiceClient>()
            .AddSingleton(emailServiceAdminSettings)
            .AddSingleton(communicationServicesSettings);

        WolverineSettings wolverineSettings = config.GetSection(nameof(WolverineSettings)).Get<WolverineSettings>() ?? new WolverineSettings();

        RegisterEmailSendResultDispatcher(services, wolverineSettings);
        RegisterEmailStatusCheckDispatcher(services, wolverineSettings);
        RegisterEmailServiceRateLimitDispatcher(services, wolverineSettings);

        return services;
    }

    /// <summary>
    /// Registers the appropriate <see cref="IEmailStatusCheckDispatcher"/> implementation
    /// based on Wolverine configuration, selecting either the ASB or Kafka transport path.
    /// </summary>
    private static void RegisterEmailStatusCheckDispatcher(IServiceCollection services, WolverineSettings wolverineSettings)
    {
        if (wolverineSettings.EnableWolverine && wolverineSettings.EnableEmailStatusCheckPublisher)
        {
            if (!wolverineSettings.EnableEmailStatusCheckListener)
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.EnableEmailStatusCheckListener)} must be enabled when {nameof(WolverineSettings.EnableEmailStatusCheckPublisher)} is enabled.");
            }

            if (string.IsNullOrWhiteSpace(wolverineSettings.EmailStatusCheckQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.EmailStatusCheckQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailStatusCheckPublisher)} is enabled.");
            }

            services.AddSingleton<IEmailStatusCheckDispatcher, EmailStatusCheckPublisher>();
        }
    }

    /// <summary>
    /// Registers the appropriate <see cref="IEmailSendResultDispatcher"/> implementation
    /// based on Wolverine configuration, selecting either the ASB or Kafka transport path.
    /// </summary>
    private static void RegisterEmailSendResultDispatcher(IServiceCollection services, WolverineSettings wolverineSettings)
    {
        if (wolverineSettings.EnableWolverine && wolverineSettings.EnableEmailSendResultPublisher)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendResultQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.EmailSendResultQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailSendResultPublisher)} is enabled.");
            }

            services.AddSingleton<IEmailSendResultDispatcher, EmailSendResultPublisher>();
        }
    }

    /// <summary>
    /// Registers the appropriate <see cref="IEmailServiceRateLimitDispatcher"/> implementation
    /// based on Wolverine configuration, selecting either the ASB or Kafka transport path.
    /// </summary>
    private static void RegisterEmailServiceRateLimitDispatcher(IServiceCollection services, WolverineSettings wolverineSettings)
    {
        if (wolverineSettings.EnableWolverine && wolverineSettings.EnableEmailServiceRateLimitPublisher)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.EmailServiceRateLimitQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.EmailServiceRateLimitQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailServiceRateLimitPublisher)} is enabled.");
            }

            services.AddSingleton<IEmailServiceRateLimitDispatcher, EmailServiceRateLimitPublisher>();
        }
    }
}
