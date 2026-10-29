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
    public async Task ValidateSenders_MatchingIsCaseSensitive()
    {
        var service = await CreateLoadedService(true, false);

        Assert.NotNull(service.ValidateSenders(_owner, ["OK@digdir.no"], []));
    }

    [Fact]
    public async Task ValidateSenders_ServiceOwnerMatchingIsCaseInsensitive()
    {
        var service = await CreateLoadedService(true, false);

        Assert.Null(service.ValidateSenders(_owner.ToUpperInvariant(), ["ok@digdir.no"], []));
    }

    [Fact]
    public async Task ValidateSenders_WhitespaceSender_Rejected()
    {
        var service = await CreateLoadedService(true, false);

        Assert.NotNull(service.ValidateSenders(_owner, [" "], []));
    }

    [Fact]
    public void ValidateSenders_FlagsOff_DoesNotRequireLoadedSenders()
    {
        var service = CreateUnloadedService(false, false);

        Assert.Null(service.ValidateSenders(_owner, ["x@y.no"], ["Unknown"]));
    }

    [Fact]
    public async Task ValidateSenders_UnregisteredSms_RejectedAndNamed()
    {
        var service = await CreateLoadedService(false, true);

        var problem = service.ValidateSenders(_owner, ["anything@x.no"], ["Evil"]);

        Assert.NotNull(problem);
        Assert.Contains("'Evil'", problem.Detail);
    }

    [Fact]
    public async Task ValidateSenders_FlagsOff_AcceptsUnregisteredSenders()
    {
        var service = await CreateLoadedService(false, false);

        Assert.Null(service.ValidateSenders(_owner, ["x@y.no"], ["Unknown"]));
    }

    [Fact]
    public async Task LoadRegisteredSenders_CalledAgain_ReplacesPreviousData()
    {
        var repository = new Mock<IServiceOwnerRepository>();
        repository
            .SetupSequence(r => r.GetRegisteredSenders(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RegisteredSender(_owner, SenderAddressType.Email, "old@digdir.no")])
            .ReturnsAsync([new RegisteredSender(_owner, SenderAddressType.Email, "new@digdir.no")]);
        var service = CreateService(true, false, repository);

        await service.LoadSenders(TestContext.Current.CancellationToken);
        await service.LoadSenders(TestContext.Current.CancellationToken);

        Assert.Null(service.ValidateSenders(_owner, ["new@digdir.no"], []));
        Assert.NotNull(service.ValidateSenders(_owner, ["old@digdir.no"], []));
    }

    [Fact]
    public async Task ValidateSenders_NullDefaultAndRegisteredSenders_Accepted()
    {
        var service = await CreateLoadedService(true, true);

        Assert.Null(service.ValidateSenders(_owner, [null, "noreply@altinn.cloud", "ok@digdir.no"], [null, "Altinn", "Digdir"]));
    }

    [Fact]
    public async Task ValidateSenders_UnregisteredEmails_RejectedAndAllListed()
    {
        var service = await CreateLoadedService(true, false);

        var problem = service.ValidateSenders(_owner, ["bad1@x.no", "ok@digdir.no", "bad2@x.no", "bad1@x.no"], []);

        Assert.NotNull(problem);
        Assert.Contains("'bad1@x.no', 'bad2@x.no'", problem.Detail);
        Assert.DoesNotContain("ok@digdir.no", problem.Detail);
    }

    [Fact]
    public async Task ValidateSenders_PaddedRegisteredSender_AcceptedAfterTrim()
    {
        var service = await CreateLoadedService(true, false);

        Assert.Null(service.ValidateSenders(_owner, [" ok@digdir.no "], []));
    }

    [Fact]
    public void ValidateSenders_SendersNotLoaded_ThrowsInvalidOperationException()
    {
        var service = CreateUnloadedService(true, false);

        Assert.Throws<InvalidOperationException>(() => service.ValidateSenders(_owner, ["a@x.no"], []));
    }

    [Fact]
    public async Task ValidateSenders_SenderRegisteredForOtherOwnerOrType_Rejected()
    {
        var service = await CreateLoadedService(true, true);

        Assert.NotNull(service.ValidateSenders(_owner, ["Digdir"], []));
        Assert.NotNull(service.ValidateSenders(_owner, [], ["ok@digdir.no"]));
        Assert.NotNull(service.ValidateSenders(_owner, ["other@other.no"], []));
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
                new(_owner, SenderAddressType.Sms, "Digdir"),
                new(_owner, SenderAddressType.Email, "ok@digdir.no"),
                new("other", SenderAddressType.Email, "other@other.no")
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
}
