using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// Shared setup for integration tests that exercise sender ownership enforcement with the real
/// <see cref="ServiceOwnerService"/> on top of a mocked repository.
/// </summary>
public static class SenderOwnershipTestUtil
{
    /// <summary>
    /// Creates notification options with the sender ownership enforcement flags set as specified.
    /// </summary>
    /// <param name="enforceEmail">Whether email sender ownership is enforced.</param>
    /// <param name="enforceSms">Whether SMS sender ownership is enforced.</param>
    /// <returns>The configured <see cref="NotificationConfig"/> options.</returns>
    public static IOptions<NotificationConfig> CreateConfig(bool enforceEmail = false, bool enforceSms = false)
    {
        return Options.Create(new NotificationConfig
        {
            DefaultSmsSenderNumber = "Altinn",
            EnforceSmsSenderOwnership = enforceSms,
            EnforceEmailSenderOwnership = enforceEmail,
            DefaultEmailFromAddress = "noreply@altinn.cloud"
        });
    }

    /// <summary>
    /// Creates a <see cref="ServiceOwnerService"/> backed by a mocked repository and loads the given senders into its in-memory lookup.
    /// </summary>
    /// <param name="config">The notification options controlling sender ownership enforcement.</param>
    /// <param name="registeredSenders">The senders the mocked repository returns as registered.</param>
    /// <returns>A service owner service with the senders already loaded.</returns>
    public static async Task<ServiceOwnerService> CreateLoadedServiceOwnerService(IOptions<NotificationConfig> config, params RegisteredSender[] registeredSenders)
    {
        var serviceOwnerRepository = new Mock<IServiceOwnerRepository>();
        serviceOwnerRepository
            .Setup(r => r.GetRegisteredSenders(It.IsAny<CancellationToken>()))
            .ReturnsAsync(registeredSenders);

        var serviceOwnerService = new ServiceOwnerService(serviceOwnerRepository.Object, config, NullLogger<ServiceOwnerService>.Instance);
        await serviceOwnerService.LoadSenders(TestContext.Current.CancellationToken);

        return serviceOwnerService;
    }
}
