using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

/// <summary>
/// Unit tests verifying that all order processing services pass <see cref="OrderLifecycleStage.Processing"/>
/// to the <see cref="IContactPointService"/> when processing orders picked up by the consumer.
/// This ensures the expensive authorization lookup for user-registered contact points
/// is performed during processing, not during order creation.
/// </summary>
public class OrderLifecycleStageProcessingTests
{
    private static readonly Guid _orderId = Guid.NewGuid();
    private static readonly DateTime _requestedSendTime = DateTime.UtcNow;

    [Fact]
    public async Task EmailOrderProcessing_ProcessOrder_UsesProcessingPhase()
    {
        // Arrange
        var order = CreateOrder(NotificationChannel.Email, withOrganizationRecipient: true);

        var contactPointMock = new Mock<IContactPointService>();
        contactPointMock
            .Setup(c => c.AddEmailContactPoints(
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                It.IsAny<OrderLifecycleStage>()))
            .Returns(Task.CompletedTask);

        var service = CreateEmailProcessingService(contactPointMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointMock.Verify(
            c => c.AddEmailContactPoints(
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                OrderLifecycleStage.Processing),
            Times.Once);
    }

    [Fact]
    public async Task SmsOrderProcessing_ProcessOrder_UsesProcessingPhase()
    {
        // Arrange
        var order = CreateOrder(NotificationChannel.Sms, withOrganizationRecipient: true);
        order.Templates = [new SmsTemplate("Altinn", "Test SMS body")];

        var contactPointMock = new Mock<IContactPointService>();
        contactPointMock
            .Setup(c => c.AddSmsContactPoints(
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                It.IsAny<OrderLifecycleStage>()))
            .Returns(Task.CompletedTask);

        var service = CreateSmsProcessingService(contactPointMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointMock.Verify(
            c => c.AddSmsContactPoints(
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                OrderLifecycleStage.Processing),
            Times.Once);
    }

    [Fact]
    public async Task EmailAndSmsOrderProcessing_ProcessOrder_UsesProcessingPhase()
    {
        // Arrange
        var order = CreateOrder(NotificationChannel.EmailAndSms, withOrganizationRecipient: true);
        order.Templates =
        [
            new EmailTemplate("sender@example.com", "Subject", "Body", EmailContentType.Plain),
            new SmsTemplate("Altinn", "Test SMS body")
        ];

        var contactPointMock = new Mock<IContactPointService>();
        contactPointMock
            .Setup(c => c.AddEmailAndSmsContactPointsAsync(
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                It.IsAny<OrderLifecycleStage>()))
            .Returns(Task.CompletedTask);

        var service = CreateEmailAndSmsProcessingService(contactPointMock.Object);

        // Act
        await service.ProcessOrderAsync(order);

        // Assert
        contactPointMock.Verify(
            c => c.AddEmailAndSmsContactPointsAsync(
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                OrderLifecycleStage.Processing),
            Times.Once);
    }

    [Fact]
    public async Task PreferredChannelProcessing_ProcessOrder_EmailPreferred_UsesProcessingPhase()
    {
        // Arrange
        var order = CreateOrder(NotificationChannel.EmailPreferred, withOrganizationRecipient: true);
        order.Templates =
        [
            new EmailTemplate("sender@example.com", "Subject", "Body", EmailContentType.Plain),
            new SmsTemplate("Altinn", "Test SMS body")
        ];

        var contactPointMock = new Mock<IContactPointService>();
        contactPointMock
            .Setup(c => c.AddPreferredContactPoints(
                It.IsAny<NotificationChannel>(),
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                It.IsAny<OrderLifecycleStage>()))
            .Returns(Task.CompletedTask);

        var service = CreatePreferredChannelProcessingService(contactPointMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointMock.Verify(
            c => c.AddPreferredContactPoints(
                NotificationChannel.EmailPreferred,
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                OrderLifecycleStage.Processing),
            Times.Once);
    }

    [Fact]
    public async Task PreferredChannelProcessing_ProcessOrder_SmsPreferred_UsesProcessingPhase()
    {
        // Arrange
        var order = CreateOrder(NotificationChannel.SmsPreferred, withOrganizationRecipient: true);
        order.Templates =
        [
            new EmailTemplate("sender@example.com", "Subject", "Body", EmailContentType.Plain),
            new SmsTemplate("Altinn", "Test SMS body")
        ];

        var contactPointMock = new Mock<IContactPointService>();
        contactPointMock
            .Setup(c => c.AddPreferredContactPoints(
                It.IsAny<NotificationChannel>(),
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                It.IsAny<OrderLifecycleStage>()))
            .Returns(Task.CompletedTask);

        var service = CreatePreferredChannelProcessingService(contactPointMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointMock.Verify(
            c => c.AddPreferredContactPoints(
                NotificationChannel.SmsPreferred,
                It.IsAny<List<Recipient>>(),
                It.IsAny<string?>(),
                OrderLifecycleStage.Processing),
            Times.Once);
    }

    /// <summary>
    /// Creates a notification order with an organization recipient that has no contact points,
    /// forcing the processing service to call the contact point service.
    /// </summary>
    private static NotificationOrder CreateOrder(NotificationChannel channel, bool withOrganizationRecipient = false)
    {
        return new NotificationOrder
        {
            Id = _orderId,
            NotificationChannel = channel,
            RequestedSendTime = _requestedSendTime,
            ResourceId = "urn:altinn:resource:5201C0F33A8C",
            Templates = [new EmailTemplate("sender@example.com", "Subject", "Body", EmailContentType.Plain)],
            Recipients =
            [
                withOrganizationRecipient
                    ? new Recipient([], organizationNumber: "987654321")
                    : new Recipient([], nationalIdentityNumber: "29105573746")
            ]
        };
    }

    private static EmailOrderProcessingService CreateEmailProcessingService(IContactPointService contactPointService)
    {
        var keywordsServiceMock = new Mock<IKeywordsService>();
        keywordsServiceMock
            .Setup(e => e.ReplaceKeywordsAsync(It.IsAny<IEnumerable<EmailRecipient>>()))
            .ReturnsAsync((IEnumerable<EmailRecipient> r) => r);

        return new EmailOrderProcessingService(
            new Mock<IEmailNotificationRepository>().Object,
            new Mock<IEmailNotificationService>().Object,
            contactPointService,
            keywordsServiceMock.Object);
    }

    private static SmsOrderProcessingService CreateSmsProcessingService(IContactPointService contactPointService)
    {
        var keywordsServiceMock = new Mock<IKeywordsService>();
        keywordsServiceMock
            .Setup(e => e.ReplaceKeywordsAsync(It.IsAny<List<SmsRecipient>>()))
            .ReturnsAsync((List<SmsRecipient> r) => r);

        var scheduleMock = new Mock<INotificationScheduleService>();
        scheduleMock
            .Setup(e => e.GetSmsExpirationDateTime(It.IsAny<DateTime>()))
            .Returns((DateTime dt) => dt.AddHours(48));

        return new SmsOrderProcessingService(
            keywordsServiceMock.Object,
            new Mock<ISmsNotificationService>().Object,
            contactPointService,
            new Mock<ISmsNotificationRepository>().Object,
            scheduleMock.Object);
    }

    private static EmailAndSmsOrderProcessingService CreateEmailAndSmsProcessingService(IContactPointService contactPointService)
    {
        return new EmailAndSmsOrderProcessingService(
            CreateEmailProcessingService(contactPointService),
            CreateSmsProcessingService(contactPointService),
            contactPointService);
    }

    private static PreferredChannelProcessingService CreatePreferredChannelProcessingService(IContactPointService contactPointService)
    {
        return new PreferredChannelProcessingService(
            CreateEmailProcessingService(contactPointService),
            CreateSmsProcessingService(contactPointService),
            contactPointService);
    }
}
