using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class ServiceOwnerServiceTests
{
    private const string _owner = "digdir";

    [Fact]
    public async Task GetUnregisteredSenders_MatchingIsCaseSensitive()
    {
        var service = await CreateLoadedService(new RegisteredSender(_owner, SenderAddressType.Email, "ok@digdir.no"));

        Assert.Equal(["OK@digdir.no"], service.GetUnregisteredSenders(_owner, SenderAddressType.Email, ["OK@digdir.no"]));
    }

    [Fact]
    public async Task LoadRegisteredSenders_CalledAgain_ReplacesPreviousData()
    {
        var repository = new Mock<IServiceOwnerRepository>();
        repository
            .SetupSequence(r => r.GetRegisteredSenders(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RegisteredSender(_owner, SenderAddressType.Email, "old@digdir.no")])
            .ReturnsAsync([new RegisteredSender(_owner, SenderAddressType.Email, "new@digdir.no")]);
        var service = new ServiceOwnerService(repository.Object);

        await service.LoadRegisteredSenders(TestContext.Current.CancellationToken);
        await service.LoadRegisteredSenders(TestContext.Current.CancellationToken);

        Assert.Empty(service.GetUnregisteredSenders(_owner, SenderAddressType.Email, ["new@digdir.no"]));
        Assert.Equal(["old@digdir.no"], service.GetUnregisteredSenders(_owner, SenderAddressType.Email, ["old@digdir.no"]));
    }

    [Fact]
    public async Task GetUnregisteredSenders_OnlyUnregisteredReturned_DuplicatesRemoved()
    {
        var service = await CreateLoadedService(new RegisteredSender(_owner, SenderAddressType.Email, "ok@digdir.no"));

        var result = service.GetUnregisteredSenders(_owner, SenderAddressType.Email, ["bad@x.no", "ok@digdir.no", "bad@x.no"]);

        Assert.Equal(["bad@x.no"], result);
    }

    [Fact]
    public void GetUnregisteredSenders_SendersNotLoaded_ThrowsInvalidOperationException()
    {
        var service = new ServiceOwnerService(new Mock<IServiceOwnerRepository>().Object);

        Assert.Throws<InvalidOperationException>(() => service.GetUnregisteredSenders(_owner, SenderAddressType.Email, ["a"]));
    }

    [Fact]
    public async Task GetUnregisteredSenders_SenderRegisteredForOtherOwnerOrType_IsUnregistered()
    {
        var service = await CreateLoadedService(
            new RegisteredSender("other", SenderAddressType.Email, "a@other.no"),
            new RegisteredSender(_owner, SenderAddressType.Sms, "Digdir"));

        Assert.Empty(service.GetUnregisteredSenders(_owner, SenderAddressType.Sms, ["Digdir"]));
        Assert.Equal(["Digdir"], service.GetUnregisteredSenders(_owner, SenderAddressType.Email, ["Digdir"]));
        Assert.Equal(["a@other.no"], service.GetUnregisteredSenders(_owner, SenderAddressType.Email, ["a@other.no"]));
    }

    private static async Task<ServiceOwnerService> CreateLoadedService(params RegisteredSender[] senders)
    {
        var repository = new Mock<IServiceOwnerRepository>();
        repository
            .Setup(r => r.GetRegisteredSenders(It.IsAny<CancellationToken>()))
            .ReturnsAsync(senders);

        var service = new ServiceOwnerService(repository.Object);
        await service.LoadRegisteredSenders(TestContext.Current.CancellationToken);

        return service;
    }
}
