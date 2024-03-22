using System;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class OrderRequestServiceTests
{
    [Fact]
    public async Task RegisterNotificationOrder_ForEmail_ExpectedInputToRepository()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime createdTime = DateTime.UtcNow.AddMinutes(-2);
        Guid id = Guid.NewGuid();

        NotificationOrder expectedRepoInput = new()
        {
            Id = id,
            Created = createdTime,
            Creator = new("ttd"),
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = sendTime,
            Recipients = { },
            SendersReference = "senders-reference",
            Templates = { new EmailTemplate { Body = "email-body", FromAddress = "dontreply@skatteetaten.no" } }
        };
      
        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.Email,
            Recipients = { },
            SendersReference = "senders-reference",
            RequestedSendTime = sendTime,
            Templates = { new EmailTemplate { Body = "email-body", FromAddress = "dontreply@skatteetaten.no" } }
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(o => Assert.Equivalent(expectedRepoInput, o))
            .ReturnsAsync((NotificationOrder order) => order);

        var service = GetTestService(repoMock.Object, null, id, createdTime);

        // Act                    
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert
        Assert.Equal(expectedRepoInput.Id, actual.OrderId);
        repoMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrder_ForEmail_NoFromAddressDefaultInserted()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime createdTime = DateTime.UtcNow.AddMinutes(-2);
        Guid id = Guid.NewGuid();

        NotificationOrder expectedRepoInput = new()
        {
            Id = id,
            Created = createdTime,
            Creator = new("ttd"),
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = sendTime,
            Recipients = { },
            SendersReference = "senders-reference",
            Templates = { new EmailTemplate { Body = "email-body", FromAddress = "noreply@altinn.no" } }
        };

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.Email,
            Recipients = { },
            SendersReference = "senders-reference",
            RequestedSendTime = sendTime,
            Templates = { new EmailTemplate { Body = "email-body" } }
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(o => Assert.Equivalent(expectedRepoInput, o))
            .ReturnsAsync((NotificationOrder order) => order);

        var service = GetTestService(repoMock.Object, null, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert        
        Assert.Equal(expectedRepoInput.Id, actual.OrderId);
        repoMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrder_ForSms_ExpectedInputToRepository()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime createdTime = DateTime.UtcNow.AddMinutes(-2);
        Guid id = Guid.NewGuid();

        NotificationOrder expectedRepoInput = new()
        {
            Id = id,
            Created = createdTime,
            Creator = new("ttd"),
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = sendTime,
            Recipients = { },
            SendersReference = "senders-reference",
            Templates = { new SmsTemplate { Body = "sms-body", SenderNumber = "Skatteetaten" } }
        };

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.Sms,
            Recipients = { },
            SendersReference = "senders-reference",
            RequestedSendTime = sendTime,
            Templates = { new SmsTemplate { Body = "sms-body", SenderNumber = "Skatteetaten" } }
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(o => Assert.Equivalent(expectedRepoInput, o))
            .ReturnsAsync((NotificationOrder order) => order);

        var service = GetTestService(repoMock.Object, null, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert        
        Assert.Equal(expectedRepoInput.Id, actual.OrderId);
        repoMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrder_ForSms_NoSenderNumberDefaultInserted()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime createdTime = DateTime.UtcNow.AddMinutes(-2);
        Guid id = Guid.NewGuid();

        NotificationOrder expectedRepoInput = new()
        {
            Id = id,
            Created = createdTime,
            Creator = new("ttd"),
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = sendTime,
            Recipients = { },
            SendersReference = "senders-reference",
            Templates = { new SmsTemplate { Body = "sms-body", SenderNumber = "TestDefaultSmsSenderNumberNumber" } }
        };

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.Sms,
            Recipients = { },
            SendersReference = "senders-reference",
            RequestedSendTime = sendTime,
            Templates = { new SmsTemplate { Body = "sms-body" } }
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(o => Assert.Equivalent(expectedRepoInput, o))
            .ReturnsAsync((NotificationOrder order) => order);

        var service = GetTestService(repoMock.Object, null, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert        
        Assert.Equal(expectedRepoInput.Id, actual.OrderId);
        repoMock.VerifyAll();
    }

    public static OrderRequestService GetTestService(IOrderRepository? repository = null, IContactPointService? contactPointService = null, Guid? guid = null, DateTime? dateTime = null)
    {
        if (repository == null)
        {
            var repo = new Mock<IOrderRepository>();
            repository = repo.Object;
        }

        var guidMock = new Mock<IGuidService>();
        guidMock.Setup(g => g.NewGuid())
            .Returns(guid ?? Guid.NewGuid());

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(g => g.UtcNow())
            .Returns(dateTime ?? DateTime.UtcNow);

        if (contactPointService == null)
        {
            var contactService = new Mock<IContactPointService>();
            contactPointService = contactService.Object;
        }

        var config = Options.Create<NotificationOrderConfig>(new()
        {
            DefaultEmailFromAddress = "noreply@altinn.no",
            DefaultSmsSenderNumber = "TestDefaultSmsSenderNumberNumber"
        });
        return new OrderRequestService(repository, contactPointService, guidMock.Object, dateTimeMock.Object, config);
    }
}
