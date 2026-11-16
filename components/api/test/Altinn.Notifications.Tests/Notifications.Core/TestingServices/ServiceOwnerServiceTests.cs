using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class ServiceOwnerServiceTests
{
    private const string _owner = "digdir";

    [Fact]
    public async Task WhenSenderCasingDiffers_ThenSenderIsAllowed()
    {
        var service = await CreateLoadedService(true, true);

        Assert.True(service.IsSenderAllowed(_owner, AddressType.Sms, "DIGDIR"));
        Assert.True(service.IsSenderAllowed(_owner, AddressType.Sms, "ALTINN"));
        Assert.True(service.IsSenderAllowed(_owner, AddressType.Email, "OK@Digdir.NO"));
        Assert.True(service.IsSenderAllowed(_owner, AddressType.Email, "NoReply@Altinn.Cloud"));
    }

    [Fact]
    public async Task WhenServiceOwnerCasingDiffers_ThenSenderIsAllowed()
    {
        var service = await CreateLoadedService(true, false);

        Assert.True(service.IsSenderAllowed(_owner.ToUpperInvariant(), AddressType.Email, "ok@digdir.no"));
    }

    [Theory]
    [InlineData(AddressType.Email)]
    [InlineData(AddressType.Sms)]
    public async Task WhenEnforcementIsOff_ThenLoadedSendersAreNotRequired(AddressType addressType)
    {
        var service = CreateUnloadedService(false, false);

        Assert.True(service.IsSenderAllowed(_owner, addressType, "Unknown"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task WhenSenderIsNullOrEmptyOrWhitespace_ThenSenderIsTreatedAsNotSpecified(string? sender)
    {
        var service = await CreateLoadedService(true, true);

        Assert.True(service.IsSenderAllowed(_owner, AddressType.Sms, sender));
        Assert.True(service.IsSenderAllowed(_owner, AddressType.Email, sender));
    }

    [Fact]
    public async Task WhenEnforcementIsEnabledForOneChannel_ThenOnlyThatChannelIsEnforced()
    {
        var smsOnly = await CreateLoadedService(false, true);
        var emailOnly = await CreateLoadedService(true, false);

        Assert.False(smsOnly.IsSenderAllowed(_owner, AddressType.Sms, "Unknown"));
        Assert.True(smsOnly.IsSenderAllowed(_owner, AddressType.Email, "bad@x.no"));

        Assert.True(emailOnly.IsSenderAllowed(_owner, AddressType.Sms, "Unknown"));
        Assert.False(emailOnly.IsSenderAllowed(_owner, AddressType.Email, "bad@x.no"));
    }

    [Fact]
    public async Task WhenRegisteredSenderIsPadded_ThenSenderIsNotAllowed()
    {
        var service = await CreateLoadedService(true, false);

        Assert.False(service.IsSenderAllowed(_owner, AddressType.Email, " ok@digdir.no "));
    }

    [Fact]
    public async Task WhenSenderIsRegisteredForOtherOwnerOrType_ThenSenderIsNotAllowed()
    {
        var service = await CreateLoadedService(true, true);

        Assert.False(service.IsSenderAllowed(_owner, AddressType.Email, "Digdir"));
        Assert.False(service.IsSenderAllowed(_owner, AddressType.Sms, "ok@digdir.no"));
        Assert.False(service.IsSenderAllowed(_owner, AddressType.Email, "other@other.no"));
    }

    [Fact]
    public async Task WhenServiceOwnerIsUnknown_ThenSenderIsNotAllowed()
    {
        var service = await CreateLoadedService(true, true);

        Assert.False(service.IsSenderAllowed("unknown", AddressType.Email, "ok@digdir.no"));
    }

    [Fact]
    public async Task WhenSendersAreNotLoaded_ThenInvalidOperationExceptionIsThrown()
    {
        var service = CreateUnloadedService(true, false);

        Assert.Throws<InvalidOperationException>(() => service.IsSenderAllowed(_owner, AddressType.Email, "a@x.no"));
    }

    [Fact]
    public async Task WhenSendersAreLoadedAgain_ThenPreviousSendersAreReplaced()
    {
        var repository = new Mock<IServiceOwnerRepository>();
        repository
            .SetupSequence(r => r.GetRegisteredSenders(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RegisteredSender(_owner, AddressType.Email, "old@digdir.no")])
            .ReturnsAsync([new RegisteredSender(_owner, AddressType.Email, "new@digdir.no")]);
        var service = CreateService(true, false, repository);

        await service.LoadSenders(TestContext.Current.CancellationToken);
        await service.LoadSenders(TestContext.Current.CancellationToken);

        Assert.True(service.IsSenderAllowed(_owner, AddressType.Email, "new@digdir.no"));
        Assert.False(service.IsSenderAllowed(_owner, AddressType.Email, "old@digdir.no"));
    }

    /// <summary>
    /// Creates a service with an empty repository mock that has not loaded any senders, for testing the behavior before loading.
    /// </summary>
    /// <param name="enforceEmail">Whether sender ownership is enforced for email senders.</param>
    /// <param name="enforceSms">Whether sender ownership is enforced for SMS senders.</param>
    /// <returns>The service, which has not loaded any senders.</returns>
    private static ServiceOwnerService CreateUnloadedService(bool enforceEmail, bool enforceSms)
    {
        return CreateService(enforceEmail, enforceSms, new Mock<IServiceOwnerRepository>());
    }

    /// <summary>
    /// Creates a service whose senders have been loaded from a mocked repository.
    /// The repository holds the email sender "ok@digdir.no" and the SMS sender "Digdir" for the test owner, and the email sender "other@other.no" for another owner.
    /// </summary>
    /// <param name="enforceEmail">Whether sender ownership is enforced for email senders.</param>
    /// <param name="enforceSms">Whether sender ownership is enforced for SMS senders.</param>
    /// <returns>A task that completes with the loaded service.</returns>
    private static async Task<ServiceOwnerService> CreateLoadedService(bool enforceEmail, bool enforceSms)
    {
        var repository = new Mock<IServiceOwnerRepository>();
        repository
            .Setup(r => r.GetRegisteredSenders(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new(_owner, AddressType.Sms, "Digdir"),
                new(_owner, AddressType.Email, "ok@digdir.no"),
                new("other", AddressType.Email, "other@other.no")
            ]);

        var service = CreateService(enforceEmail, enforceSms, repository);
        await service.LoadSenders(TestContext.Current.CancellationToken);

        return service;
    }

    /// <summary>
    /// Creates a service on top of the given repository mock, without loading any senders.
    /// The default email sender is "noreply@altinn.cloud" and the default SMS sender is "Altinn".
    /// </summary>
    /// <param name="enforceEmail">Whether sender ownership is enforced for email senders.</param>
    /// <param name="enforceSms">Whether sender ownership is enforced for SMS senders.</param>
    /// <param name="repository">The mocked repository the service loads senders from.</param>
    /// <returns>The service, which has not loaded any senders.</returns>
    private static ServiceOwnerService CreateService(bool enforceEmail, bool enforceSms, Mock<IServiceOwnerRepository> repository)
    {
        var config = Options.Create(new NotificationConfig
        {
            DefaultSmsSenderNumber = "Altinn",
            EnforceSmsSenderOwnership = enforceSms,
            EnforceEmailSenderOwnership = enforceEmail,
            DefaultEmailFromAddress = "noreply@altinn.cloud"
        });

        return new ServiceOwnerService(repository.Object, config, NullLogger<ServiceOwnerService>.Instance);
    }
}
