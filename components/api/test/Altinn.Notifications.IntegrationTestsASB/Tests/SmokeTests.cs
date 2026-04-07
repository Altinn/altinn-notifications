using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.IntegrationTestsASB.Utils;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

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
    /// Verifies that updating the send status of an email notification works correctly
    /// using the containerized database. This is the core operation the delivery report
    /// handler performs when it receives a status update from the queue.
    /// </summary>
    [Fact]
    public async Task Postgres_UpdateSendStatus_UpdatesNotificationResultAndOperationId()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableWolverine", "false")
            .Initialize();

        await using (factory)
        {
            // Arrange - create order and notification
            var (_, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);
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
