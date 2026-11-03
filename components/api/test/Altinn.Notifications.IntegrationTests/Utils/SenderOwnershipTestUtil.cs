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
