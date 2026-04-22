using System.Text.Json;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Wolverine.Commands;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.IntegrationTestsASB.Utils;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for <see cref="IPastDueOrderPublisher"/> and its implementation.
/// Verifies that past-due orders are correctly wrapped in <see cref="ProcessPastDueOrderCommand"/>
/// and delivered to the Azure Service Bus queue via Wolverine.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class PastDueOrderPublisherTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Verifies that publishing a valid batch returns an empty list (success indicator).
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidOrders_ReturnsEmptyList()
    {
        var factory = CreateFactory();
        var order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        var orders = new List<NotificationOrder> { order };

        await using (factory)
        {
            string queueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Services.GetRequiredService<IPastDueOrderPublisher>();

            var result = await publisher.PublishAsync(orders, CancellationToken.None);

            Assert.Empty(result);

            // Drain the message from the queue so it doesn't pollute subsequent tests
            await ServiceBusTestUtils.WaitForMessageAsync(_fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>
    /// Verifies that the published order is correctly wrapped in a <see cref="ProcessPastDueOrderCommand"/>
    /// with the original order ID preserved when delivered to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidOrder_DeliversCommandWithCorrectOrderIdToQueue()
    {
        var factory = CreateFactory();
        var orderId = Guid.NewGuid();
        var order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = orderId;

        await using (factory)
        {
            string queueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Services.GetRequiredService<IPastDueOrderPublisher>();

            await publisher.PublishAsync([order], CancellationToken.None);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(message);

            var command = JsonSerializer.Deserialize<ProcessPastDueOrderCommand>(message.Body.ToString(), _jsonSerializerOptions);

            Assert.NotNull(command);
            Assert.Equal(orderId, command.Order.Id);
        }
    }

    /// <summary>
    /// Verifies that publishing a batch delivers one <see cref="ProcessPastDueOrderCommand"/> per order
    /// to the queue, with all order IDs correctly preserved.
    /// </summary>
    [Fact]
    public async Task PublishAsync_Batch_DeliversAllCommandsToQueue()
    {
        var factory = CreateFactory();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstOrder = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        firstOrder.Id = firstId;
        var secondOrder = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        secondOrder.Id = secondId;
        var orders = new List<NotificationOrder> { firstOrder, secondOrder };

        await using (factory)
        {
            string queueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Services.GetRequiredService<IPastDueOrderPublisher>();

            await publisher.PublishAsync(orders, CancellationToken.None);

            var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(10));

            var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(10));

            Assert.NotNull(firstMessage);
            Assert.NotNull(secondMessage);

            var commands = new[]
            {
                JsonSerializer.Deserialize<ProcessPastDueOrderCommand>(firstMessage.Body.ToString(), _jsonSerializerOptions),
                JsonSerializer.Deserialize<ProcessPastDueOrderCommand>(secondMessage.Body.ToString(), _jsonSerializerOptions)
            };

            Assert.Contains(commands, c => c?.Order.Id == firstId);
            Assert.Contains(commands, c => c?.Order.Id == secondId);
        }
    }

    /// <summary>
    /// Verifies that publishing an empty batch returns an empty list without enqueuing any messages.
    /// </summary>
    [Fact]
    public async Task PublishAsync_EmptyList_ReturnsEmptyListWithoutEnqueuingMessages()
    {
        var factory = CreateFactory();

        await using (factory)
        {
            string queueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Services.GetRequiredService<IPastDueOrderPublisher>();

            var result = await publisher.PublishAsync([], CancellationToken.None);

            Assert.Empty(result);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(5));

            Assert.Null(message);
        }
    }

    /// <summary>
    /// Verifies that a pre-cancelled token causes <see cref="OperationCanceledException"/>
    /// to be thrown before any messages are sent to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var factory = CreateFactory();
        var order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        var orders = new List<NotificationOrder> { order };

        await using (factory)
        {
            string queueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Services.GetRequiredService<IPastDueOrderPublisher>();

            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(orders, cancellationTokenSource.Token));

            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, queueName, TimeSpan.FromSeconds(5));
        }
    }

    private IntegrationTestWebApplicationFactory CreateFactory()
    {
        return new IntegrationTestWebApplicationFactory(_fixture).Initialize();
    }

    private static string GetQueueName(IntegrationTestWebApplicationFactory factory)
    {
        return factory.WolverineSettings!.PastDueOrdersQueueName;
    }
}
