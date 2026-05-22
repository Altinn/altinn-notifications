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
    public void AddIntegrationServices_KafkaSettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
        Assert.StartsWith("Required Kafka settings is missing from application configuration", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_CommunicationServicesSettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
        Assert.StartsWith("Required communication services settings is missing from application configuration", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_EmailServiceAdminSettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
        Assert.StartsWith("Required email service admin settings is missing from application configuration", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_WolverineEnabledWithStatusCheckPublisher_RegistersEmailStatusCheckPublisher()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "altinn.notifications.email.status.updated",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "altinn.platform.service.updated",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "true",
                ["WolverineSettings:EnableEmailServiceRateLimitPublisher"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.send.ratelimit"
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
    public void AddIntegrationServices_WolverineEnabledButStatusCheckListenerDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "altinn.notifications.email.status.updated",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "false",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "false",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("EnableEmailStatusCheckListener must be enabled when EnableEmailStatusCheckPublisher is enabled.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIntegrationServices_WolverineEnabledButStatusCheckQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "altinn.notifications.email.status.updated",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "false",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
                ["WolverineSettings:EmailStatusCheckQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("EmailStatusCheckQueueName must be configured when EnableEmailStatusCheckPublisher is enabled.", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_WolverineEnabledWithSendResultPublisher_RegistersEmailSendResultPublisher()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "altinn.notifications.email.sending.accepted",
                ["KafkaSettings:AltinnServiceUpdateTopicName"] = "altinn.platform.service.updated",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "true",
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "false",
                ["WolverineSettings:EnableEmailServiceRateLimitPublisher"] = "false",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddIntegrationServices(config);

        // Assert
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
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "true",
                ["WolverineSettings:EmailSendResultQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("EmailSendResultQueueName must be configured when EnableEmailSendResultPublisher is enabled.", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_WolverineEnabledWithRateLimitPublisher_RegistersEmailServiceRateLimitPublisher()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "altinn.notifications.email.status.updated",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "altinn.notifications.email.sending.accepted",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "false",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "false",
                ["WolverineSettings:EnableEmailServiceRateLimitPublisher"] = "true",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.send.ratelimit",
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
    public void AddIntegrationServices_WolverineEnabledButRateLimitQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:EmailStatusUpdatedTopicName"] = "altinn.notifications.email.status.updated",
                ["KafkaSettings:EmailSendingAcceptedTopicName"] = "altinn.notifications.email.sending.accepted",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "false",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "false",
                ["WolverineSettings:EnableEmailServiceRateLimitPublisher"] = "true",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(
            "EmailServiceRateLimitQueueName must be configured when EnableEmailServiceRateLimitPublisher is enabled.",
            exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_WolverineEnabledWithAllPublishers_NoException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",    
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "true",
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
                ["WolverineSettings:EnableEmailServiceRateLimitPublisher"] = "true",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.send.ratelimit",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Null(exception);
    }
}
