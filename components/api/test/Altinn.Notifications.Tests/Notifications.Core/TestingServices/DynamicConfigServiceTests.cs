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
        repository.Setup(r => r.GetPodCount(It.IsAny<Guid>(), TimeSpan.FromSeconds(90), cancellationToken))
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

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    public async Task GetDynamicConfig_NormalizesStaticTaskCountsForEveryPool(int configuredCount, int expectedCount)
    {
        var staticConfig = new NotificationConfig();
        foreach (ProcessLoopConfig loop in GetLoops(staticConfig))
        {
            loop.TaskCount = configuredCount;
            loop.PrimaryTaskIdleDelaySeconds = 12;
            loop.AdditionalTasksIdleDelaySeconds = 6;
            loop.RampUpLimit = 8;
        }

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new Mock<IDynamicConfigRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetPodCount(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), cancellationToken)).ReturnsAsync(3);
        repository.Setup(r => r.GetConfig(cancellationToken)).ReturnsAsync(new Dictionary<string, string>());
        var service = new DynamicConfigService(repository.Object, NullLogger<DynamicConfigService>.Instance);

        var (effectiveConfig, podCount) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(3, podCount);
        Assert.All(GetLoops(effectiveConfig), loop =>
        {
            Assert.Equal(expectedCount, loop.TaskCount);
            Assert.Equal(12, loop.PrimaryTaskIdleDelaySeconds);
            Assert.Equal(6, loop.AdditionalTasksIdleDelaySeconds);
            Assert.Equal(8, loop.RampUpLimit);
        });
        Assert.All(GetLoops(staticConfig), loop => Assert.Equal(configuredCount, loop.TaskCount));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 3)]
    [InlineData("2", 3)]
    [InlineData("3", 3)]
    [InlineData("4", 4)]
    [InlineData("invalid", 3)]
    public async Task GetDynamicConfig_NormalizesOverridesAndInvalidFallback(string configuredCount, int expectedCount)
    {
        var staticConfig = new NotificationConfig();
        staticConfig.RetryOrdersProcessLoopConfig.TaskCount = 1;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new Mock<IDynamicConfigRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetPodCount(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), cancellationToken)).ReturnsAsync(3);
        repository.Setup(r => r.GetConfig(cancellationToken)).ReturnsAsync(new Dictionary<string, string>
        {
            ["NotificationConfig:RetryOrdersProcessLoopConfig:TaskCount"] = configuredCount
        });
        var service = new DynamicConfigService(repository.Object, NullLogger<DynamicConfigService>.Instance);

        var (effectiveConfig, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(expectedCount, effectiveConfig.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(1, staticConfig.RetryOrdersProcessLoopConfig.TaskCount);
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 4)]
    public async Task GetDynamicConfig_WhenDatabaseReadFails_NormalizesStaticTaskCount(bool podCountFails, int expectedPodCount)
    {
        var staticConfig = new NotificationConfig();
        staticConfig.RetryOrdersProcessLoopConfig.TaskCount = 1;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new Mock<IDynamicConfigRepository>(MockBehavior.Strict);
        if (podCountFails)
        {
            repository.Setup(r => r.GetPodCount(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), cancellationToken)).ThrowsAsync(new InvalidOperationException());
        }
        else
        {
            repository.Setup(r => r.GetPodCount(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), cancellationToken)).ReturnsAsync(4);
            repository.Setup(r => r.GetConfig(cancellationToken)).ThrowsAsync(new InvalidOperationException());
        }

        var service = new DynamicConfigService(repository.Object, NullLogger<DynamicConfigService>.Instance);
        var (effectiveConfig, podCount) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(expectedPodCount, podCount);
        Assert.Equal(expectedPodCount, effectiveConfig.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(1, staticConfig.RetryOrdersProcessLoopConfig.TaskCount);
    }

    [Fact]
    public async Task GetDynamicConfig_WhenConfigReadFails_UsesLastSuccessfulOverridesUntilRecovery()
    {
        var staticConfig = new NotificationConfig();
        staticConfig.RetryOrdersProcessLoopConfig.TaskCount = 9;
        staticConfig.PastDueOrdersProcessLoopConfig.TaskCount = 12;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new Mock<IDynamicConfigRepository>(MockBehavior.Strict);
        repository.SetupSequence(r => r.GetPodCount(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), cancellationToken))
            .ReturnsAsync(2).ReturnsAsync(4).ReturnsAsync(1).ReturnsAsync(1).ReturnsAsync(1).ReturnsAsync(1).ReturnsAsync(1);
        repository.SetupSequence(r => r.GetConfig(cancellationToken))
            .ThrowsAsync(new InvalidOperationException())
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["NotificationConfig:RetryOrdersProcessLoopConfig:TaskCount"] = "1",
                ["NotificationConfig:PastDueOrdersProcessLoopConfig:TaskCount"] = "0"
            })
            .ThrowsAsync(new InvalidOperationException())
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["NotificationConfig:RetryOrdersProcessLoopConfig:TaskCount"] = "0"
            })
            .ThrowsAsync(new InvalidOperationException())
            .ReturnsAsync(new Dictionary<string, string>())
            .ThrowsAsync(new InvalidOperationException());
        var service = new DynamicConfigService(repository.Object, NullLogger<DynamicConfigService>.Instance);

        var (initial, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);
        var (firstSuccess, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);
        var (firstFailure, firstFailurePodCount) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);
        var (updated, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);
        var (secondFailure, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);
        var (removed, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);
        var (failureAfterRemoval, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(9, initial.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(12, initial.PastDueOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(4, firstSuccess.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(0, firstSuccess.PastDueOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(1, firstFailurePodCount);
        Assert.Equal(1, firstFailure.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(0, firstFailure.PastDueOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(0, updated.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(12, updated.PastDueOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(0, secondFailure.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(12, secondFailure.PastDueOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(9, removed.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(9, failureAfterRemoval.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(12, failureAfterRemoval.PastDueOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(9, staticConfig.RetryOrdersProcessLoopConfig.TaskCount);
    }

    [Fact]
    public async Task GetDynamicConfig_WhenPodCountReadFails_UsesLastOverridesAndPodCount()
    {
        var staticConfig = new NotificationConfig();
        staticConfig.RetryOrdersProcessLoopConfig.TaskCount = 9;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new Mock<IDynamicConfigRepository>(MockBehavior.Strict);
        repository.SetupSequence(r => r.GetPodCount(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), cancellationToken))
            .ReturnsAsync(3)
            .ThrowsAsync(new InvalidOperationException());
        repository.Setup(r => r.GetConfig(cancellationToken)).ReturnsAsync(new Dictionary<string, string>
        {
            ["NotificationConfig:RetryOrdersProcessLoopConfig:TaskCount"] = "1"
        });
        var service = new DynamicConfigService(repository.Object, NullLogger<DynamicConfigService>.Instance);

        await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);
        var (fallback, podCount) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(3, podCount);
        Assert.Equal(3, fallback.RetryOrdersProcessLoopConfig.TaskCount);
        repository.Verify(r => r.GetConfig(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetDynamicConfig_WhenPodCountChanges_RecalculatesFromStaticConfiguration()
    {
        var staticConfig = new NotificationConfig();
        staticConfig.RetryOrdersProcessLoopConfig.TaskCount = 2;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new Mock<IDynamicConfigRepository>(MockBehavior.Strict);
        repository.SetupSequence(r => r.GetPodCount(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), cancellationToken)).ReturnsAsync(4).ReturnsAsync(1);
        repository.Setup(r => r.GetConfig(cancellationToken)).ReturnsAsync(new Dictionary<string, string>());
        var service = new DynamicConfigService(repository.Object, NullLogger<DynamicConfigService>.Instance);

        var (first, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);
        var (second, _) = await service.GetDynamicConfig(staticConfig, TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(4, first.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(2, second.RetryOrdersProcessLoopConfig.TaskCount);
        Assert.Equal(2, staticConfig.RetryOrdersProcessLoopConfig.TaskCount);
    }

    private static ProcessLoopConfig[] GetLoops(NotificationConfig config) =>
    [
        config.EmailDaytimeNotificationsProcessLoopConfig,
        config.EmailAnytimeNotificationsProcessLoopConfig,
        config.ComposedEmailNotificationsProcessLoopConfig,
        config.SmsDaytimeNotificationsProcessLoopConfig,
        config.SmsAnytimeNotificationsProcessLoopConfig,
        config.PastDueOrdersProcessLoopConfig,
        config.RetryOrdersProcessLoopConfig
    ];
}
