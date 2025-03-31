using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
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

    [Fact]
    public async Task RegisterNotificationOrder_ForSms_LookupFails_OrderCreated()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime createdTime = DateTime.UtcNow.AddMinutes(-2);
        Guid id = Guid.NewGuid();

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.Sms,
            Recipients = [new Recipient() { NationalIdentityNumber = "16069412345" }],
            SendersReference = "senders-reference",
            RequestedSendTime = sendTime,
            Templates = { new SmsTemplate { Body = "sms-body" } }
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .ReturnsAsync((NotificationOrder order) => order);

        var service = GetTestService(repoMock.Object, null, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert        
        Assert.Equal(RecipientLookupStatus.Failed, actual.RecipientLookup?.Status);
        repoMock.Verify(r => r.Create(It.IsAny<NotificationOrder>()), Times.Once);
    }

    [Fact]
    public async Task RegisterNotificationOrder_ForSms_LookupPartialSuccess_OrderCreated()
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
            Recipients = [
                new Recipient() { NationalIdentityNumber = "16069412345" },
                new Recipient() { NationalIdentityNumber = "14029112345" }
                ],
            Templates = { new SmsTemplate { Body = "sms-body", SenderNumber = "TestDefaultSmsSenderNumberNumber" } }
        };

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.Sms,
            Recipients = [
                new Recipient() { NationalIdentityNumber = "16069412345" },
                new Recipient() { NationalIdentityNumber = "14029112345" }
                ],
            RequestedSendTime = sendTime,
            Templates = { new SmsTemplate { Body = "sms-body" } }
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(o => Assert.Equivalent(expectedRepoInput, o))
            .ReturnsAsync((NotificationOrder order) => order);

        Mock<IContactPointService> contactPointMock = new();
        contactPointMock
            .Setup(cp => cp.AddSmsContactPoints(It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
            .Callback<List<Recipient>, string?>((recipients, _) =>
            {
                foreach (var recipient in recipients)
                {
                    if (recipient.NationalIdentityNumber == "16069412345")
                    {
                        recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                        recipient.IsReserved = false;
                    }
                    else if (recipient.NationalIdentityNumber == "14029112345")
                    {
                        recipient.IsReserved = true;
                    }
                }
            });

        var service = GetTestService(repoMock.Object, contactPointMock.Object, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert        
        Assert.Equal(RecipientLookupStatus.PartialSuccess, actual.RecipientLookup?.Status);
        Assert.Single(actual.RecipientLookup?.IsReserved!);
        repoMock.VerifyAll();
        contactPointMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrder_ForSms_LookupSuccess_OrderCreated()
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
            Recipients = [
                new Recipient() { NationalIdentityNumber = "16069412345" },
            ],
            Templates = { new SmsTemplate { Body = "sms-body", SenderNumber = "TestDefaultSmsSenderNumberNumber" } }
        };

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.Sms,
            Recipients = [
                new Recipient() { NationalIdentityNumber = "16069412345" },
            ],
            RequestedSendTime = sendTime,
            Templates = { new SmsTemplate { Body = "sms-body" } }
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .Callback<NotificationOrder>(o => Assert.Equivalent(expectedRepoInput, o))
            .ReturnsAsync((NotificationOrder order) => order);

        Mock<IContactPointService> contactPointMock = new();
        contactPointMock
            .Setup(cp => cp.AddSmsContactPoints(It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
            .Callback<List<Recipient>, string?>((recipients, _) =>
            {
                foreach (var recipient in recipients)
                {
                    if (recipient.NationalIdentityNumber == "16069412345")
                    {
                        recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                        recipient.IsReserved = false;
                    }
                }
            });

        var service = GetTestService(repoMock.Object, contactPointMock.Object, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert        
        Assert.Equal(RecipientLookupStatus.Success, actual.RecipientLookup?.Status);
        Assert.Equal(0, actual.RecipientLookup!.IsReserved?.Count);
        Assert.Equal(0, actual.RecipientLookup!.MissingContact?.Count);
        repoMock.VerifyAll();
        contactPointMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrder_ForSmsPreferred_LookupFails_OrderCreated()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime createdTime = DateTime.UtcNow.AddMinutes(-2);
        Guid id = Guid.NewGuid();

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.SmsPreferred,
            Recipients = [new Recipient() { NationalIdentityNumber = "1" }],
            SendersReference = "senders-reference",
            RequestedSendTime = sendTime,
            Templates = [
                    new SmsTemplate { Body = "sms-body" },
                new EmailTemplate { Body = "email-body", FromAddress = "noreply@altinn.no" }]
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .ReturnsAsync((NotificationOrder order) => order);

        var service = GetTestService(repoMock.Object, null, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert        
        Assert.Equal(RecipientLookupStatus.Failed, actual.RecipientLookup?.Status);
        repoMock.Verify(r => r.Create(It.IsAny<NotificationOrder>()), Times.Once);
    }

    [Fact]
    public async Task RegisterNotificationOrder_ForSmsPreferred_LookupPartialSuccess_OrderCreated()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime createdTime = DateTime.UtcNow.AddMinutes(-2);
        Guid id = Guid.NewGuid();

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.SmsPreferred,
            Recipients = [
                new Recipient() { NationalIdentityNumber = "1" },
                new Recipient() { NationalIdentityNumber = "2" },
                new Recipient() { NationalIdentityNumber = "3" },
                new Recipient() { NationalIdentityNumber = "4" }

                ],
            RequestedSendTime = sendTime,
            Templates = [
                new SmsTemplate { Body = "sms-body" },
                new EmailTemplate { Body = "email-body", FromAddress = "noreply@altinn.no" }]
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .ReturnsAsync((NotificationOrder order) => order);

        Mock<IContactPointService> contactPointMock = new();
        contactPointMock
            .Setup(cp => cp.AddPreferredContactPoints(input.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
            .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
            {
                foreach (var recipient in recipients)
                {
                    if (recipient.NationalIdentityNumber == "1")
                    {
                        recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                        recipient.IsReserved = false;
                    }
                    else if (recipient.NationalIdentityNumber == "2")
                    {
                        recipient.AddressInfo.Add(new EmailAddressPoint("2@user.com"));
                        recipient.IsReserved = false;
                    }
                    else if (recipient.NationalIdentityNumber == "3")
                    {
                        recipient.IsReserved = true;
                    }
                    else if (recipient.NationalIdentityNumber == "4")
                    {
                        recipient.IsReserved = false;
                    }
                }
            });

        var service = GetTestService(repoMock.Object, contactPointMock.Object, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert        
        Assert.Equal(RecipientLookupStatus.PartialSuccess, actual.RecipientLookup?.Status);
        Assert.Single(actual.RecipientLookup!.IsReserved!);
        Assert.Single(actual.RecipientLookup!.MissingContact!);
        repoMock.VerifyAll();
        contactPointMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrder_ForSmsPreferred_LookupSuccess_OrderCreated()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime createdTime = DateTime.UtcNow.AddMinutes(-2);
        Guid id = Guid.NewGuid();

        NotificationOrderRequest input = new()
        {
            Creator = new Creator("ttd"),

            NotificationChannel = NotificationChannel.SmsPreferred,
            Recipients = [
               new Recipient() { NationalIdentityNumber = "1" },
                new Recipient() { NationalIdentityNumber = "2" }
               ],
            RequestedSendTime = sendTime,
            Templates = [
                   new SmsTemplate { Body = "sms-body" },
                new EmailTemplate { Body = "email-body", FromAddress = "noreply@altinn.no" }]
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .ReturnsAsync((NotificationOrder order) => order);

        Mock<IContactPointService> contactPointMock = new();
        contactPointMock
            .Setup(cp => cp.AddPreferredContactPoints(input.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
            .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
            {
                foreach (var recipient in recipients)
                {
                    if (recipient.NationalIdentityNumber == "1")
                    {
                        recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                        recipient.IsReserved = false;
                    }
                    else if (recipient.NationalIdentityNumber == "2")
                    {
                        recipient.AddressInfo.Add(new EmailAddressPoint("2@user.com"));
                        recipient.IsReserved = false;
                    }
                }
            });

        var service = GetTestService(repoMock.Object, contactPointMock.Object, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert        
        Assert.Equal(RecipientLookupStatus.Success, actual.RecipientLookup?.Status);
        Assert.Equal(0, actual.RecipientLookup!.IsReserved?.Count);
        Assert.Equal(0, actual.RecipientLookup!.MissingContact?.Count);
        repoMock.VerifyAll();
        contactPointMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_RecipientPersonWithMultipleReminders_OrderChainCreated()
    {
        // Arrange
        Guid mainOrderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        Guid firstReminderId = Guid.NewGuid();
        Guid secondReminderId = Guid.NewGuid();
        DateTime mainOrderSendTime = DateTime.UtcNow;

        var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(mainOrderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("skd"))
            .SetSendersReference("TAX-REMINDER-2025")
            .SetRequestedSendTime(mainOrderSendTime.AddDays(1))
            .SetIdempotencyId("84CD3017-92E3-4C3D-80DE-C10338F30813")
            .SetConditionEndpoint(new Uri("https://api.skatteetaten.no/conditions/new"))
            .SetDialogportenAssociation(new DialogportenIdentifiers { DialogId = "20E3D06D5546", TransmissionId = "F9D34BB1C65F" })
            .SetRecipient(new NotificationRecipient
            {
                RecipientPerson = new RecipientPerson
                {
                    IgnoreReservation = true,
                    NationalIdentityNumber = "29105573746",
                    ResourceId = "urn:altinn:resource:tax-2025",
                    ChannelSchema = NotificationChannel.EmailPreferred,

                    EmailSettings = new EmailSendingOptions
                    {
                        Subject = "Tax Filing 2025",
                        SenderName = "Skatteetaten",
                        ContentType = EmailContentType.Html,
                        SendingTimePolicy = SendingTimePolicy.Anytime,
                        SenderEmailAddress = "no-reply@skatteetaten.no",
                        Body = "<p>Log in to <a href=\"https://skatteetaten.no\">Tax Portal</a> to file your return.</p>"
                    },
                    SmsSettings = new SmsSendingOptions
                    {
                        Sender = "Skatteetaten",
                        Body = "Tax filing due: Visit Skatteetaten.no.",
                        SendingTimePolicy = SendingTimePolicy.Daytime
                    }
                }
            })
            .SetReminders(
            [
                new NotificationReminder
                {
                    DelayDays = 7,
                    OrderId = firstReminderId,
                    SendersReference = "TAX-REMINDER-2025-FIRST",
                    RequestedSendTime = mainOrderSendTime.AddDays(7),
                    ConditionEndpoint = new Uri("https://api.skatteetaten.no/conditions/incomplete"),
                    Recipient = new NotificationRecipient
                    {
                        RecipientPerson = new RecipientPerson
                        {
                            IgnoreReservation = true,
                            NationalIdentityNumber = "29105573746",
                            ResourceId = "urn:altinn:resource:tax-2025",
                            ChannelSchema = NotificationChannel.EmailPreferred,
                            EmailSettings = new EmailSendingOptions
                            {
                                SenderName = "Skatteetaten",
                                Subject = "Reminder: Tax 2025",
                                ContentType = EmailContentType.Html,
                                SendingTimePolicy = SendingTimePolicy.Anytime,
                                SenderEmailAddress = "no-reply@skatteetaten.no",
                                Body = "<p><strong>Reminder:</strong> File your return at <a href=\"https://skatteetaten.no\">Tax Portal</a>.</p>"
                            },
                            SmsSettings = new SmsSendingOptions
                            {
                                Sender = "Skatteetaten",
                                SendingTimePolicy = SendingTimePolicy.Daytime,
                                Body = "Reminder: File your tax return at Skatteetaten.no."
                            }
                        }
                    }
                },
                new NotificationReminder
                {
                    DelayDays = 14,
                    OrderId = secondReminderId,
                    SendersReference = "TAX-REMINDER-2025-FINAL",
                    RequestedSendTime = mainOrderSendTime.AddDays(14),
                    ConditionEndpoint = new Uri("https://api.Skatteetaten.no/conditions/incomplete"),
                    Recipient = new NotificationRecipient
                    {
                        RecipientPerson = new RecipientPerson
                        {
                            IgnoreReservation = true,
                            NationalIdentityNumber = "29105573746",
                            ResourceId = "urn:altinn:resource:tax-2025",
                            ChannelSchema = NotificationChannel.SmsPreferred,
                            EmailSettings = new EmailSendingOptions
                            {
                                SenderName = "Skatteetaten",
                                ContentType = EmailContentType.Html,
                                Subject = "Final Reminder: Tax 2025",
                                SendingTimePolicy = SendingTimePolicy.Anytime,
                                SenderEmailAddress = "no-reply@Skatteetaten.no",
                                Body = "<p><strong>Final Reminder:</strong> File now to avoid penalties. <a href=\"https://skatteetaten.no\">Tax Portal</a></p>"
                            },
                            SmsSettings = new SmsSendingOptions
                            {
                                Sender = "Skatteetaten",
                                SendingTimePolicy = SendingTimePolicy.Daytime,
                                Body = "Urgent: File your tax return now at Skatteetaten.no."
                            }
                        }
                    }
                }
            ])
            .Build();

        // Expected orders
        var expectedMainOrder = new NotificationOrder(
            mainOrderId,
            "TAX-REMINDER-2025",
            [
                new SmsTemplate("Skatteetaten", "Tax filing due: Visit Skatteetaten.no."),
                new EmailTemplate("no-reply@skatteetaten.no", "Tax Filing 2025", "<p>Log in to <a href=\"https://skatteetaten.no\">Tax Portal</a> to file your return.</p>", EmailContentType.Html)
            ],
            mainOrderSendTime.AddDays(1),
            NotificationChannel.EmailPreferred,
            new Creator("skd"),
            DateTime.UtcNow,
            [new([], "29105573746")],
            true,
            "urn:altinn:resource:tax-2025",
            new Uri("https://api.skatteetaten.no/conditions/new"));

        var expectedFirstReminder = new NotificationOrder(
            firstReminderId,
            "TAX-REMINDER-2025-FIRST",
            [
                new SmsTemplate("Skatteetaten", "Reminder: File your tax return at Skatteetaten.no."),
                new EmailTemplate("no-reply@skatteetaten.no", "Reminder: Tax 2025", "<p><strong>Reminder:</strong> File your return at <a href=\"https://skatteetaten.no\">Tax Portal</a>.</p>", EmailContentType.Html)
            ],
            mainOrderSendTime.AddDays(7),
            NotificationChannel.EmailPreferred,
            new Creator("skd"),
            DateTime.UtcNow,
            [new([], "29105573746")],
            true,
            "urn:altinn:resource:tax-2025",
            new Uri("https://api.skatteetaten.no/conditions/incomplete"));

        var expectedFinalReminder = new NotificationOrder(
            secondReminderId,
            "TAX-REMINDER-2025-FINAL",
            [
                new SmsTemplate("Skatteetaten", "Urgent: File your tax return now at Skatteetaten.no."),
                new EmailTemplate("no-reply@skatteetaten.no", "Final Reminder: Tax 2025", "<p><strong>Final Reminder:</strong> File now to avoid penalties. <a href=\"https://skatteetaten.no\">Tax Portal</a></p>", EmailContentType.Html)
            ],
            mainOrderSendTime.AddDays(14),
            NotificationChannel.SmsPreferred,
            new Creator("skd"),
            DateTime.UtcNow,
            [new([], "29105573746")],
            true,
            "urn:altinn:resource:tax-2025",
            new Uri("https://api.Skatteetaten.no/conditions/incomplete"));

        var orderRepositoryMock = new Mock<IOrderRepository>();
        var contactPointServiceMock = new Mock<IContactPointService>();

        orderRepositoryMock
            .Setup(r => r.Create(
                It.Is<NotificationOrderChainRequest>(chain => chain.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(mainOrder => mainOrder.Id == mainOrderId),
                It.Is<List<NotificationOrder>>(reminders =>
                reminders.Count == 2 &&
                reminders.Any(o => o.Id == firstReminderId) &&
                reminders.Any(o => o.Id == secondReminderId))))
            .ReturnsAsync([expectedMainOrder, expectedFirstReminder, expectedFinalReminder]);

        contactPointServiceMock
            .Setup(contactService => contactService.AddPreferredContactPoints(It.IsAny<NotificationChannel>(), It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
            .Callback<NotificationChannel, List<Recipient>, string?>((channel, recipients, _) =>
            {
                foreach (var recipient in recipients)
                {
                    if (recipient.NationalIdentityNumber == "29105573746")
                    {
                        switch (channel)
                        {
                            case NotificationChannel.Email:
                            case NotificationChannel.EmailPreferred:
                                recipient.AddressInfo.Add(new EmailAddressPoint("recipient@example.com"));
                                break;

                            case NotificationChannel.Sms:
                            case NotificationChannel.SmsPreferred:
                                recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                                break;
                        }

                        recipient.IsReserved = true;
                    }
                }
            });

        var service = GetTestService(orderRepositoryMock.Object, contactPointServiceMock.Object, mainOrderId, mainOrderSendTime);

        // Act
        var response = await service.RegisterNotificationOrderChain(orderChainRequest);

        // Assert
        Assert.Equal(orderChainId, response.Id);
        Assert.Equal(mainOrderId, response.CreationResult.ShipmentId);
        Assert.Equal("TAX-REMINDER-2025", response.CreationResult.SendersReference);

        Assert.NotNull(response.CreationResult.Reminders);
        Assert.Equal(2, response.CreationResult.Reminders.Count);

        Assert.Equal(firstReminderId, response.CreationResult.Reminders[0].ShipmentId);
        Assert.Equal("TAX-REMINDER-2025-FIRST", response.CreationResult.Reminders[0].SendersReference);

        Assert.Equal(secondReminderId, response.CreationResult.Reminders[1].ShipmentId);
        Assert.Equal("TAX-REMINDER-2025-FINAL", response.CreationResult.Reminders[1].SendersReference);

        // Verify repository interactions
        orderRepositoryMock.Verify(
            r => r.Create(
                It.Is<NotificationOrderChainRequest>(req =>
                    req.OrderChainId == orderChainId &&
                    req.DialogportenAssociation != null &&
                    req.DialogportenAssociation.DialogId == "20E3D06D5546" &&
                    req.DialogportenAssociation.TransmissionId == "F9D34BB1C65F"),
                It.Is<NotificationOrder>(o =>
                    o.Id == mainOrderId &&
                    o.SendersReference == "TAX-REMINDER-2025" &&
                    o.ResourceId == "urn:altinn:resource:tax-2025" &&
                    o.NotificationChannel == NotificationChannel.EmailPreferred &&
                    o.Recipients.Any(r => r.NationalIdentityNumber == "29105573746")),
                It.Is<List<NotificationOrder>>(list =>
                    list.Count == 2 &&
                    list[0].Id == firstReminderId &&
                    list[1].Id == secondReminderId)),
            Times.Once);

        // Verify contact point interactions
        contactPointServiceMock.Verify(
            cp => cp.AddPreferredContactPoints(
                It.Is<NotificationChannel>(ch => ch == NotificationChannel.EmailPreferred),
                It.Is<List<Recipient>>(r => r.Any(rec => rec.NationalIdentityNumber == "29105573746")),
                It.Is<string?>(s => s == "urn:altinn:resource:tax-2025")),
            Times.Exactly(2));

        // Verify contact point added the expected email address
        contactPointServiceMock.Verify(
            cp => cp.AddPreferredContactPoints(
                It.Is<NotificationChannel>(ch => ch == NotificationChannel.SmsPreferred),
                It.Is<List<Recipient>>(r => r.Any(rec => rec.NationalIdentityNumber == "29105573746")),
                It.Is<string?>(s => s == "urn:altinn:resource:tax-2025")),
            Times.Once);
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_RecipientOrganizationWithoutReminders_OrderChainCreated()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        DateTime currentTime = DateTime.UtcNow;

        var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("brg"))
            .SetSendersReference("ANNUAL-REPORT-2025")
            .SetRequestedSendTime(currentTime.AddHours(2))
            .SetIdempotencyId("65698F3A-7B27-478C-9E76-A190C34A8099")
            .SetConditionEndpoint(new Uri("https://api.brreg.no/conditions/annual-report"))
            .SetDialogportenAssociation(new DialogportenIdentifiers { DialogId = "20E3D06D5546", TransmissionId = "F9D34BB1C65F" })
            .SetRecipient(new NotificationRecipient
            {
                RecipientOrganization = new RecipientOrganization
                {
                    OrgNumber = "312508729",
                    ChannelSchema = NotificationChannel.Email,
                    ResourceId = "urn:altinn:resource:annual-report-2025",
                    EmailSettings = new EmailSendingOptions
                    {
                        Subject = "Annual Report 2025",
                        ContentType = EmailContentType.Html,
                        SenderName = "Brønnøysundregistrene",
                        SenderEmailAddress = "no-reply@brreg.no",
                        Body = "<p>Your organization's annual report is due by March 31, 2025. Log in to Altinn to complete it.</p>",
                        SendingTimePolicy = SendingTimePolicy.Anytime
                    }
                }
            })
            .Build();

        var expectedOrder = new NotificationOrder(
            orderId,
            "ANNUAL-REPORT-2025",
            [
                new EmailTemplate("no-reply@brreg.no", "Annual Report 2025", "<p>Your organization's annual report is due by March 31, 2025. Log in to Altinn to complete it.</p>", EmailContentType.Html)
            ],
            currentTime.AddHours(2),
            NotificationChannel.Email,
            new Creator("brg"),
            DateTime.UtcNow,
            [new([], organizationNumber: "312508729")],
            null,
            "urn:altinn:resource:annual-report-2025",
            new Uri("https://api.brreg.no/conditions/annual-report"));

        // Setup mock
        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(r => r.Create(
            It.Is<NotificationOrderChainRequest>(e => e.OrderChainId == orderChainId && e.SendersReference == "ANNUAL-REPORT-2025"),
            It.Is<NotificationOrder>(o => o.NotificationChannel == NotificationChannel.Email && o.Recipients.Any(r => r.OrganizationNumber == "312508729")),
            It.Is<List<NotificationOrder>>(list => list.Count == 0)))
            .ReturnsAsync([expectedOrder]);

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(contactService => contactService.AddEmailContactPoints(It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
            .Callback<List<Recipient>, string?>((recipients, _) =>
            {
                foreach (var recipient in recipients)
                {
                    if (recipient.OrganizationNumber == "312508729")
                    {
                        recipient.AddressInfo.Add(new EmailAddressPoint("recipient@example.com"));
                    }
                }
            });

        var service = GetTestService(orderRepositoryMock.Object, contactPointServiceMock.Object, orderId, currentTime);

        // Act
        var response = await service.RegisterNotificationOrderChain(orderChainRequest);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(orderChainId, response.Id);

        Assert.NotNull(response.CreationResult);
        Assert.Equal(orderId, response.CreationResult.ShipmentId);
        Assert.Equal("ANNUAL-REPORT-2025", response.CreationResult.SendersReference);

        Assert.Null(response.CreationResult.Reminders);

        // Verify repository interactions
        orderRepositoryMock.Verify(
            r => r.Create(
            It.Is<NotificationOrderChainRequest>(e => e.OrderChainId == orderChainId),
            It.Is<NotificationOrder>(o =>
                o.Id == orderId &&
                o.SendersReference == "ANNUAL-REPORT-2025" &&
                o.NotificationChannel == NotificationChannel.Email &&
                o.Recipients.Any(r => r.OrganizationNumber == "312508729")),
            It.Is<List<NotificationOrder>>(list => list.Count == 0)),
            Times.Once);

        // Verify contact point interactions
        contactPointServiceMock.Verify(
            cp => cp.AddEmailContactPoints(
            It.Is<List<Recipient>>(r => r.Any(rec => rec.OrganizationNumber == "312508729")),
            It.Is<string?>(s => s == "urn:altinn:resource:annual-report-2025")),
            Times.Once);
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_RepositoryReturnsEmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        DateTime currentTime = DateTime.UtcNow;

        var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("test"))
            .SetIdempotencyId("test-idempotency-id")
            .SetRecipient(new NotificationRecipient
            {
                RecipientEmail = new RecipientEmail
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptions
                    {
                        Body = "Test body",
                        Subject = "Test subject",
                        ContentType = EmailContentType.Plain
                    }
                }
            })
            .Build();

        // Setup repository to return an empty list
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.Create(It.Is<NotificationOrderChainRequest>(e => e.OrderChainId == orderChainId), It.Is<NotificationOrder>(e => e.Id == orderId), It.IsAny<List<NotificationOrder>>()))
            .ReturnsAsync([]);

        var service = GetTestService(repoMock.Object, null, orderId, currentTime);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.RegisterNotificationOrderChain(orderRequest));

        Assert.Equal("Failed to create the notification order chain.", exception.Message);

        // Verify the repository was called
        repoMock.Verify(r => r.Create(It.Is<NotificationOrderChainRequest>(e => e.OrderChainId == orderChainId), It.Is<NotificationOrder>(e => e.Id == orderId), It.IsAny<List<NotificationOrder>>()), Times.Once);
    }

    [Fact]
    public async Task CreateNotificationOrder_WithMissingContactInformation_ThrowsInvalidOperationException()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        DateTime currentTime = DateTime.UtcNow;

        var recipient = new NotificationRecipient
        {
            RecipientPerson = new RecipientPerson
            {
                NationalIdentityNumber = "16069412345",
                ResourceId = "urn:altinn:resource:test",
                ChannelSchema = NotificationChannel.Email,
                EmailSettings = new EmailSendingOptions
                {
                    Body = "Test Body",
                    Subject = "Test Subject",
                    ContentType = EmailContentType.Plain
                }
            }
        };

        Mock<IContactPointService> contactPointMock = new();
        contactPointMock
            .Setup(contactService => contactService.AddEmailContactPoints(It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
            .Callback<List<Recipient>, string?>((recipients, _) =>
            {
                // Intentionally don't add any address info to simulate missing contact
            });

        var service = GetTestService(null, contactPointMock.Object, orderId, currentTime);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.RegisterNotificationOrderChain(
                new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                    .SetOrderId(orderId)
                    .SetRecipient(recipient)
                    .SetOrderChainId(orderChainId)
                    .SetCreator(new Creator("test"))
                    .SetRequestedSendTime(currentTime.AddHours(1))
                    .SetIdempotencyId("C0A3FABE-D65F-48A0-8745-5D4CC6EA7968")
                    .Build()));

        // Verify the exception message contains information about missing contacts
        Assert.Contains("Missing contact information for recipient", exception.Message);

        // Verify the contact point service was called
        contactPointMock.Verify(
            contactService => contactService.AddEmailContactPoints(
                It.Is<List<Recipient>>(r => r.Any(rec => rec.NationalIdentityNumber == "16069412345")),
                It.Is<string?>(s => s == "urn:altinn:resource:test")),
            Times.Once);
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

        var config = Options.Create<NotificationConfig>(new()
        {
            DefaultEmailFromAddress = "noreply@altinn.no",
            DefaultSmsSenderNumber = "TestDefaultSmsSenderNumberNumber"
        });

        return new OrderRequestService(repository, contactPointService, guidMock.Object, dateTimeMock.Object, config);
    }
}
