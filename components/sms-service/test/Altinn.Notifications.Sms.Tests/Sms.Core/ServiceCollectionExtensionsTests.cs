using Altinn.Notifications.Sms.Core.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Tests.Sms.Core;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCoreServices_KafkaSettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddCoreServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
        Assert.StartsWith("Required Kafka settings is missing from application configuration", exception.Message);
    }

    [Fact]
    public void AddCoreServices_KafkaSettingsPresent_NoException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "altinn.notifications.sms.status.updated",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddCoreServices(config));

        // Assert
        Assert.Null(exception);
    }
}
