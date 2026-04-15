using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Integrations.Consumers;
using Altinn.Notifications.Email.Integrations.Publishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntegrationServices_KafkaSettingsMissing_ThrowsException()
    {
        // Arrange
        string expectedExceptionMessage = "Required Kafka settings is missing from application configuration (Parameter 'config')";

        Environment.SetEnvironmentVariable("KafkaSettings__ConsumerGroupId", null);
        Environment.SetEnvironmentVariable("CommunicationServicesSettings__Connectionstring", "value");

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddIntegrationServicess_NotificationOrderConfigMissing_ThrowsException()
    {
        // Arrange
        string expectedExceptionMessage = "Required communication services settings is missing from application configuration (Parameter 'config')";

        Environment.SetEnvironmentVariable("KafkaSettings__ConsumerGroupId", "value");
        Environment.SetEnvironmentVariable("CommunicationServicesSettings__Connectionstring", null);

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_EmailServiceAdminSettingsMissing_ThrowsException()
    {
        // Arrange
        string expectedExceptionMessage = "Required email service admin settings is missing from application configuration (Parameter 'config')";

        Environment.SetEnvironmentVariable("KafkaSettings__ConsumerGroupId", "value");
        Environment.SetEnvironmentVariable("CommunicationServicesSettings__Connectionstring", "value");
        Environment.SetEnvironmentVariable("EmailServiceAdminSettings__IntermittentErrorDelay", null);

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddKafkaHealthChecks_KafkaSettingsMissing_ThrowsException()
    {
        Environment.SetEnvironmentVariable("KafkaSettings", null);

        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddIntegrationHealthChecks(config));
    }

    [Fact]
    public void AddIntegrationServices_WolverineAndStatusCheckEnabled_RegistersWolverineDispatcher()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "email-status-check-queue",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddIntegrationServices(config);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailStatusCheckDispatcher));

        Assert.NotNull(descriptor);
        Assert.Equal(typeof(EmailStatusCheckPublisher), descriptor.ImplementationType);
        Assert.Contains(services, d => d.ImplementationType == typeof(EmailSendingAcceptedConsumer)); // The Kafka consumer should be registered alongside the publisher when Wolverine is enabled
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AddIntegrationServices_WolverineNotFullyEnabled_RegistersKafkaDispatcher(bool enableWolverine, bool enableStatusCheck)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = enableWolverine.ToString(),
                ["WolverineSettings:EnableEmailStatusCheckListener"] = enableStatusCheck.ToString(),
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddIntegrationServices(config);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailStatusCheckDispatcher));

        Assert.NotNull(descriptor);
        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Contains(services, d => d.ImplementationType == typeof(EmailSendingAcceptedConsumer));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIntegrationServices_WolverineEnabledButQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Both feature flags are on, but EmailStatusCheckQueueName is blank — fail fast rather than
        // silently falling back to Kafka, to surface misconfiguration at startup.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",
                ["WolverineSettings:EmailStatusCheckQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        Assert.Equal("EmailStatusCheckQueueName must be configured when EnableEmailStatusCheckListener is enabled.", exception.Message);
    }
}
