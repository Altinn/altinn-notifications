using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Tests.Notifications.Utils;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.SendCondition;

/// <summary>
/// Verifies the whole flow for an email order with a send condition: order creation through the controller,
/// order processing, and the send-time condition evaluation that decides the terminal notification status.
/// </summary>
[Collection(GlobalStateSerialCollection.Name)]
public sealed class SendConditionAtSendTimeTests : IClassFixture<IntegrationTestWebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string _basePath = "/notifications/api/v1/orders/email";

    private readonly IntegrationTestWebApplicationFactory<Program> _factory;
    private readonly List<Guid> _ordersToDelete = [];

    public SendConditionAtSendTimeTests(IntegrationTestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EmailOrder_ConditionNotMet_NotificationIsSendConditionNotMetAndOrderIsCompleted()
    {
        // Arrange
        Uri conditionEndpoint = new($"https://vg.no/condition/{Guid.NewGuid()}");
        Mock<IConditionClient> conditionClient = new();
        conditionClient.Setup(c => c.CheckSendCondition(conditionEndpoint, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _factory.InstallService(conditionClient.Object);

        Guid orderId = await PostOrder(conditionEndpoint);

        // Act
        await ProcessOrderAndSendNotification(orderId);

        // Assert
        Assert.Equal("SendConditionNotMet", await GetNotificationResult(orderId));
        Assert.Equal("Completed", await GetOrderProcessedStatus(orderId));
        Assert.Equal(1, await PostgreUtil.SelectNotificationLogEntryCount(orderId));
        Assert.Equal(1, await PostgreUtil.SelectStatusFeedEntryCount(orderId));
        conditionClient.Verify(c => c.CheckSendCondition(conditionEndpoint, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmailOrder_ConditionMet_NotificationIsSentAndOrderIsNotCompleted()
    {
        // Arrange
        Uri conditionEndpoint = new($"https://vg.no/condition/{Guid.NewGuid()}");
        Mock<IConditionClient> conditionClient = new();
        conditionClient.Setup(c => c.CheckSendCondition(conditionEndpoint, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _factory.InstallService(conditionClient.Object);

        Guid orderId = await PostOrder(conditionEndpoint);

        // Act
        await ProcessOrderAndSendNotification(orderId);

        // Assert
        Assert.Equal("Sending", await GetNotificationResult(orderId));
        Assert.NotEqual("Completed", await GetOrderProcessedStatus(orderId));
        conditionClient.Verify(c => c.CheckSendCondition(conditionEndpoint, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmailOrder_ConditionCheckFailsTwice_RetryIsScheduledThenNotificationIsSentWithoutCondition()
    {
        // Arrange
        Uri conditionEndpoint = new($"https://vg.no/condition/{Guid.NewGuid()}");
        Mock<IConditionClient> conditionClient = new();
        conditionClient
            .Setup(c => c.CheckSendCondition(conditionEndpoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionClientError { StatusCode = 503, Message = "unavailable" });
        _factory.InstallService(conditionClient.Object);

        Guid orderId = await PostOrder(conditionEndpoint);

        // Act: the first inconclusive check schedules a retry and keeps the notification New
        await ProcessOrderAndSendNotification(orderId, expectResolved: false);

        // Assert
        Assert.Equal("New", await GetNotificationResult(orderId));
        Assert.Equal("true", await GetIsRetryScheduledInFuture(orderId));
        conditionClient.Verify(c => c.CheckSendCondition(conditionEndpoint), Times.Once);

        // Act: once the retry is due, a second inconclusive check sends the notification as if no condition was configured
        await MakeRetryDue(orderId);
        await SendNotification(orderId);

        // Assert
        Assert.Equal("Sending", await GetNotificationResult(orderId));
        Assert.NotEqual("Completed", await GetOrderProcessedStatus(orderId));
        conditionClient.Verify(c => c.CheckSendCondition(conditionEndpoint), Times.Exactly(2));
    }

    [Fact]
    public async Task EmailOrder_ConditionCheckFailsThenConditionNotMet_NotificationIsSendConditionNotMet()
    {
        // Arrange
        Uri conditionEndpoint = new($"https://vg.no/condition/{Guid.NewGuid()}");
        Mock<IConditionClient> conditionClient = new();
        conditionClient
            .SetupSequence(c => c.CheckSendCondition(conditionEndpoint))
            .ReturnsAsync(new ConditionClientError { StatusCode = 503, Message = "unavailable" })
            .ReturnsAsync(false);
        _factory.InstallService(conditionClient.Object);

        Guid orderId = await PostOrder(conditionEndpoint);
        await ProcessOrderAndSendNotification(orderId, expectResolved: false);
        Assert.Equal("New", await GetNotificationResult(orderId));

        // Act
        await MakeRetryDue(orderId);
        await SendNotification(orderId);

        // Assert
        Assert.Equal("SendConditionNotMet", await GetNotificationResult(orderId));
        Assert.Equal("Completed", await GetOrderProcessedStatus(orderId));
        conditionClient.Verify(c => c.CheckSendCondition(conditionEndpoint), Times.Exactly(2));
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _factory.ResetInstalledMocks();

        foreach (Guid orderId in _ordersToDelete)
        {
            await PostgreUtil.DeleteNotificationLogFromDb(orderId);
            await PostgreUtil.DeleteStatusFeedFromDb(orderId);
            await PostgreUtil.DeleteOrderFromDb(orderId);
        }
    }

    private static Task<string> GetNotificationResult(Guid orderId)
    {
        return PostgreUtil.RunSqlReturnOutput<string>(
            $"SELECT e.result::text FROM notifications.emailnotifications e JOIN notifications.orders o ON o._id = e._orderid WHERE o.alternateid = '{orderId}'");
    }

    private static Task<string> GetOrderProcessedStatus(Guid orderId)
    {
        return PostgreUtil.RunSqlReturnOutput<string>($"SELECT processedstatus::text FROM notifications.orders WHERE alternateid = '{orderId}'");
    }

    private static Task<string> GetIsRetryScheduledInFuture(Guid orderId)
    {
        return PostgreUtil.RunSqlReturnOutput<string>(
            $"SELECT (e.nextattempt > now())::text FROM notifications.emailnotifications e JOIN notifications.orders o ON o._id = e._orderid WHERE o.alternateid = '{orderId}'");
    }

    private static Task MakeRetryDue(Guid orderId)
    {
        return PostgreUtil.RunSql(
            $"UPDATE notifications.emailnotifications SET nextattempt = now() - interval '1 second' WHERE _orderid = (SELECT _id FROM notifications.orders WHERE alternateid = '{orderId}')");
    }

    private async Task SendNotification(Guid orderId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IEmailNotificationService emailNotificationService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

        // The database is shared with other tests, so claims are repeated until this notification has been handled.
        for (int attempt = 0; attempt < 50 && await GetNotificationResult(orderId) == "New"; attempt++)
        {
            await emailNotificationService.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);
        }
    }

    private async Task ProcessOrderAndSendNotification(Guid orderId, bool expectResolved = true)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IOrderProcessingService orderProcessingService = scope.ServiceProvider.GetRequiredService<IOrderProcessingService>();
        IEmailNotificationService emailNotificationService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

        // The database is shared with other tests, so claims are repeated until this order has been handled.
        for (int attempt = 0; attempt < 50 && await GetOrderProcessedStatus(orderId) == "Registered"; attempt++)
        {
            await orderProcessingService.TryProcessOrder(false, TestContext.Current.CancellationToken);
        }

        Assert.NotEqual("Registered", await GetOrderProcessedStatus(orderId));

        for (int attempt = 0; attempt < 50; attempt++)
        {
            await emailNotificationService.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

            if (expectResolved && await GetNotificationResult(orderId) != "New")
            {
                break;
            }
        }
    }

    private async Task<Guid> PostOrder(Uri conditionEndpoint)
    {
        EmailNotificationOrderRequestExt request = new()
        {
            Body = "email-body",
            Subject = "email-subject",
            RequestedSendTime = DateTime.UtcNow,
            ConditionEndpoint = conditionEndpoint,
            ContentType = EmailContentTypeExt.Plain,
            SendersReference = $"ref-{Guid.NewGuid()}",
            Recipients = [new RecipientExt { EmailAddress = "recipient@domain.com" }]
        };

        HttpClient client = _factory.SharedClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        using StringContent content = new(request.Serialize(), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync(_basePath, content, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode == HttpStatusCode.Accepted, responseBody);

        NotificationOrderRequestResponseExt? receipt = JsonSerializer.Deserialize<NotificationOrderRequestResponseExt>(responseBody);
        Assert.NotNull(receipt);
        Assert.True(receipt.OrderId.HasValue);

        _ordersToDelete.Add(receipt.OrderId.Value);
        return receipt.OrderId.Value;
    }
}
