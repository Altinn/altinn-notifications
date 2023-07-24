using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Core.Integrations.Consumers;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Persistence.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Core.Consumers;

public class PastDueOrdersConsumerTests
{
    [Fact]
    public async Task RunTask_10seconds()
    {
        var builder = new ConfigurationBuilder().AddJsonFile($"appsettings.json", optional: false);
        var config = builder.Build();

        Persistence.Configuration.PostgreSqlSettings postgresSettings = config.GetSection("PostgreSqlSettings").Get<Persistence.Configuration.PostgreSqlSettings>();

        IServiceCollection services = new ServiceCollection()
            .AddLogging();

        services.AddCoreServices(config);
        services.AddPostgresRepositories(postgresSettings);
        services.AddKafkaServices(config);

        var serviceProvider = services.BuildServiceProvider();

        var hostedServiceList = serviceProvider.GetServices<IHostedService>();
        var service = hostedServiceList.First(s => s.GetType() == typeof(PastDueOrdersConsumer));

        await service.StartAsync(CancellationToken.None);

        await Task.Delay(10000);

        await service.StopAsync(CancellationToken.None);
    }
}
