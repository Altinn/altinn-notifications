using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// Shared setup for integration tests that exercise sender ownership enforcement with the real
/// <see cref="ServiceOwnerService"/> backed by the test database.
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

    public static async Task<ServiceOwnerService> CreateLoadedServiceOwnerService(IOptions<NotificationConfig> config)
    {
        var serviceOwnerRepository = (ServiceOwnerRepository)ServiceUtil
            .GetServices([typeof(IServiceOwnerRepository)])
            .First(i => i.GetType() == typeof(ServiceOwnerRepository));

        var serviceOwnerService = new ServiceOwnerService(serviceOwnerRepository, config, NullLogger<ServiceOwnerService>.Instance);
        await serviceOwnerService.LoadSenders(TestContext.Current.CancellationToken);

        return serviceOwnerService;
    }

    public static Mock<IGuidService> CreateGuidService()
    {
        var guidService = new Mock<IGuidService>();
        guidService.Setup(g => g.NewGuid()).Returns(Guid.NewGuid);

        return guidService;
    }

    public static Mock<IDateTimeService> CreateDateTimeService()
    {
        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService.Setup(d => d.UtcNow()).Returns(DateTime.UtcNow);

        return dateTimeService;
    }
}
