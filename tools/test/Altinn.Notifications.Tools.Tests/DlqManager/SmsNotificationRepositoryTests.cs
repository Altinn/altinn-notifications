using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.DlqManager.Repositories;
using Altinn.Notifications.Tools.Tests.Infrastructure;

using Npgsql;
using NpgsqlTypes;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.DlqManager;

/// <summary>
/// Integration tests for <see cref="SmsNotificationRepository"/> against a real PostgreSQL database.
/// Each test seeds a minimal order + SMS notification row and cleans up after itself.
/// </summary>
[Collection(nameof(IntegrationContainersCollection))]
public class SmsNotificationRepositoryTests(IntegrationContainersFixture fixture) : IAsyncLifetime
{
    private readonly IntegrationContainersFixture _fixture = fixture;
    private readonly SmsNotificationRepository _repository = new(fixture.DataSource);
    private readonly List<Guid> _notificationIds = [];
    private readonly List<Guid> _orderIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_notificationIds.Count == 0 && _orderIds.Count == 0)
        {
            return;
        }

        await using var cmd = _fixture.DataSource.CreateCommand(
            "DELETE FROM notifications.smsnotifications WHERE alternateid = ANY(@ids);" +
            "DELETE FROM notifications.orders WHERE alternateid = ANY(@orderIds);");
        cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = _notificationIds.ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter("orderIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = _orderIds.ToArray() });
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task GetNotificationStateAsync_WhenNotFound_ReturnsNullTuple()
    {
        var (result, expiryTime, isExpired, resultTime) =
            await _repository.GetNotificationStateAsync(Guid.NewGuid());

        Assert.Null(result);
        Assert.Null(expiryTime);
        Assert.False(isExpired);
        Assert.Null(resultTime);
    }

    [Fact]
    public async Task GetNotificationStateAsync_WhenSendingAndNotExpired_ReturnsIsExpiredFalse()
    {
        var (notificationId, _) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(1));

        var (result, expiryTime, isExpired, _) =
            await _repository.GetNotificationStateAsync(notificationId);

        Assert.Equal("Sending", result);
        Assert.NotNull(expiryTime);
        Assert.False(isExpired);
    }

    [Fact]
    public async Task GetNotificationStateAsync_WhenSendingAndExpired_ReturnsIsExpiredTrue()
    {
        var (notificationId, _) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));

        var (result, expiryTime, isExpired, _) =
            await _repository.GetNotificationStateAsync(notificationId);

        Assert.Equal("Sending", result);
        Assert.NotNull(expiryTime);
        Assert.True(isExpired);
    }

    [Fact]
    public async Task GetNotificationStateAsync_WhenResultIsAccepted_ReturnsAcceptedAndNotExpired()
    {
        var (notificationId, _) = await SeedNotificationAsync("Accepted", DateTime.UtcNow.AddHours(1));

        var (result, _, isExpired, _) =
            await _repository.GetNotificationStateAsync(notificationId);

        Assert.Equal("Accepted", result);
        Assert.False(isExpired);
    }

    // ── GetNotificationStatesAsync ────────────────────────────────────────────
    [Fact]
    public async Task GetNotificationStatesAsync_WhenEmptyList_ReturnsEmptyDictionary()
    {
        var result = await _repository.GetNotificationStatesAsync([]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNotificationStatesAsync_WhenNoIdsMatch_ReturnsEmptyDictionary()
    {
        var result = await _repository.GetNotificationStatesAsync([Guid.NewGuid(), Guid.NewGuid()]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNotificationStatesAsync_WhenSingleId_ReturnsMatchingEntry()
    {
        var (notificationId, _) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(1));

        var result = await _repository.GetNotificationStatesAsync([notificationId]);

        Assert.Single(result);
        var (res, expiryTime, isExpired, _) = result[notificationId];
        Assert.Equal("Sending", res);
        Assert.NotNull(expiryTime);
        Assert.False(isExpired);
    }

    [Fact]
    public async Task GetNotificationStatesAsync_WhenMultipleIds_ReturnsAllMatching()
    {
        var (id1, _) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(1));
        var (id2, _) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));
        var (id3, _) = await SeedNotificationAsync("Accepted", DateTime.UtcNow.AddHours(1));

        var result = await _repository.GetNotificationStatesAsync([id1, id2, id3]);

        Assert.Equal(3, result.Count);
        Assert.Equal("Sending", result[id1].Result);
        Assert.False(result[id1].IsExpired);
        Assert.Equal("Sending", result[id2].Result);
        Assert.True(result[id2].IsExpired);
        Assert.Equal("Accepted", result[id3].Result);
    }

    [Fact]
    public async Task GetNotificationStatesAsync_WhenSomeIdsNotFound_ReturnsOnlyExisting()
    {
        var (notificationId, _) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(1));
        var absentId = Guid.NewGuid();

        var result = await _repository.GetNotificationStatesAsync([notificationId, absentId]);

        Assert.Single(result);
        Assert.True(result.ContainsKey(notificationId));
        Assert.False(result.ContainsKey(absentId));
    }

    // ── UpdateResultToAcceptedAsync ───────────────────────────────────────────
    [Fact]
    public async Task UpdateResultToAcceptedAsync_WhenSendingAndExpired_UpdatesToAcceptedAndReturnsOne()
    {
        var (notificationId, _) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(-1));

        int rows = await _repository.UpdateResultToAcceptedAsync(notificationId);

        Assert.Equal(1, rows);
        var (result, _, _, resultTime) = await _repository.GetNotificationStateAsync(notificationId);
        Assert.Equal("Accepted", result);
        Assert.NotNull(resultTime);
    }

    [Fact]
    public async Task UpdateResultToAcceptedAsync_WhenSendingButNotExpired_ReturnsZeroWithoutUpdate()
    {
        var (notificationId, _) = await SeedNotificationAsync("Sending", DateTime.UtcNow.AddHours(1));

        int rows = await _repository.UpdateResultToAcceptedAsync(notificationId);

        Assert.Equal(0, rows);
        var (result, _, _, _) = await _repository.GetNotificationStateAsync(notificationId);
        Assert.Equal("Sending", result);
    }

    [Fact]
    public async Task UpdateResultToAcceptedAsync_WhenResultIsNotSending_ReturnsZeroWithoutUpdate()
    {
        var (notificationId, _) = await SeedNotificationAsync("Accepted", DateTime.UtcNow.AddHours(-1));

        int rows = await _repository.UpdateResultToAcceptedAsync(notificationId);

        Assert.Equal(0, rows);
        var (result, _, _, _) = await _repository.GetNotificationStateAsync(notificationId);
        Assert.Equal("Accepted", result);
    }

    [Fact]
    public async Task UpdateResultToAcceptedAsync_WhenNotFound_ReturnsZero()
    {
        int rows = await _repository.UpdateResultToAcceptedAsync(Guid.NewGuid());

        Assert.Equal(0, rows);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<(Guid NotificationId, Guid OrderId)> SeedNotificationAsync(
        string result,
        DateTime expiryTime)
    {
        var orderId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        _orderIds.Add(orderId);
        _notificationIds.Add(notificationId);

        await using var insertOrder = _fixture.DataSource.CreateCommand("""
            INSERT INTO notifications.orders
                (alternateid, creatorname, sendersreference, created, requestedsendtime, notificationorder)
            VALUES (@orderId, 'dlq-test', 'dlq-test-ref', NOW(), NOW(), '{}')
            RETURNING _id
            """);
        insertOrder.Parameters.Add(new NpgsqlParameter("orderId", NpgsqlDbType.Uuid) { Value = orderId });
        long orderDbId = (long)(await insertOrder.ExecuteScalarAsync())!;

        await using var insertNotification = _fixture.DataSource.CreateCommand("""
            INSERT INTO notifications.smsnotifications
                (_orderid, alternateid, mobilenumber, result, resulttime, expirytime)
            VALUES (@orderDbId, @notificationId, '+4712345678',
                    @result::smsnotificationresulttype, NOW(), @expiryTime)
            """);
        insertNotification.Parameters.Add(new NpgsqlParameter("orderDbId", NpgsqlDbType.Bigint) { Value = orderDbId });
        insertNotification.Parameters.Add(new NpgsqlParameter("notificationId", NpgsqlDbType.Uuid) { Value = notificationId });
        insertNotification.Parameters.Add(new NpgsqlParameter("result", NpgsqlDbType.Text) { Value = result });
        insertNotification.Parameters.Add(new NpgsqlParameter("expiryTime", NpgsqlDbType.TimestampTz) { Value = expiryTime });
        await insertNotification.ExecuteNonQueryAsync();

        return (notificationId, orderId);
    }
}
