using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class DynamicConfigServiceTests
{
    [Fact]
    public async Task GetDynamicConfig_AppliesOnlyTaskCountsAndRestoresStaticValueWhenOverrideIsRemoved()
    {
        var staticConfig = new NotificationConfig { DefaultSmsSenderNumber = "12345" };
        staticConfig.PastDueOrdersProcessLoopConfig.TaskCount = 30;
        staticConfig.PastDueOrdersProcessLoopConfig.PrimaryTaskIdleDelaySeconds = 12;
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["notificationconfig:pastdueordersprocessloopconfig:taskcount"] = "0",
            ["NotificationConfig:PastDueOrdersProcessLoopConfig:PrimaryTaskIdleDelaySeconds"] = "1",
            ["NotificationConfig:DefaultSmsSenderNumber"] = "99999"
        };

        var repository = new Mock<IDynamicConfigRepository>(MockBehavior.Strict);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var podIds = new List<Guid>();
        repository.Setup(r => r.GetPodCount(It.IsAny<Guid>(), TimeSpan.FromMinutes(2), cancellationToken))
            .Callback<Guid, TimeSpan, CancellationToken>((podId, _, _) => podIds.Add(podId))
            .ReturnsAsync(3);
        repository.SetupSequence(r => r.GetConfig(cancellationToken))
            .ReturnsAsync(overrides)
            .ReturnsAsync(new Dictionary<string, string>());
        var service = new DynamicConfigService(repository.Object, NullLogger<DynamicConfigService>.Instance);

        var (effectiveConfig, podCount) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);
        var (restoredConfig, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(3, podCount);
        Assert.Equal(0, effectiveConfig.PastDueOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(12, effectiveConfig.PastDueOrdersProcessLoopConfig.PrimaryTaskIdleDelaySeconds);
        Assert.Equal("12345", effectiveConfig.DefaultSmsSenderNumber);
        Assert.Equal(30, staticConfig.PastDueOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(30, restoredConfig.PastDueOrdersProcessLoopConfig.TaskCount);
        Assert.Collection(podIds, first => Assert.NotEqual(Guid.Empty, first), second => Assert.Equal(podIds[0], second));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public async Task GetDynamicConfig_IgnoresInvalidTaskCount(string invalidValue)
    {
        var staticConfig = new NotificationConfig();
        staticConfig.RetryOrdersProcessLoopConfig.TaskCount = 9;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new Mock<IDynamicConfigRepository>();
        repository.Setup(r => r.GetPodCount(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), cancellationToken)).ReturnsAsync(1);
        repository.Setup(r => r.GetConfig(cancellationToken)).ReturnsAsync(new Dictionary<string, string>
        {
            ["NotificationConfig:RetryOrdersProcessLoopConfig:TaskCount"] = invalidValue
        });
        var service = new DynamicConfigService(repository.Object, NullLogger<DynamicConfigService>.Instance);

        var (effectiveConfig, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(9, effectiveConfig.RetryOrdersProcessLoopConfig.TaskCount);
    }
}
