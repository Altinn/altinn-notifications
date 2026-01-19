using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Tools.RetryDeadDeliveryReports;
using Altinn.Notifications.Tools.RetryDeadDeliveryReports.EventGrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

[assembly: ExcludeFromCodeCoverage]

var builder = Host.CreateApplicationBuilder(args);

ConfigurationUtil.ConfigureServices(builder);

var host = builder.Build();

await ProcessDeadDeliveryReportsAsync(host);

static async Task ProcessDeadDeliveryReportsAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IDeadDeliveryReportRepository>();
    var eventGridClient = scope.ServiceProvider.GetRequiredService<IEventGridClient>();
    var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

    try
    {
        const int fromId = 20000;
        const int toId = 50000;

        var operationResults = await Util.GetAndMapDeadDeliveryReports(
            repository,
            fromId,
            toId,
            DeliveryReportChannel.AzureCommunicationServices,
            CancellationToken.None);

        await EventGridUtil.ProcessAndPostEventsAsync(operationResults, dataSource, eventGridClient);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
}
