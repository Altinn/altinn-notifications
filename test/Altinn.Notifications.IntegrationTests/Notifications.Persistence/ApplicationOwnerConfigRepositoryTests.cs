using Altinn.Notifications.Core.Models;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.Extensions.Configuration;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class ApplicationOwnerConfigRepositoryTests : IAsyncLifetime
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly string _applicationOwnerId = Guid.NewGuid().ToString();

    public ApplicationOwnerConfigRepositoryTests()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        PostgreSqlSettings? settings = configuration.GetSection("PostgreSqlSettings").Get<PostgreSqlSettings>();

        string connectionString = string.Format(settings!.ConnectionString, settings.NotificationsDbPwd);

        _dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await PostgreUtil.RunSql($"DELETE FROM notifications.applicationownerconfig WHERE orgid='{_applicationOwnerId}'");
    }

    [Fact]
    public async Task GetTest_Org_does_not_exist_Returns_null()
    {
        // Arrange
        var target = new ApplicationOwnerConfigRepository(_dataSource);

        // Act
        ApplicationOwnerConfig? readAppOwnerConfig = await target.GetApplicationOwnerConfig("notmeoryou");

        // Assert
        Assert.Null(readAppOwnerConfig);
    }

    [Fact]
    public async Task WriteTest_and_GetTest_Written_Match_Read()
    {
        // Arrange
        var target = new ApplicationOwnerConfigRepository(_dataSource);

        ApplicationOwnerConfig writtenAppOwnerConfig = new(_applicationOwnerId);
        writtenAppOwnerConfig.EmailAddresses.Add("noreply@altinn.cloud");
        writtenAppOwnerConfig.EmailAddresses.Add("doreply@altinn.cloud");

        // Act
        await target.WriteApplicationOwnerConfig(writtenAppOwnerConfig);
        ApplicationOwnerConfig? readAppOwnerConfig = await target.GetApplicationOwnerConfig(_applicationOwnerId);

        // Assert
        Assert.Equivalent(writtenAppOwnerConfig, readAppOwnerConfig);
    }

    [Fact]
    public async Task WriteTest_Write_twice_with_different_values_Get_returns_last_write()
    {
        // Arrange
        var target = new ApplicationOwnerConfigRepository(_dataSource);

        ApplicationOwnerConfig firstWrite = new(_applicationOwnerId);
        firstWrite.EmailAddresses.Add("noreply@altinn.cloud");
        firstWrite.EmailAddresses.Add("doreply@altinn.cloud");

        ApplicationOwnerConfig secondWrite = new(_applicationOwnerId);
        secondWrite.EmailAddresses.Add("baretullfra@altinn.cloud");
        secondWrite.SmsNames.Add("98989891");
        secondWrite.SmsNames.Add("53819");

        // Act
        await target.WriteApplicationOwnerConfig(firstWrite); 
        await target.WriteApplicationOwnerConfig(secondWrite);
        ApplicationOwnerConfig? readAppOwnerConfig = await target.GetApplicationOwnerConfig(_applicationOwnerId);

        // Assert
        Assert.Equivalent(secondWrite, readAppOwnerConfig);
    }
}
