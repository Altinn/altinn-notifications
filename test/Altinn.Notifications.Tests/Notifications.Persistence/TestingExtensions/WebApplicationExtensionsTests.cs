using System;

using Altinn.Notifications.Persistence.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Persistence.TestingExtensions;

public class WebApplicationExtensions
{
    [Fact]
    public void SetUpPostgreSql_PostgreSettingsMissing_ThrowsException()
    {

        var config = new ConfigurationBuilder().Build();

        var app = WebApplication.CreateBuilder().Build();

        Assert.Throws<ArgumentNullException>(() => app.SetUpPostgreSql(true, config));
    }
}