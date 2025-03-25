using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

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
    public async Task RegisterNotificationOrderChain_WithMultipleReminders_OrderChainCreated()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        DateTime currentTime = DateTime.UtcNow;

        var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetCreator(new Creator("skd"))
            .SetRequestedSendTime(currentTime.AddDays(1))
            .SetSendersReference("TAX-FILING-REMINDER-2025")
            .SetIdempotencyId("84CD3017-92E3-4C3D-80DE-C10338F30813")
            .SetRecipient(new NotificationRecipient
            {
                RecipientPerson = new RecipientPerson
                {
                    IgnoreReservation = true,
                    NationalIdentityNumber = "29105573746",
                    ChannelSchema = NotificationChannel.EmailPreferred,
                    ResourceId = "urn:altinn:resource:tax-filing-2025",
                    EmailSettings = new EmailSendingOptions
                    {
                        ContentType = EmailContentType.Html,
                        Subject = "Tax Filing 2025",
                        SenderName = "Norwegian Tax Administration",
                        SenderEmailAddress = "no-reply@skatteetaten.no",
                        Body = "<p>Dear taxpayer,</p><p>Please log in to <a href=\"https://skatteetaten.no\">Skatteetaten</a> to complete your filing.</p>",
                    },
                    SmsSettings = new SmsSendingOptions
                    {
                        SendingTimePolicy = SendingTimePolicy.Daytime,
                        Sender = "Skatteetaten",
                        Body = "Tax filing: Please visit skatteetaten.no to complete your filing."
                    }
                }
            })
            .SetConditionEndpoint(new Uri("https://api.skatteetaten.no/tax/conditions/filing-incomplete"))
            .SetReminders(
            [
                new NotificationReminder
                {
                    DelayDays = 7,
                    OrderId = Guid.NewGuid(),
                    RequestedSendTime = currentTime.AddDays(8),
                    SendersReference = "TAX-FILING-REMINDER-2025-FIRST",
                    ConditionEndpoint = new Uri("https://api.skatteetaten.no/tax/conditions/filing-incomplete"),
                    Recipient = new NotificationRecipient
                    {
                        RecipientPerson = new RecipientPerson
                        {
                            IgnoreReservation = true,
                            NationalIdentityNumber = "29105573746",
                            ChannelSchema = NotificationChannel.EmailPreferred,
                            ResourceId = "urn:altinn:resource:tax-filing-2025",
                            EmailSettings = new EmailSendingOptions
                            {
                                ContentType = EmailContentType.Html,
                                Subject = "REMINDER: Tax Filing 2025",
                                SenderName = "Norwegian Tax Administration",
                                SenderEmailAddress = "no-reply@skatteetaten.no",
                                Body = "<p>Dear taxpayer,</p><p><strong>REMINDER:</strong> Please log in to <a href=\"https://skatteetaten.no\">Skatteetaten</a> to complete your filing.</p>"
                            },
                            SmsSettings = new SmsSendingOptions
                            {
                                SendingTimePolicy = SendingTimePolicy.Daytime,
                                Sender = "Skatteetaten",
                                Body = "REMINDER: visit skatteetaten.no to complete your filing."
                            }
                        }
                    }
                },
                new NotificationReminder
                {
                    DelayDays = 14,
                    OrderId = Guid.NewGuid(),
                    RequestedSendTime = currentTime.AddDays(15),
                    SendersReference = "TAX-FILING-REMINDER-2025-FINAL",
                    ConditionEndpoint = new Uri("https://api.skatteetaten.no/tax/conditions/filing-incomplete"),
                    Recipient = new NotificationRecipient
                    {
                        RecipientPerson = new RecipientPerson
                        {
                            NationalIdentityNumber = "29105573746",
                            ChannelSchema = NotificationChannel.EmailPreferred,
                            ResourceId = "urn:altinn:resource:tax-filing-2025",
                            IgnoreReservation = true,
                            EmailSettings = new EmailSendingOptions
                            {
                                ContentType = EmailContentType.Html,
                                Subject = "FINAL REMINDER: Tax Filing 2025",
                                SenderName = "Norwegian Tax Administration",
                                SenderEmailAddress = "no-reply@skatteetaten.no",
                                Body = "<p>Dear taxpayer,</p><p><strong>FINAL REMINDER:</strong> Please log in to <a href=\"https://skatteetaten.no\">Skatteetaten</a> to complete your filing or you may incur penalties.</p>",
                            },
                            SmsSettings = new SmsSendingOptions
                            {
                                SendingTimePolicy = SendingTimePolicy.Daytime,
                                Sender = "Skatteetaten",
                                Body = "URGENT: You have not completed your filing yet. Complete immediately at skatteetaten.no to avoid penalties."
                            }
                        }
                    }
                }
            ])
            .Build();

        // Setup expected orders with appropriate channel, templates, and recipients.
        var mainOrderId = orderId;
        var firstReminderId = Guid.NewGuid();
        var secondReminderId = Guid.NewGuid();

        var expectedMainOrder = new NotificationOrder(
            mainOrderId,
            "TAX-FILING-REMINDER-2025",
            [
                new SmsTemplate("Skatteetaten", "Tax filing: Please visit skatteetaten.no to complete your filing."),
                new EmailTemplate("no-reply@skatteetaten.no", "Tax Filing 2025", "<p>Dear taxpayer,</p><p>Please log in to <a href=\"https://skatteetaten.no\">Skatteetaten</a> to complete your filing.</p>", EmailContentType.Html)
            ],
            currentTime.AddDays(1),
            NotificationChannel.EmailPreferred,
            new Creator("skd"),
            currentTime,
            [new([], nationalIdentityNumber: "29105573746")],
            true,
            "urn:altinn:resource:tax-filing-2025",
            new Uri("https://api.skatteetaten.no/tax/conditions/filing-incomplete"));

        var expectedFirstReminderOrder = new NotificationOrder(
            firstReminderId,
            "TAX-FILING-REMINDER-2025-FIRST",
            [
                new SmsTemplate("Skatteetaten", "REMINDER: visit skatteetaten.no to complete your filing."),
                new EmailTemplate("no-reply@skatteetaten.no", "REMINDER: Tax Filing 2025", "<p>Dear taxpayer,</p><p><strong>REMINDER:</strong> Please log in to <a href=\"https://skatteetaten.no\">Skatteetaten</a> to complete your filing.</p>", EmailContentType.Html)
            ],
            currentTime.AddDays(8),
            NotificationChannel.EmailPreferred,
            new Creator("skd"),
            currentTime,
            [new([], nationalIdentityNumber: "29105573746")],
            true,
            "urn:altinn:resource:tax-filing-2025",
            new Uri("https://api.skatteetaten.no/tax/conditions/filing-incomplete"));

        var expectedSecondReminderOrder = new NotificationOrder(
            secondReminderId,
            "TAX-FILING-REMINDER-2025-FINAL",
            [
                new SmsTemplate("Skatteetaten", "URGENT: You have not completed your filing yet. Complete immediately at skatteetaten.no to avoid penalties."),
                new EmailTemplate("no-reply@skatteetaten.no", "FINAL REMINDER: Tax Filing 2025", "<p>Dear taxpayer,</p><p><strong>FINAL REMINDER:</strong> Please log in to <a href=\"https://skatteetaten.no\">Skatteetaten</a> to complete your filing or you may incur penalties.</p>", EmailContentType.Html)
            ],
            currentTime.AddDays(15),
            NotificationChannel.EmailPreferred,
            new Creator("skd"),
            currentTime,
            [new([], nationalIdentityNumber: "29105573746")],
            true,
            "urn:altinn:resource:tax-filing-2025",
            new Uri("https://api.skatteetaten.no/tax/conditions/filing-incomplete"));

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.Create(
            It.Is<NotificationOrderChainRequest>(req => req.OrderId == orderId && req.SendersReference == "TAX-FILING-REMINDER-2025"),
            It.Is<NotificationOrder>(o => o.NotificationChannel == NotificationChannel.EmailPreferred),
            It.Is<List<NotificationOrder>>(list => list.Count == 2)))
            .ReturnsAsync(
            [
            expectedMainOrder,
            expectedFirstReminderOrder,
            expectedSecondReminderOrder
            ]);

        // Setup service with mocks
        var contactPointMock = new Mock<IContactPointService>();
        contactPointMock
            .Setup(cp => cp.AddPreferredContactPoints(It.IsAny<NotificationChannel>(), It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
            .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
            {
                // Simulate successful lookup by adding contact points
                foreach (var recipient in recipients)
                {
                    if (recipient.NationalIdentityNumber == "29105573746")
                    {
                        recipient.AddressInfo.Add(new EmailAddressPoint("taxpayer@example.com"));
                        recipient.IsReserved = false;
                    }
                }
            });

        var service = GetTestService(repoMock.Object, contactPointMock.Object, orderId, currentTime);

        // Act
        var response = await service.RegisterNotificationOrderChain(orderRequest);

        // Assert
        Assert.Equal(mainOrderId, response.Id);
        Assert.NotNull(response.CreationResult.Reminders);
        Assert.Equal(2, response.CreationResult.Reminders.Count);
        Assert.Equal("TAX-FILING-REMINDER-2025", response.CreationResult.SendersReference);
        Assert.Equal("TAX-FILING-REMINDER-2025-FIRST", response.CreationResult.Reminders[0].SendersReference);
        Assert.Equal("TAX-FILING-REMINDER-2025-FINAL", response.CreationResult.Reminders[1].SendersReference);

        // Verify that the mocks were called with the expected parameters
        repoMock.Verify(
            r => r.Create(
            It.Is<NotificationOrderChainRequest>(req => req.OrderId == orderId),
            It.Is<NotificationOrder>(o => o.SendersReference == "TAX-FILING-REMINDER-2025"),
            It.Is<List<NotificationOrder>>(list => list.Count == 2)),
            Times.Once);

        contactPointMock.Verify(
            cp => cp.AddPreferredContactPoints(
            It.Is<NotificationChannel>(c => c == NotificationChannel.EmailPreferred),
            It.Is<List<Recipient>>(r => r.Any(e => e.NationalIdentityNumber == "29105573746")),
            It.Is<string?>(s => s == "urn:altinn:resource:tax-filing-2025")),
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
