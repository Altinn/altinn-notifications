using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Producers;
using Altinn.Notifications.Sms.Integrations.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

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

        Assert.Equal("Required SmsGatewayConfiguration settings is missing from application configuration. (Parameter 'config')", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_MissingKafkaConfig_ThrowsException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        Assert.Equal("Required Kafka settings is missing from application configuration (Parameter 'config')", exception.Message);
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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AddIntegrationServices_WolverineDeliveryReportPublisherNotFullyEnabled_RegistersKafkaPublisher(bool enableWolverine, bool enableDeliveryReportPublisher)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "sms.status.updated",
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
                ["WolverineSettings:EnableWolverine"] = enableWolverine.ToString(),
                ["WolverineSettings:EnableSmsDeliveryReportPublisher"] = enableDeliveryReportPublisher.ToString(),
            })
            .Build();

        IServiceCollection services = new ServiceCollection();
        services.AddIntegrationServices(config);
        services.Replace(ServiceDescriptor.Singleton<ICommonProducer>(new Mock<ICommonProducer>().Object));

        var publisher = services.BuildServiceProvider().GetRequiredService<ISmsDeliveryReportPublisher>();

        Assert.IsType<KafkaSmsDeliveryReportPublisher>(publisher);
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

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISmsSendResultDispatcher));

        Assert.NotNull(descriptor);
        Assert.Equal(typeof(SmsSendResultPublisher), descriptor.ImplementationType);
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
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = "sms.status.updated",
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
                ["WolverineSettings:EnableWolverine"] = enableWolverine.ToString(),
                ["WolverineSettings:EnableSmsSendResultPublisher"] = enableSendResultPublisher.ToString(),
            })
            .Build();

        IServiceCollection services = new ServiceCollection();
        services.AddIntegrationServices(config);
        services.Replace(ServiceDescriptor.Singleton<ICommonProducer>(new Mock<ICommonProducer>().Object));

        var dispatcher = services.BuildServiceProvider().GetRequiredService<ISmsSendResultDispatcher>();

        Assert.IsType<SmsSendResultProducer>(dispatcher);
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIntegrationServices_KafkaDeliveryReportTopicMissing_ThrowsInvalidOperationException(string? topicName)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = topicName,
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        Assert.Equal("SmsStatusUpdatedTopicName must be configured when the Wolverine SMS delivery report publisher is disabled.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIntegrationServices_KafkaSendResultTopicMissing_ThrowsInvalidOperationException(string? topicName)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["KafkaSettings:SmsStatusUpdatedTopicName"] = topicName,
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSmsDeliveryReportPublisher"] = "true",
                ["WolverineSettings:SmsDeliveryReportQueueName"] = "sms.delivery.report.queue",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        Assert.Equal("SmsStatusUpdatedTopicName must be configured when the Wolverine SMS send result publisher is disabled.", exception.Message);
    }
}
