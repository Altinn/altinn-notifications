using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;
using Altinn.Notifications.Sms.IntegrationTestsASB.Infrastructure;

using Azure.Messaging.ServiceBus;

using Xunit;

namespace Altinn.Notifications.Sms.IntegrationTestsASB.Tests;

/// <summary>
/// Smoke tests to verify the TestContainers-based integration test infrastructure
/// works correctly for the SMS service: containers start, host initializes, and ASB transport is functional.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SmokeTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Verifies that the test infrastructure starts correctly:
    /// ASB containers are running, host initializes, and PostgreSQL is not provisioned.
    /// </summary>
    [Fact]
    public async Task Infrastructure_StartsSuccessfully_HostInitialized()
    {
        Assert.True(_fixture.IsRunning, "Test containers should be running");
        Assert.False(string.IsNullOrEmpty(_fixture.ServiceBusConnectionString), "ServiceBus connection string should be set");
        Assert.True(string.IsNullOrEmpty(_fixture.PostgresConnectionString), "Postgres should not be provisioned for SMS service");

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableWolverine", "false")
            .Initialize();

        await using (factory)
        {
            Assert.NotNull(factory.Host);
        }
    }

    /// <summary>
    /// Verifies that a message can be sent to and received from the smoke-test queue
    /// via the ASB emulator, proving the transport layer works.
    /// </summary>
    [Fact]
    public async Task ServiceBus_CanSendAndReceiveMessage_OnSmokeTestQueue()
    {
        const string queueName = "smoke-test";
        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableWolverine", "false")
            .Initialize();

        await using (factory)
        {
            await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
            await using var sender = client.CreateSender(queueName);

            string testBody = $"{{\"smokeTest\": \"{Guid.NewGuid()}\"}}";
            await sender.SendMessageAsync(new ServiceBusMessage(testBody));

            var received = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(received);
            Assert.Equal(testBody, received.Body.ToString());
        }
    }
}
