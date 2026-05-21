using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntegrationServices_MissingSmsGatewayConfig_ThrowsException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        Assert.StartsWith("Required SmsGatewayConfiguration settings is missing from application configuration.", exception.Message);
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void AddIntegrationServices_SmsGatewayConfigIncluded_NoException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "sms.status.updated",
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Record.Exception(() => services.AddIntegrationServices(config));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddIntegrationServices_TimeoutInSecondsIsNotPositive_ThrowsInvalidOperationException(int timeout)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "sms.status.updated",
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
                ["SmsGatewaySettings:TimeoutInSeconds"] = timeout.ToString(),
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        Assert.Equal("TimeoutInSeconds must be greater than 0.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIntegrationServices_WolverineEnabledButDeliveryReportQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSmsDeliveryReportPublisher"] = "true",
                ["WolverineSettings:SmsDeliveryReportQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        Assert.Equal("SmsDeliveryReportQueueName must be configured when EnableSmsDeliveryReportPublisher is enabled.", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_WolverineAndSendResultPublisherEnabled_RegistersSmsSendResultPublisher()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "sms.status.updated",
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSmsSendResultPublisher"] = "true",
                ["WolverineSettings:SmsSendResultQueueName"] = "altinn.notifications.sms.send.result",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        services.AddIntegrationServices(config);

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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "sms.status.updated",
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSmsSendResultPublisher"] = "true",
                ["WolverineSettings:SmsSendResultQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        Assert.Equal("SmsSendResultQueueName must be configured when EnableSmsSendResultPublisher is enabled.", exception.Message);
    }
}
