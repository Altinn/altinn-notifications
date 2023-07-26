using System;

using Altinn.Notifications.Persistence.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Persistence.TestingExtensions;

public class WebApplicationExtensions
{

    [Fact]
    public void SetUpPostgreSql_PostgreSettings_ThrowsException()
    {
        Environment.SetEnvironmentVariable("PostgreSQLSettings_EnableDBConnection", "true");

        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables();

        var config = builder.Build();

        var app = WebApplication.CreateBuilder()
                         .Build();

        Assert.Throws<ArgumentNullException>(() => app.SetUpPostgreSql(true, config));
    }
}