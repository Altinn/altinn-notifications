using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntegrationServices_SmsGatewaySettingsMissing_ThrowsArgumentNullException()
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
        Assert.StartsWith("Required SmsGatewayConfiguration settings is missing from application configuration.", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_KafkaPathWithValidGatewayConfig_NoException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "altinn.notifications.sms.status.updated",
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
                ["WolverineSettings:EnableWolverine"] = "false",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void AddIntegrationServices_WolverineEnabledWithAllPublishers_NoException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSmsDeliveryReportPublisher"] = "true",
                ["WolverineSettings:SmsDeliveryReportQueueName"] = "altinn.notifications.sms.deliveryreports",
                ["WolverineSettings:EnableSmsSendResultPublisher"] = "true",
                ["WolverineSettings:SmsSendResultQueueName"] = "altinn.notifications.sms.send.result",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIntegrationServices_WolverineEnabledButDeliveryReportQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSmsDeliveryReportPublisher"] = "true",
                ["WolverineSettings:SmsDeliveryReportQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("SmsDeliveryReportQueueName must be configured when EnableSmsDeliveryReportPublisher is enabled.", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_WolverineEnabledWithSendResultPublisher_RegistersSmsSendResultPublisher()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "altinn.notifications.sms.status.updated",
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSmsDeliveryReportPublisher"] = "false",
                ["WolverineSettings:EnableSmsSendResultPublisher"] = "true",
                ["WolverineSettings:SmsSendResultQueueName"] = "altinn.notifications.sms.send.result",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddIntegrationServices(config);

        // Assert
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ISmsSendResultDispatcher>();
        Assert.IsType<SmsSendResultPublisher>(dispatcher);
        Assert.Single(services, d => d.ServiceType == typeof(ISmsSendResultDispatcher));
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
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "altinn.notifications.sms.status.updated",
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSmsDeliveryReportPublisher"] = "false",
                ["WolverineSettings:EnableSmsSendResultPublisher"] = "true",
                ["WolverineSettings:SmsSendResultQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("SmsSendResultQueueName must be configured when EnableSmsSendResultPublisher is enabled.", exception.Message);
    }
}
