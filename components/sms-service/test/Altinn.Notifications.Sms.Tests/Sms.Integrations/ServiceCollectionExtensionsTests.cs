using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Producers;
using Altinn.Notifications.Sms.Integrations.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntegrationServices_MissingSmsGatewayConfig_ThrowsException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KafkaSettings__BrokerAddress", "localhost:9092", EnvironmentVariableTarget.Process);
        string expectedExceptionMessage = "Required SmsGatewayConfiguration settings is missing from application configuration. (Parameter 'config')";

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_MissingKafkaConfig_ThrowsException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KafkaSettings__BrokerAddress", null, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("SmsGatewaySettings__Endpoint", "https://vg.no", EnvironmentVariableTarget.Process);
        string expectedExceptionMessage = "Required Kafka settings is missing from application configuration (Parameter 'config')";

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_SmsGatewayConfigIncluded_NoException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SmsGatewaySettings__Endpoint", "https://vg.no", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("KafkaSettings__BrokerAddress", "localhost:9092", EnvironmentVariableTarget.Process);
        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddIntegrationServices(config));

        // Assert
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
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
                ["WolverineSettings:EnableWolverine"] = enableWolverine.ToString(),
                ["WolverineSettings:EnableSmsDeliveryReportPublisher"] = enableDeliveryReportPublisher.ToString(),
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        services.AddIntegrationServices(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISmsDeliveryReportPublisher));

        Assert.NotNull(descriptor);
        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);
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
                ["SmsGatewaySettings:Endpoint"] = "https://vg.no",
                ["WolverineSettings:EnableWolverine"] = enableWolverine.ToString(),
                ["WolverineSettings:EnableSmsSendResultPublisher"] = enableSendResultPublisher.ToString(),
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        services.AddIntegrationServices(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISmsSendResultDispatcher));

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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
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
