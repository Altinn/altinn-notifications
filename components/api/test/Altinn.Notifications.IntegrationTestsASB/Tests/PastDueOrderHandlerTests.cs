using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Wolverine.Commands;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.IntegrationTestsASB.Utils;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

[Collection(nameof(IntegrationTestContainersCollection))]
public class PastDueOrderHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    [Fact]
    public async Task ProcessPastDueOrder_WhenPublishedViaASB_HandlerProcessesOrder()
    {
        // Arrange - mock service that captures calls and returns success
        int processCallCount = 0;
        var testOrder = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        testOrder.Id = Guid.NewGuid();

        var mockService = new Mock<IOrderProcessingService>();
        mockService
            .Setup(s => s.ProcessOrder(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(_ => Interlocked.Increment(ref processCallCount))
            .ReturnsAsync(new NotificationOrderProcessingResult { IsRetryRequired = false });

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            // Act - publish order via the ASB publisher (end-to-end: publisher → ASB → handler)
            var publisher = factory.Services.GetRequiredService<IPastDueOrderPublisher>();
            await publisher.PublishAsync([testOrder]);

            // Assert - handler called exactly once, queue drained
            var processed = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(processCallCount == 1),
                maxAttempts: 20,
                delayMs: 500);
            Assert.True(processed, "Handler should have processed the order exactly once");

            var queueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture.ServiceBusConnectionString,
                factory.WolverineSettings!.PastDueOrdersQueueName,
                TimeSpan.FromSeconds(5));
            Assert.True(queueEmpty, "Queue should be empty after successful processing");
        }
    }

    [Fact]
    public async Task ProcessPastDueOrder_WhenSendConditionIsInconclusive_ProcessOrderRetryCalledOnRetry()
    {
        // Arrange - ProcessOrder returns inconclusive; the handler schedules a new command
        // with IsRetry = true after PastDueOrdersRetryDelayMs. ProcessOrderRetry succeeds,
        // so the message should be processed without going to DLQ.
        int processOrderCallCount = 0;
        int processOrderRetryCallCount = 0;

        var mockService = new Mock<IOrderProcessingService>();
        mockService
            .Setup(s => s.ProcessOrder(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(_ => Interlocked.Increment(ref processOrderCallCount))
            .ReturnsAsync(new NotificationOrderProcessingResult { IsRetryRequired = true });
        mockService
            .Setup(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(_ => Interlocked.Increment(ref processOrderRetryCallCount))
            .Returns(Task.CompletedTask);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            var settings = factory.WolverineSettings!;
            string queueName = settings.PastDueOrdersQueueName;

            // Act - send command directly to the queue (IsRetry defaults to false)
            await factory.SendToQueueAsync(queueName, new ProcessPastDueOrderCommand
            {
                Order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient()
            });

            // Assert - ProcessOrderRetry called once on the scheduled retry attempt
            const int pollDelayMs = 500;
            var retryProcessed = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(processOrderRetryCallCount == 1),
                maxAttempts: (settings.PastDueOrdersRetryDelayMs + 5000) / pollDelayMs,
                delayMs: pollDelayMs);
            Assert.True(retryProcessed, "ProcessOrderRetry should be called once on the retry");
            Assert.Equal(1, processOrderCallCount);

            // Assert - queue empty, message not dead-lettered
            var queueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(queueEmpty, "Queue should be empty after successful retry");
        }
    }

    [Fact]
    public async Task ProcessPastDueOrder_WhenProcessOrderAlwaysThrowsNpgsqlException_MovesToDeadLetterQueue()
    {
        // Arrange - ProcessOrder always throws; since all Wolverine-managed retries re-deliver
        // the original message (IsRetry = false), ProcessOrder is called on every attempt and
        // ProcessOrderRetry is never called.
        int processOrderCallCount = 0;

        var mockService = new Mock<IOrderProcessingService>();
        mockService
            .Setup(s => s.ProcessOrder(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(_ => Interlocked.Increment(ref processOrderCallCount))
            .ThrowsAsync(new NpgsqlException("Simulated database error"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            var policy = factory.WolverineSettings!.PastDueOrdersQueuePolicy;
            int expectedRetryAttempts = policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;
            int expectedTotalProcessOrderCalls = 1 + expectedRetryAttempts;

            string queueName = factory.WolverineSettings!.PastDueOrdersQueueName;

            // Act - send command directly to the queue
            await factory.SendToQueueAsync(queueName, new ProcessPastDueOrderCommand
            {
                Order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient()
            });

            // Assert - message moves to DLQ after all retries exhausted
            var dlqTimeout = TimeSpan.FromMilliseconds(policy.CooldownDelaysMs.Sum() + policy.ScheduleDelaysMs.Sum() + 10_000);
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                dlqTimeout);
            Assert.NotNull(deadLetterMessage);

            // Assert - ProcessOrder called on every attempt (initial + all retries),
            // ProcessOrderRetry never called because IsRetry is always false on re-delivered messages.
            Console.WriteLine($"[Test] ProcessOrder calls: {processOrderCallCount} (expected {expectedTotalProcessOrderCalls})");
            Assert.Equal(expectedTotalProcessOrderCalls, processOrderCallCount);
            mockService.Verify(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);
        }
    }

    [Fact]
    public async Task ProcessPastDueOrder_WhenProcessOrderThrowsPlatformDependencyException_OrderStatusResetToRegistered()
    {
        // Arrange - real OrderProcessingService wired against real DB, but with a mock email
        // processing service that throws PlatformDependencyException, reproducing the scenario
        // where a platform dependency (e.g. profile service) is unavailable during processing.
        // This test proves that ProcessOrderRetry — which catches PlatformDependencyException
        // and resets the order to Registered — is still reached via the IsRetry=true scheduled command.
        int processOrderCallCount = 0;
        int processOrderRetryCallCount = 0;

        var mockEmailProcessingService = new Mock<IEmailOrderProcessingService>();
        mockEmailProcessingService
            .Setup(s => s.ProcessOrder(It.IsAny<NotificationOrder>()))
            .Callback(() => Interlocked.Increment(ref processOrderCallCount))
            .ThrowsAsync(new PlatformDependencyException("Profile", "GetUserContactPoints", new TaskCanceledException()));
        mockEmailProcessingService
            .Setup(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()))
            .Callback(() => Interlocked.Increment(ref processOrderRetryCallCount))
            .ThrowsAsync(new PlatformDependencyException("Profile", "GetUserContactPoints", new TaskCanceledException()));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService<IEmailOrderProcessingService>(_ => mockEmailProcessingService.Object)
            .Initialize();

        await using (factory)
        {
            var settings = factory.WolverineSettings!;
            string queueName = settings.PastDueOrdersQueueName;

            // Persist a real order in the DB so the status can be observed
            var (order, _) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);

            // Manually set to Processing to simulate the state when the handler picks it up
            await PostgreUtil.RunSql(
                _fixture.PostgresConnectionString,
                $"UPDATE notifications.orders SET processedstatus = 'Processing' WHERE alternateid='{order.Id}'");

            // Act - send the command with IsRetry=false (initial attempt)
            await factory.SendToQueueAsync(queueName, new ProcessPastDueOrderCommand { Order = order });

            // Assert - after the scheduled IsRetry=true message is processed and
            // ProcessOrderRetry catches PlatformDependencyException, the order is reset to Registered
            const int pollDelayMs = 500;
            var orderResetToRegistered = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    var status = await PostgreUtil.RunSqlReturnOutput<string>(
                        _fixture.PostgresConnectionString,
                        $"SELECT processedstatus FROM notifications.orders WHERE alternateid='{order.Id}'");
                    return status == "Registered";
                },
                maxAttempts: (settings.PastDueOrdersRetryDelayMs + 10_000) / pollDelayMs,
                delayMs: pollDelayMs);

            Assert.True(
                orderResetToRegistered,
                "Order should be reset to Registered after ProcessOrderRetry catches PlatformDependencyException");
            Assert.Equal(1, processOrderCallCount);
            Assert.Equal(1, processOrderRetryCallCount);
        }
    }
}
