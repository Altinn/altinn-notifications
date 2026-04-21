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
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "test-email-status-updated-topic",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "test-altinn-service-update-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
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
    public void AddIntegrationServices_WolverineNotFullyEnabled_RegistersKafkaDispatcher(bool enableWolverine, bool enableStatusCheckPublisher)
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
                ["WolverineSettings:EnableWolverine"] = enableWolverine.ToString(),
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = enableStatusCheckPublisher.ToString(),
                ["WolverineSettings:EmailStatusCheckQueueName"] = "email-status-check-queue",
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
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "test-email-status-updated-topic",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "test-altinn-service-update-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
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
                ["WolverineSettings:EmailSendResultQueueName"] = "email-send-result-queue",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        services.AddIntegrationServices(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailSendResultDispatcher));

        Assert.NotNull(descriptor);
        Assert.Equal(typeof(EmailSendResultPublisher), descriptor.ImplementationType);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AddIntegrationServices_WolverineSendResultPublisherNotFullyEnabled_RegistersKafkaProducer(bool enableWolverine, bool enableSendResultPublisher)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["WolverineSettings:EnableWolverine"] = enableWolverine.ToString(),
                ["WolverineSettings:EnableEmailSendResultPublisher"] = enableSendResultPublisher.ToString(),
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        services.AddIntegrationServices(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailSendResultDispatcher));

        Assert.NotNull(descriptor);
        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);
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
    public void AddIntegrationServices_ListenerOnPublisherOff_RegistersKafkaDispatcher()
    {
        // Transition scenario: the ASB listener remains active to drain inflight messages,
        // while the publisher is switched back to Kafka.
        // Note: ASB listener activation (via Wolverine) is verified in integration tests —
        // this test only covers dispatcher (IEmailStatusCheckDispatcher) registration.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",   // ASB listener active, draining inflight messages
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "false", // publisher switched back to Kafka
                ["WolverineSettings:EmailStatusCheckQueueName"] = "email-status-check-queue",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        services.AddIntegrationServices(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailStatusCheckDispatcher));

        Assert.NotNull(descriptor);
        Assert.Null(descriptor.ImplementationType);       // Not EmailStatusCheckPublisher (ASB)
        Assert.NotNull(descriptor.ImplementationFactory); // Factory = Kafka path
        Assert.Contains(services, d => d.ImplementationType == typeof(EmailSendingAcceptedConsumer));
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
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailServiceRateLimitDispatcher));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(EmailServiceRateLimitPublisher), descriptor.ImplementationType);
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

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void AddIntegrationServices_EmailServiceRateLimitPublisherNotFullyEnabled_RegistersKafkaDispatcher(bool enableWolverine, bool enablePublisher)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "test-topic",
                ["WolverineSettings:EnableWolverine"] = enableWolverine.ToString(),
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "test-email-status-updated-topic",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "altinn.platform.service.updated",
                ["WolverineSettings:EnableEmailServiceRateLimitPublisher"] = enablePublisher.ToString(),
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.service.ratelimit",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddIntegrationServices(config);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailServiceRateLimitDispatcher));
        Assert.NotNull(descriptor);
        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);
    }
}
