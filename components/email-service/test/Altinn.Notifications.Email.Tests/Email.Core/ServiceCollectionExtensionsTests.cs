using Altinn.Notifications.Email.Core.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCoreServices_KafkaTopicSettingsMissing_ThrowsException()
    {
        string expectedExceptionMessage = "Required Kafka topic settings is missing from application configuration (Parameter 'config')";

        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection();
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddCoreServices(config));
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }
}
