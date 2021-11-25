using Altinn.Notifications.Configuration;

using Npgsql.Logging;

using Yuniql.AspNetCore;
using Yuniql.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Trace, true, true);

ConsoleTraceService traceService = new ConsoleTraceService { IsDebugEnabled = true };

string connectionString = string.Format(
    builder.Configuration.GetValue<string>("PostgreSQLSettings:AdminConnectionString"),
    builder.Configuration.GetValue<string>("PostgreSQLSettings:EventsDbAdminPwd"));

app.UseYuniql(
    new PostgreSqlDataService(traceService),
    new PostgreSqlBulkImportService(traceService),
    traceService,
    new Yuniql.AspNetCore.Configuration
    {
        Workspace = Path.Combine(Environment.CurrentDirectory, builder.Configuration.GetValue<string>("PostgreSQLSettings:WorkspacePath")),
        ConnectionString = connectionString,
        IsAutoCreateDatabase = false,
        IsDebug = true
    });

app.UseAuthorization();

app.MapControllers();

app.Run();
