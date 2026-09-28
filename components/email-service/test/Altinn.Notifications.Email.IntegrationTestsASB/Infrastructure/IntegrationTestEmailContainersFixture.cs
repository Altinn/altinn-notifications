using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.IntegrationTestsASB.Tests;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure
{
    public class IntegrationTestEmailContainersFixture : IntegrationTestContainersFixture
    {
        private static readonly bool UseBaseBehaviour = false;
        private ISendingService _defaultSendingService = null!;
        private IEmailServiceClient _defaultEmailServiceClient = null!;

        /// <summary>
        /// Gets the shared email ASB test host instance.
        /// </summary>
        public IntegrationTestWebApplicationFactory WebHost { get; private set; } = null!;

        /// <summary>
        /// Gets the delegating email service client wrapper used for per-test overrides.
        /// </summary>
        internal DelegatingEmailServiceClient EmailServiceClient { get; }

        /// <summary>
        /// Gets the delegating sending service wrapper used for per-test overrides.
        /// </summary>
        internal DelegatingSendingService SendingService { get; } = new DelegatingSendingService(new AlwaysSucceedSendingService());

        /// <summary>
        /// Gets the resolved Wolverine settings from the shared host.
        /// </summary>
        internal WolverineSettings? WolverineSettings => WebHost.WolverineSettings;

        public IntegrationTestEmailContainersFixture()
        {
            _defaultEmailServiceClient = new UnconfiguredEmailServiceClient();
            EmailServiceClient = new DelegatingEmailServiceClient(_defaultEmailServiceClient);
        }

        /// <summary>
        /// Installs an email service client override for subsequent test operations.
        /// </summary>
        /// <param name="emailServiceClient">Email service client to install.</param>
        internal void InstallEmailServiceClient(IEmailServiceClient emailServiceClient)
        {
            ArgumentNullException.ThrowIfNull(emailServiceClient);
            EmailServiceClient.SetCurrentClient(emailServiceClient);
        }

        /// <summary>
        /// Installs a sending service override for subsequent test operations.
        /// </summary>
        /// <param name="sendingService">Sending service to install.</param>
        internal void InstallSendingService(ISendingService sendingService)
        {
            ArgumentNullException.ThrowIfNull(sendingService);
            SendingService.SetCurrentService(sendingService);
        }

        /// <summary>
        /// Restores default email service and sending service implementations.
        /// </summary>
        internal void ResetInstalledMocks()
        {
            EmailServiceClient.SetCurrentClient(_defaultEmailServiceClient);
            SendingService.SetCurrentService(_defaultSendingService);
        }

        /// <summary>
        /// Drains both the main queue and its dead-letter queue.
        /// </summary>
        /// <param name="queueName">Queue name to drain.</param>
        internal async Task DrainQueue(string queueName)
        {
            await ServiceBusTestUtils.DrainQueueAsync(ServiceBusConnectionString, queueName);
            await ServiceBusTestUtils.DrainQueueAsync(ServiceBusConnectionString, $"{queueName}/$deadletterqueue");
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            if (UseBaseBehaviour)
            {
                return;
            }

            // Needed if IsLocalASBDisabled in IntegrationTestContainersFixture is set to true
            ServiceBusConnectionString = string.IsNullOrEmpty(ServiceBusConnectionString)
                ? $"Endpoint=sb://127.0.0.1;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
                : ServiceBusConnectionString;

            WebHost = new IntegrationTestWebApplicationFactory(this)
                .ReplaceService<IEmailServiceClient>(_ => EmailServiceClient)
                .ReplaceService<ISendingService>(_ => SendingService)
                .Initialize();

            using var scope = WebHost.Services.CreateScope();
            var serviceProvider = scope.ServiceProvider;
            _defaultSendingService = new Core.Sending.SendingService(
                serviceProvider.GetRequiredService<ILogger<Core.Sending.SendingService>>(),
                EmailServiceClient,
                serviceProvider.GetRequiredService<IEmailStatusCheckDispatcher>(),
                serviceProvider.GetRequiredService<IEmailSendResultDispatcher>(),
                serviceProvider.GetRequiredService<IEmailServiceRateLimitDispatcher>());

            ResetInstalledMocks();
        }

        public override async ValueTask DisposeAsync()
        {
            try
            {
                await WebHost.DisposeAsync();
                if (UseBaseBehaviour)
                {
                    return;
                }

                await base.DisposeAsync();
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }
    }
}
