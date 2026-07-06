using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class ComposedEmailPublishBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_SignalFires_CallsSendComposedNotifications()
    {
        // Arrange
        var sendCalled = new TaskCompletionSource();

        var emailServiceMock = new Mock<IEmailNotificationService>();
        emailServiceMock
            .Setup(s => s.SendComposedNotifications(It.IsAny<CancellationToken>()))
            .Callback(() => sendCalled.TrySetResult())
            .Returns(Task.CompletedTask);

        int signalCallCount = 0;
        var signalMock = new Mock<IComposedEmailPublishSignal>();
        signalMock
            .Setup(s => s.WaitAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                if (Interlocked.Increment(ref signalCallCount) == 1)
                {
                    return;
                }

                await Task.Delay(Timeout.Infinite, ct);
            });

        var service = new ComposedEmailPublishBackgroundService(
            emailServiceMock.Object,
            signalMock.Object,
            new Mock<ILogger<ComposedEmailPublishBackgroundService>>().Object);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await sendCalled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        emailServiceMock.Verify(s => s.SendComposedNotifications(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WaitAsyncThrowsOperationCanceledException_ExitsCleanly()
    {
        // Arrange
        var emailServiceMock = new Mock<IEmailNotificationService>();

        var signalMock = new Mock<IComposedEmailPublishSignal>();
        signalMock
            .Setup(s => s.WaitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new ComposedEmailPublishBackgroundService(
            emailServiceMock.Object,
            signalMock.Object,
            new Mock<ILogger<ComposedEmailPublishBackgroundService>>().Object);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await service.StopAsync(TestContext.Current.CancellationToken);
        emailServiceMock.Verify(s => s.SendComposedNotifications(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WaitAsyncThrowsGeneralException_LogsErrorAndContinuesLoop()
    {
        // Arrange
        var loopContinued = new TaskCompletionSource();
        var exception = new InvalidOperationException("signal failure");

        int signalCallCount = 0;
        var signalMock = new Mock<IComposedEmailPublishSignal>();
        signalMock
            .Setup(s => s.WaitAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                if (Interlocked.Increment(ref signalCallCount) == 1)
                {
                    throw exception;
                }

                loopContinued.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            });

        var loggerMock = new Mock<ILogger<ComposedEmailPublishBackgroundService>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var service = new ComposedEmailPublishBackgroundService(
            new Mock<IEmailNotificationService>().Object,
            signalMock.Object,
            loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await loopContinued.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert — error logged and loop did not exit (second WaitAsync was reached)
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("waiting for composed email")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SendComposedNotificationsThrowsOperationCanceledException_ExitsCleanly()
    {
        // Arrange
        var sendCalled = new TaskCompletionSource();

        var emailServiceMock = new Mock<IEmailNotificationService>();
        emailServiceMock
            .Setup(s => s.SendComposedNotifications(It.IsAny<CancellationToken>()))
            .Callback(() => sendCalled.TrySetResult())
            .ThrowsAsync(new OperationCanceledException());

        var signalMock = new Mock<IComposedEmailPublishSignal>();
        signalMock
            .Setup(s => s.WaitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ComposedEmailPublishBackgroundService(
            emailServiceMock.Object,
            signalMock.Object,
            new Mock<ILogger<ComposedEmailPublishBackgroundService>>().Object);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await sendCalled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        emailServiceMock.Verify(s => s.SendComposedNotifications(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SendComposedNotificationsThrowsGeneralException_LogsErrorAndContinuesLoop()
    {
        // Arrange
        var loopContinued = new TaskCompletionSource();
        var exception = new InvalidOperationException("send failure");

        int signalCallCount = 0;
        var signalMock = new Mock<IComposedEmailPublishSignal>();
        signalMock
            .Setup(s => s.WaitAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                if (Interlocked.Increment(ref signalCallCount) > 1)
                {
                    loopContinued.TrySetResult();
                    await Task.Delay(Timeout.Infinite, ct);
                }
            });

        var emailServiceMock = new Mock<IEmailNotificationService>();
        emailServiceMock
            .Setup(s => s.SendComposedNotifications(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var loggerMock = new Mock<ILogger<ComposedEmailPublishBackgroundService>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var service = new ComposedEmailPublishBackgroundService(
            emailServiceMock.Object,
            signalMock.Object,
            loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await loopContinued.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert — error logged and loop continued to next WaitAsync
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("sending composed email")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
