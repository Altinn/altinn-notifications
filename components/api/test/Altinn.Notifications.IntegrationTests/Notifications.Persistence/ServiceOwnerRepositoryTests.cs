using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class ServiceOwnerRepositoryTests : IAsyncLifetime
{
    private readonly string _owner = $"test-owner-{Guid.NewGuid():N}";

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await PostgreUtil.RunSql(
            "DELETE FROM notifications.serviceowner WHERE lower(serviceowner) = @owner",
            new NpgsqlParameter("@owner", _owner));
    }

    [Fact]
    public async Task WhenGettingRegisteredSenders_ThenSendersAreReturnedWithMappedAddressType()
    {
        // Arrange
        ServiceOwnerRepository sut = CreateRepository();

        string sms = $"S{Guid.NewGuid():N}"[..11];
        string email = $"{Guid.NewGuid():N}@test.no";

        await PostgreUtil.InsertServiceOwnerSender(_owner, "SMS", sms);
        await PostgreUtil.InsertServiceOwnerSender(_owner, "Email", email);

        // Act
        var result = await sut.GetRegisteredSenders(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, s => s.ServiceOwner == _owner && s.AddressType == AddressType.Sms && s.SenderAddress == sms);
        Assert.Contains(result, s => s.ServiceOwner == _owner && s.AddressType == AddressType.Email && s.SenderAddress == email);
    }

    [Fact]
    public async Task WhenInsertingSameSenderWithDifferentCasing_ThenUniqueViolationIsThrown()
    {
        // Arrange
        string email = $"{Guid.NewGuid():N}@test.no";
        await PostgreUtil.InsertServiceOwnerSender(_owner, "Email", email);

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => PostgreUtil.InsertServiceOwnerSender(_owner, "Email", email.ToUpperInvariant()));

        // Assert
        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
    }

    [Fact]
    public async Task WhenInsertingSenderWithNonLowercaseServiceOwner_ThenCheckViolationIsThrown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => PostgreUtil.InsertServiceOwnerSender(_owner.ToUpperInvariant(), "Email", $"{Guid.NewGuid():N}@test.no"));

        // Assert
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    private static ServiceOwnerRepository CreateRepository()
    {
        return (ServiceOwnerRepository)ServiceUtil
            .GetServices([typeof(IServiceOwnerRepository)])
            .First(i => i.GetType() == typeof(ServiceOwnerRepository));
    }
}
