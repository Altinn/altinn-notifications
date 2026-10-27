using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure
{
    public class IntegrationTestEmailContainersFixture : IntegrationTestContainersFixture
    {
        /// <summary>
        /// Gets the shared email ASB test host instance.
        /// </summary>
        public IntegrationTestWebApplicationFactory WebHost { get; private set; } = null!;

        /// <summary>
        /// Gets the resolved Wolverine settings from the shared host.
        /// </summary>
        internal WolverineSettings? WolverineSettings => WebHost.WolverineSettings;

        /// <summary>
        /// Restores default service implementations.
        /// </summary>
        internal void ResetInstalledMocks()
        {
            WebHost.ResetInstalledMocks();
        }

        /// <summary>
        /// Installs a service override for subsequent test operations.
        /// </summary>
        /// <typeparam name="TService">Service contract type.</typeparam>
        /// <param name="service">Service instance to install.</param>
        internal void InstallService<TService>(TService service)
            where TService : class
        {
            WebHost.InstallService(service);
        }

        /// <summary>
        /// Drains both the main queue and its dead-letter queue.
        /// </summary>
        /// <param name="queueName">Queue name to drain.</param>
        internal async Task DrainQueueAsync(string queueName)
        {
            await ServiceBusTestUtils.DrainQueueAsync(ServiceBusConnectionString, queueName);
            await ServiceBusTestUtils.DrainQueueAsync(ServiceBusConnectionString, $"{queueName}/$deadletterqueue");
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();

            // Needed if IsLocalASBDisabled in IntegrationTestContainersFixture is set to true
            ServiceBusConnectionString = string.IsNullOrEmpty(ServiceBusConnectionString)
                ? $"Endpoint=sb://127.0.0.1;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
                : ServiceBusConnectionString;

            WebHost = new IntegrationTestWebApplicationFactory(this)
                .Initialize();

            ResetInstalledMocks();
        }

        public override async ValueTask DisposeAsync()
        {
            try
            {
                if (WebHost != null)
                {
                    await WebHost.DisposeAsync();
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
