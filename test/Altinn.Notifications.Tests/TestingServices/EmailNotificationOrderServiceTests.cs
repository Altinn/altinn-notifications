using System;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.TestingServices;
public class EmailNotificationOrderServiceTests
{

    [Fact]
    public async Task RegisterEmailNotificationOrder_ExpectedInputToRepository()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime createdTime = DateTime.UtcNow.AddMinutes(-2);
        string id = Guid.NewGuid().ToString();

        NotificationOrder expected = new()
        {
            Id = id,
            Created = createdTime,
            Creator = new("ttd"),
            NotificationChannel = NotificationChannel.Email,
            SendTime = sendTime,
            Recipients = { },
            SendersReference = "senders-reference",
            Templates = { }
        };

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.Email,
            Recipients = { },
            SendersReference = "senders-reference",
            SendTime = sendTime,
            Templates = { }
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .ReturnsAsync((NotificationOrder order) => order);

        var service = GetTestService(repoMock.Object, id, createdTime);

        //Act
        (NotificationOrder? actual, ServiceError? _) = await service.RegisterEmailNotificationOrder(input);

        // Assert
        Assert.Equivalent(expected, actual, true);
        repoMock.VerifyAll();
    }

    public static EmailNotificationOrderService GetTestService(IOrderRepository? repository = null, string? guid = null, DateTime? dateTime = null)
    {
        if (repository == null)
        {
            var repo = new Mock<IOrderRepository>();
            repository = repo.Object;
        }

        var guidMock = new Mock<IGuidService>();
        guidMock.Setup(g => g.NewGuidAsString())
            .Returns(guid ?? Guid.NewGuid().ToString());

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(g => g.UtcNow())
            .Returns(dateTime ?? DateTime.UtcNow);

        return new EmailNotificationOrderService(repository, guidMock.Object, dateTimeMock.Object);
    }
}
