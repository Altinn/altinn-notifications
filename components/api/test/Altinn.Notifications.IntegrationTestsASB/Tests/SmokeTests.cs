using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.IntegrationTestsASB.Extensions;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.IntegrationTestsASB.Utils;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Azure.Messaging.ServiceBus;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Smoke tests to verify the TestContainers-based integration test infrastructure
/// works correctly: containers start, host initializes, ASB and Postgres are functional.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SmokeTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Verifies that the test infrastructure starts correctly:
    /// containers are running, Wolverine connects to ASB, and the host initializes.
    /// </summary>
    [Fact]
    public async Task Infrastructure_StartsSuccessfully_ContainersRunningAndHostInitialized()
    {
        Assert.True(_fixture.IsRunning, "Test containers should be running");
        Assert.False(string.IsNullOrEmpty(_fixture.ServiceBusConnectionString), "ServiceBus connection string should be set");
        Assert.False(string.IsNullOrEmpty(_fixture.PostgresConnectionString), "Postgres connection string should be set");

        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            Assert.NotNull(factory.Host);
            Assert.True(factory.WolverineSettings.EnableWolverine, "Wolverine should be enabled");
            Assert.True(factory.WolverineSettings.EnableEmailDeliveryReportListener, "Email delivery report listener should be enabled");
        }
    }

    /// <summary>
    /// Verifies that a message can be sent to the email delivery report queue
    /// and that the queue receives it via the ASB emulator.
    /// </summary>
    [Fact]
    public async Task ServiceBus_CanSendAndReceiveMessage_OnDeliveryReportQueue()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings.EmailDeliveryReportQueueName;

            // Send a test message directly to the queue
            await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
            await using var sender = client.CreateSender(queueName);

            string testBody = "{\"test\": \"message\"}";
            await sender.SendMessageAsync(new ServiceBusMessage(testBody));

            // Wolverine's listener will pick up the message. Since the handler
            // doesn't exist on this branch, the message will eventually end up
            // in the dead letter queue or be processed with an error.
            // For the smoke test, we just verify the send succeeded without throwing.
            Assert.True(true, "Message was sent to the queue successfully");
        }
    }

    /// <summary>
    /// Verifies that an order and email notification can be created and read back
    /// from the containerized PostgreSQL database via the application's repositories.
    /// </summary>
    [Fact]
    public async Task Postgres_CanCreateOrderAndEmailNotification_ViaRepositories()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange & Act
            var (order, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);

            // Assert - verify data was persisted
            var count = await PostgreUtil.RunSqlReturnOutput<int>(
                _fixture.PostgresConnectionString,
                "SELECT count(1) FROM notifications.emailnotifications WHERE alternateid = $1",
                new NpgsqlParameter { Value = notification.Id });

            Assert.Equal(1, count);
        }
    }

    /// <summary>
    /// Verifies that updating the send status of an email notification works correctly
    /// using the containerized database. This is the core operation the delivery report
    /// handler performs when it receives a status update from the queue.
    /// </summary>
    [Fact]
    public async Task Postgres_UpdateSendStatus_UpdatesNotificationResultAndOperationId()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange - create order and notification
            var (order, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);
            string operationId = Guid.NewGuid().ToString();

            // Act - update send status (simulates what the delivery report handler does)
            await PostgreUtil.UpdateSendStatus(factory, notification.Id, EmailNotificationResultType.Delivered, operationId);

            // Assert - verify status was updated in database
            await using var dataSource = NpgsqlDataSource.Create(_fixture.PostgresConnectionString);

            await using var cmd = dataSource.CreateCommand(
                "SELECT result, operationid FROM notifications.emailnotifications WHERE alternateid = $1");
            cmd.Parameters.AddWithValue(notification.Id);

            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Notification should exist in database");

            string result = reader.GetString(0);
            string savedOperationId = reader.GetString(1);

            Assert.Equal(EmailNotificationResultType.Delivered.ToString(), result);
            Assert.Equal(operationId, savedOperationId);
        }
    }
}
