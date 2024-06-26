﻿using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Health;
using Altinn.Notifications.Integrations.Kafka.Producers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations;

public class KafkaHealthCheckTests : IAsyncLifetime
{
    private readonly string _topicName = Guid.NewGuid().ToString();
    private readonly KafkaSettings _settings;

    public KafkaHealthCheckTests()
    {
        IConfiguration configuration = new ConfigurationBuilder()
                       .AddJsonFile("appsettings.json")
                       .Build();

        KafkaSettings? settings = configuration.GetSection("KafkaSettings").Get<KafkaSettings>();
        settings!.Admin.TopicList = new List<string>() { _topicName };

        _settings = settings;
        _ = new KafkaProducer(Options.Create(settings), Mock.Of<ILogger<KafkaProducer>>());
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyResult()
    {
        using KafkaHealthCheck healthCheck = new(_settings);
        HealthCheckResult res = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, res.Status);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await KafkaUtil.DeleteTopicAsync(_topicName);
    }

    /// <inheritdoc/>
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
