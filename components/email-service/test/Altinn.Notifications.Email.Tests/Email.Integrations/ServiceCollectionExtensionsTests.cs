using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Integrations.Consumers;
using Altinn.Notifications.Email.Integrations.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
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
    public void AddIntegrationServices_WolverineAndStatusCheckEnabled_RegistersWolverineDispatcher()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "test-email-status-updated-topic",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "test-altinn-service-update-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "email-status-check-queue",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddIntegrationServices(config);
        services.AddSingleton(new Mock<IDateTimeService>().Object);

        // Assert
        var provider = services.BuildServiceProvider();
        var statusDispatcher = provider.GetRequiredService<IEmailStatusCheckDispatcher>();
        Assert.IsType<EmailStatusCheckPublisher>(statusDispatcher);
        Assert.Single(services, d => d.ServiceType == typeof(IEmailStatusCheckDispatcher));
        Assert.Contains(services, d => d.ImplementationType == typeof(EmailSendingAcceptedConsumer)); // The Kafka consumer should be registered alongside the publisher when Wolverine is enabled
    }

    [Fact]
    public void AddIntegrationServices_WolverineEnabledButListenerDisabled_ThrowsInvalidOperationException()
    {
        // Publisher requires the listener to be active — publishing to the status check queue without a
        // listener consuming it would silently drop messages.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "test-email-status-updated-topic",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "test-altinn-service-update-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "false",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "email-status-check-queue",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        Assert.Equal("EnableEmailStatusCheckListener must be enabled when EnableEmailStatusCheckPublisher is enabled.", exception.Message);
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
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "test-email-status-updated-topic",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "test-altinn-service-update-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
                ["WolverineSettings:EmailStatusCheckQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        Assert.Equal("EmailStatusCheckQueueName must be configured when EnableEmailStatusCheckPublisher is enabled.", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_WolverineAndSendResultPublisherEnabled_RegistersEmailSendResultPublisher()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:EnableWolverine"] = "true",
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "true",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "test-altinn-service-update-topic",
                ["WolverineSettings:EmailSendResultQueueName"] = "email-send-result-queue",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        services.AddIntegrationServices(config);

        var provider = services.BuildServiceProvider();
        var sendResultDispatcher = provider.GetRequiredService<IEmailSendResultDispatcher>();
        Assert.IsType<EmailSendResultPublisher>(sendResultDispatcher);
        Assert.Single(services, d => d.ServiceType == typeof(IEmailSendResultDispatcher));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIntegrationServices_WolverineEnabledButSendResultQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Both feature flags are on, but EmailSendResultQueueName is blank — fail fast to surface
        // misconfiguration at startup rather than silently falling back to Kafka.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:EnableWolverine"] = "true",
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["WolverineSettings:EmailSendResultQueueName"] = queueName,
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "true",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        Assert.Equal("EmailSendResultQueueName must be configured when EnableEmailSendResultPublisher is enabled.", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_EmailServiceRateLimitPublisherEnabled_RegistersAsbDispatcher()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:EnableWolverine"] = "true",
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "test-email-status-updated-topic",
                ["WolverineSettings:EnableEmailServiceRateLimitPublisher"] = "true",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "altinn.platform.service.updated",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.service.ratelimit",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddIntegrationServices(config);

        // Assert
        var provider = services.BuildServiceProvider();
        var rateLimitDispatcher = provider.GetRequiredService<IEmailServiceRateLimitDispatcher>();
        Assert.IsType<EmailServiceRateLimitPublisher>(rateLimitDispatcher);
        Assert.Single(services, d => d.ServiceType == typeof(IEmailServiceRateLimitDispatcher));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void AddIntegrationServices_EmailServiceRateLimitPublisherEnabledButQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:EnableWolverine"] = "true",
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "test-email-status-updated-topic",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = queueName,
                ["WolverineSettings:EnableEmailServiceRateLimitPublisher"] = "true",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));
        Assert.Equal(
            "EmailServiceRateLimitQueueName must be configured when EnableEmailServiceRateLimitPublisher is enabled.",
            exception.Message);
    }
}
