using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class OrderProcessingServiceTests
{
    private const string _pastDueTopicName = "orders.pastdue";

    [Fact]
    public async Task StartProcessingPastDueOrders_ProducerCalledOnceForEachOrder()
    {
        // Arrange 
        NotificationOrder order = new();

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.GetPastDueOrdersAndSetProcessingState())
        .ReturnsAsync(new List<NotificationOrder>() { order, order, order, order });

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_pastDueTopicName)), It.IsAny<string>()));

        var service = GetTestService(repo: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.StartProcessingPastDueOrders();

        // Assert
        repoMock.Verify();
        producerMock.Verify(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_pastDueTopicName)), It.IsAny<string>()), Times.Exactly(4));
    }

    private static OrderProcessingService GetTestService(
        IOrderRepository? repo = null,
        IKafkaProducer? producer = null)
    {
        if (repo == null)
        {
            var repoMock = new Mock<IOrderRepository>();
            repo = repoMock.Object;
        }

        var emailRepoMock = new Mock<IEmailNotificationRepository>();
        var emailServiceMock = new Mock<IEmailNotificationService>();

        var smsRepoMock = new Mock<ISmsNotificationRepository>();
        var smsServiceMock = new Mock<ISmsNotificationService>();

        if (producer == null)
        {
            var producerMock = new Mock<IKafkaProducer>();
            producer = producerMock.Object;
        }

        var kafkaSettings = new Altinn.Notifications.Core.Configuration.KafkaSettings() { PastDueOrdersTopicName = _pastDueTopicName };

        return new OrderProcessingService(repo, emailRepoMock.Object, emailServiceMock.Object, smsRepoMock.Object, smsServiceMock.Object, producer, Options.Create(kafkaSettings));
    }
}
