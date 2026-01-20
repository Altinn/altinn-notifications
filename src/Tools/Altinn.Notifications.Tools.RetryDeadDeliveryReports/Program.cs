using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Tools.RetryDeadDeliveryReports;
using Altinn.Notifications.Tools.RetryDeadDeliveryReports.Configuration;
using Altinn.Notifications.Tools.RetryDeadDeliveryReports.EventGrid;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

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
    var processingSettings = scope.ServiceProvider.GetRequiredService<IOptions<ProcessingSettings>>().Value;

    try
    {
        var operationResults = await Util.GetAndMapDeadDeliveryReports(
            repository,
            processingSettings.FromId,
            processingSettings.ToId,
            DeliveryReportChannel.AzureCommunicationServices,
            CancellationToken.None);

        await EventGridUtil.ProcessAndPostEventsAsync(operationResults, dataSource, eventGridClient);
    }
static async Task<int> ProcessDeadDeliveryReportsAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IDeadDeliveryReportRepository>();
    var eventGridClient = scope.ServiceProvider.GetRequiredService<IEventGridClient>();
    var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
    var processingSettings = scope.ServiceProvider.GetRequiredService<IOptions<ProcessingSettings>>().Value;

    try
    {
        var operationResults = await Util.GetAndMapDeadDeliveryReports(
            repository,
            processingSettings.FromId,
            processingSettings.ToId,
            DeliveryReportChannel.AzureCommunicationServices,
            CancellationToken.None);

        await EventGridUtil.ProcessAndPostEventsAsync(operationResults, dataSource, eventGridClient);
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return 1;
    }
}
}
