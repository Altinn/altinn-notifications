using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Moq;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

[Collection(nameof(IntegrationTestContainersCollection))]
public class EmailServiceRateLimitHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    [Fact]
    public async Task EmailServiceRateLimit_WhenValidCommand_CallsServiceWithCorrectArguments()
    {
        // Arrange
        string capturedSource = string.Empty;
        AltinnServiceUpdateSchema capturedSchema = default;
        string capturedData = string.Empty;

        var mockService = new Mock<IAltinnServiceUpdateService>();
        mockService
            .Setup(s => s.HandleServiceUpdate(It.IsAny<string>(), It.IsAny<AltinnServiceUpdateSchema>(), It.IsAny<string>()))
            .Callback<string, AltinnServiceUpdateSchema, string>((src, schema, data) =>
            {
                capturedData = data;
                capturedSource = src;
                capturedSchema = schema;
            })
            .Returns(Task.CompletedTask);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailServiceRateLimitQueueName;

            var command = new EmailServiceRateLimitCommand
            {
                Source = "Platform-Notifications-Email",
                Data = """{"resource":"azure-communication-services-email","resetTime":"2026-01-01T00:00:00Z"}"""
            };

            // Act
            await factory.SendToQueueAsync(queueName, command);

            // Assert
            var handlerCalled = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(mockService.Invocations.Count > 0),
                maxAttempts: 20,
                delayMs: 500);

            Assert.True(handlerCalled, "IAltinnServiceUpdateService.HandleServiceUpdate should have been called");

            mockService.Verify(
                s => s.HandleServiceUpdate(
                    It.IsAny<string>(),
                    It.IsAny<AltinnServiceUpdateSchema>(),
                    It.IsAny<string>()),
                Times.Once);

            Assert.Equal(command.Data, capturedData);
            Assert.Equal("platform-notifications-email", capturedSource);
            Assert.Equal(AltinnServiceUpdateSchema.ResourceLimitExceeded, capturedSchema);
        }
    }

    [Fact]
    public async Task EmailServiceRateLimit_WhenDatabaseThrowsNpgsqlException_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange
        int attemptCount = 0;
        var mockService = new Mock<IAltinnServiceUpdateService>();
        mockService
            .Setup(s => s.HandleServiceUpdate(It.IsAny<string>(), It.IsAny<AltinnServiceUpdateSchema>(), It.IsAny<string>()))
            .Callback(() => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new NpgsqlException("Simulated database error"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailServiceRateLimitQueueName;
            var policy = factory.WolverineSettings.EmailServiceRateLimitQueuePolicy;

            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;

            // Act
            await factory.SendToQueueAsync(queueName, new EmailServiceRateLimitCommand
            {
                Source = "platform-notifications-email",
                Data = "{}"
            });

            // Assert
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(30));
            Assert.NotNull(deadLetterMessage);

            await WaitForUtils.WaitForAsync(() => Task.FromResult(attemptCount >= expectedAttempts), maxAttempts: 10, delayMs: 200);

            Console.WriteLine($"[Test] Handler was called {attemptCount} times (expected {expectedAttempts})");
            Assert.Equal(expectedAttempts, attemptCount);
        }
    }
}
