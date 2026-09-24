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

        public IntegrationTestWebApplicationFactory WebHost { get; private set; } = null!;

        internal DelegatingEmailServiceClient EmailServiceClient { get; }

        internal DelegatingSendingService SendingService { get; } = new DelegatingSendingService(new AlwaysSucceedSendingService());

        internal WolverineSettings? WolverineSettings => WebHost.WolverineSettings;

        public IntegrationTestEmailContainersFixture()
        {
            _defaultEmailServiceClient = new UnconfiguredEmailServiceClient();
            EmailServiceClient = new DelegatingEmailServiceClient(_defaultEmailServiceClient);
        }

        internal void InstallEmailServiceClient(IEmailServiceClient emailServiceClient)
        {
            ArgumentNullException.ThrowIfNull(emailServiceClient);
            EmailServiceClient.SetCurrentClient(emailServiceClient);
        }

        internal void InstallSendingService(ISendingService sendingService)
        {
            ArgumentNullException.ThrowIfNull(sendingService);
            SendingService.SetCurrentService(sendingService);
        }

        internal void ResetInstalledMocks()
        {
            EmailServiceClient.SetCurrentClient(_defaultEmailServiceClient);
            SendingService.SetCurrentService(_defaultSendingService);
        }

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
            await WebHost.DisposeAsync();
            if (UseBaseBehaviour)
            {
                return;
            }

            await base.DisposeAsync();
        }
    }
}
