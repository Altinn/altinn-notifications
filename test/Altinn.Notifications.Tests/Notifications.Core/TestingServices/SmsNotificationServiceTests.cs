using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class SmsNotificationServiceTests
{
    [Fact]
    public async Task CreateNotifications_NewSmsNotification_RepositoryCalledOnce()
    {
        // Arrange 
        var repoMock = new Mock<ISmsNotificationRepository>();
        var guidService = new Mock<IGuidService>();
        guidService
            .Setup(g => g.NewGuid())
            .Returns(Guid.NewGuid());

        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService
            .Setup(d => d.UtcNow())
            .Returns(DateTime.UtcNow);

        var service = new SmsNotificationService(guidService.Object, dateTimeService.Object, repoMock.Object);

        // Act
        await service.CreateNotification(Guid.NewGuid(), DateTime.UtcNow, new Recipient("recipientId", new List<IAddressPoint>() { new SmsAddressPoint("999999999") }));

        // Assert
        repoMock.Verify(r => r.AddNotification(It.IsAny<SmsNotification>(), It.IsAny<DateTime>()), Times.Once);
    }
}
