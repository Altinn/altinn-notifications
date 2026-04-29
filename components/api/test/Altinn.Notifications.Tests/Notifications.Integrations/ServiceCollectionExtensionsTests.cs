using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Integrations.Kafka.Publishers;
using Altinn.Notifications.Integrations.Wolverine.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations;

public class ServiceCollectionExtensionsTests
{
    private static IConfigurationBuilder BaseConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
            });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddKafkaServices_SmsTopicMissing_ThrowsInvalidOperationException(string? topicName)
    {
        var config = BaseConfig()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:SmsQueueTopicName"] = topicName,
                ["KafkaSettings:EmailQueueTopicName"] = "email.queue",
                ["KafkaSettings:PastDueOrdersTopicName"] = "past.due.orders",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddKafkaServices(config));

        Assert.Equal("SmsQueueTopicName must be configured when the Wolverine SMS command publisher is disabled.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddKafkaServices_EmailTopicMissing_ThrowsInvalidOperationException(string? topicName)
    {
        var config = BaseConfig()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:SmsQueueTopicName"] = "sms.queue",
                ["KafkaSettings:EmailQueueTopicName"] = topicName,
                ["KafkaSettings:PastDueOrdersTopicName"] = "past.due.orders",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddKafkaServices(config));

        Assert.Equal("EmailQueueTopicName must be configured when the Wolverine email command publisher is disabled.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddKafkaServices_PastDueOrdersTopicMissing_ThrowsInvalidOperationException(string? topicName)
    {
        var config = BaseConfig()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:SmsQueueTopicName"] = "sms.queue",
                ["KafkaSettings:EmailQueueTopicName"] = "email.queue",
                ["KafkaSettings:PastDueOrdersTopicName"] = topicName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddKafkaServices(config));

        Assert.Equal("PastDueOrdersTopicName must be configured when the Wolverine past due order publisher is disabled.", exception.Message);
    }

    [Fact]
    public void AddKafkaServices_WolverineDisabled_AllTopicsPresent_RegistersKafkaPublishers()
    {
        var config = BaseConfig()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:SmsQueueTopicName"] = "sms.queue",
                ["KafkaSettings:EmailQueueTopicName"] = "email.queue",
                ["KafkaSettings:PastDueOrdersTopicName"] = "past.due.orders",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();
        services.AddKafkaServices(config);
        services.AddSingleton(new Mock<IKafkaProducer>().Object);

        var provider = services.BuildServiceProvider();

        Assert.IsType<KafkaSendSmsPublisher>(provider.GetRequiredService<ISendSmsPublisher>());
        Assert.IsType<KafkaEmailCommandPublisher>(provider.GetRequiredService<IEmailCommandPublisher>());
        Assert.IsType<KafkaPastDueOrderPublisher>(provider.GetRequiredService<IPastDueOrderPublisher>());
    }

    [Fact]
    public void AddKafkaServices_WolverineFullyEnabled_RegistersAsbPublishers()
    {
        var config = BaseConfig()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSendSmsPublisher"] = "true",
                ["WolverineSettings:SendSmsQueueName"] = "sms.send.queue",
                ["WolverineSettings:EnableSendEmailPublisher"] = "true",
                ["WolverineSettings:EmailSendQueueName"] = "email.send.queue",
                ["WolverineSettings:EnablePastDueOrderPublisher"] = "true",
                ["WolverineSettings:EnablePastDueOrderListener"] = "true",
                ["WolverineSettings:PastDueOrdersQueueName"] = "past.due.orders.queue",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();
        services.AddKafkaServices(config);

        Assert.Contains(services, d => d.ServiceType == typeof(ISendSmsPublisher) && d.ImplementationType == typeof(SendSmsCommandPublisher));
        Assert.Contains(services, d => d.ServiceType == typeof(IEmailCommandPublisher) && d.ImplementationType == typeof(EmailCommandPublisher));
        Assert.Contains(services, d => d.ServiceType == typeof(IPastDueOrderPublisher) && d.ImplementationType == typeof(PastDueOrderPublisher));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddKafkaServices_WolverineEnabledButSmsSendQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        var config = BaseConfig()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSendSmsPublisher"] = "true",
                ["WolverineSettings:SendSmsQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddKafkaServices(config));

        Assert.Equal("SendSmsQueueName must be configured when EnableSendSmsPublisher is enabled.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddKafkaServices_WolverineEnabledButEmailSendQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        var config = BaseConfig()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:SmsQueueTopicName"] = "sms.queue",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableSendEmailPublisher"] = "true",
                ["WolverineSettings:EmailSendQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddKafkaServices(config));

        Assert.Equal("EmailSendQueueName must be configured when EnableSendEmailPublisher is enabled.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddKafkaServices_WolverineEnabledButPastDueQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        var config = BaseConfig()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:SmsQueueTopicName"] = "sms.queue",
                ["KafkaSettings:EmailQueueTopicName"] = "email.queue",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnablePastDueOrderPublisher"] = "true",
                ["WolverineSettings:EnablePastDueOrderListener"] = "true",
                ["WolverineSettings:PastDueOrdersQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddKafkaServices(config));

        Assert.Equal("PastDueOrdersQueueName must be configured when EnablePastDueOrderPublisher is enabled.", exception.Message);
    }

    [Fact]
    public void AddKafkaServices_PastDuePublisherEnabledButListenerDisabled_ThrowsInvalidOperationException()
    {
        var config = BaseConfig()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:SmsQueueTopicName"] = "sms.queue",
                ["KafkaSettings:EmailQueueTopicName"] = "email.queue",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnablePastDueOrderPublisher"] = "true",
                ["WolverineSettings:EnablePastDueOrderListener"] = "false",
                ["WolverineSettings:PastDueOrdersQueueName"] = "past.due.orders.queue",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddKafkaServices(config));

        Assert.Equal("EnablePastDueOrderListener must be enabled when EnablePastDueOrderPublisher is enabled.", exception.Message);
    }
}
