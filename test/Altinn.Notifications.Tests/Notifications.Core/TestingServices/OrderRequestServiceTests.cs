using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
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
    public async Task RegisterNotificationOrder_ForEmailAndSms_CallsAddEmailAndSmsContactPointsAsync()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        DateTime createdTime = DateTime.UtcNow;
        DateTime sendTime = DateTime.UtcNow.AddMinutes(10);

        var input = new NotificationOrderRequest
        {
            Creator = new Creator("ttd"),
            RequestedSendTime = sendTime,

            NotificationChannel = NotificationChannel.EmailAndSms,
            SendersReference = "15FF9B24-EF7E-469D-80D6-E186FCF6D657",

            Recipients =
            [
                new Recipient { NationalIdentityNumber = "14210548840" },
                new Recipient { NationalIdentityNumber = "30286043298" }
            ],

            Templates =
            [
                new SmsTemplate { Body = "sms-body", SenderNumber = "TestSender" },
                new EmailTemplate { Body = "email-body", FromAddress = "noreply@altinn.no" }
            ]
        };

        Mock<IOrderRepository> repoMock = new();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .ReturnsAsync((NotificationOrder order) => order);

        Mock<IContactPointService> contactPointMock = new();
        contactPointMock
            .Setup(cp => cp.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
            .Callback<List<Recipient>, string?>((recipients, resourceId) =>
            {
                foreach (var recipient in recipients)
                {
                    if (recipient.NationalIdentityNumber == "14210548840")
                    {
                        recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                        recipient.AddressInfo.Add(new EmailAddressPoint("first-recipient@example.com"));
                    }
                    else if (recipient.NationalIdentityNumber == "30286043298")
                    {
                        recipient.AddressInfo.Add(new SmsAddressPoint("+4788888888"));
                        recipient.AddressInfo.Add(new EmailAddressPoint("second-recipient@example.com"));
                    }
                }
            });

        var service = GetTestService(repoMock.Object, contactPointMock.Object, id, createdTime);

        // Act
        NotificationOrderRequestResponse actual = await service.RegisterNotificationOrder(input);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(id, actual.OrderId);

        contactPointMock.Verify(
            cp => cp.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients =>
                    recipients.Any(r => r.NationalIdentityNumber == "14210548840") &&
                    recipients.Any(r => r.NationalIdentityNumber == "30286043298")),
                It.Is<string?>(resourceId => resourceId == null)),
            Times.Once);

        repoMock.Verify(r => r.Create(It.IsAny<NotificationOrder>()), Times.Once);
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
                reminders.Any(o => o.Id == secondReminderId)),
                It.IsAny<CancellationToken>()))
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
        Assert.Equal(orderChainId, response.OrderChainId);
        Assert.Equal(mainOrderId, response.OrderChainReceipt.ShipmentId);
        Assert.Equal("TAX-REMINDER-2025", response.OrderChainReceipt.SendersReference);

        Assert.NotNull(response.OrderChainReceipt.Reminders);
        Assert.Equal(2, response.OrderChainReceipt.Reminders.Count);

        Assert.NotEqual(orderChainId, response.OrderChainReceipt.Reminders[0].ShipmentId);
        Assert.Equal(firstReminderId, response.OrderChainReceipt.Reminders[0].ShipmentId);
        Assert.Equal("TAX-REMINDER-2025-FIRST", response.OrderChainReceipt.Reminders[0].SendersReference);

        Assert.NotEqual(orderChainId, response.OrderChainReceipt.Reminders[1].ShipmentId);
        Assert.Equal(secondReminderId, response.OrderChainReceipt.Reminders[1].ShipmentId);
        Assert.Equal("TAX-REMINDER-2025-FINAL", response.OrderChainReceipt.Reminders[1].SendersReference);

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
                    list[1].Id == secondReminderId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify contact point interactions
        contactPointServiceMock.Verify(
            cp => cp.AddPreferredContactPoints(
                It.Is<NotificationChannel>(ch => ch == NotificationChannel.EmailPreferred),
                It.Is<List<Recipient>>(r => r.Any(rec => rec.NationalIdentityNumber == "29105573746")),
                It.Is<string?>(s => s == "tax-2025")),
            Times.Exactly(2));

        // Verify contact point added the expected email address
        contactPointServiceMock.Verify(
            cp => cp.AddPreferredContactPoints(
                It.Is<NotificationChannel>(ch => ch == NotificationChannel.SmsPreferred),
                It.Is<List<Recipient>>(r => r.Any(rec => rec.NationalIdentityNumber == "29105573746")),
                It.Is<string?>(s => s == "tax-2025")),
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
            It.Is<List<NotificationOrder>>(list => list.Count == 0),
            It.IsAny<CancellationToken>()))
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
        Assert.Equal(orderChainId, response.OrderChainId);

        Assert.NotNull(response.OrderChainReceipt);
        Assert.Equal(orderId, response.OrderChainReceipt.ShipmentId);
        Assert.NotEqual(orderChainId, response.OrderChainReceipt.ShipmentId);
        Assert.Equal("ANNUAL-REPORT-2025", response.OrderChainReceipt.SendersReference);

        Assert.Null(response.OrderChainReceipt.Reminders);

        // Verify repository interactions
        orderRepositoryMock.Verify(
            r => r.Create(
            It.Is<NotificationOrderChainRequest>(e => e.OrderChainId == orderChainId),
            It.Is<NotificationOrder>(o =>
                o.Id == orderId &&
                o.SendersReference == "ANNUAL-REPORT-2025" &&
                o.NotificationChannel == NotificationChannel.Email &&
                o.Recipients.Any(r => r.OrganizationNumber == "312508729")),
            It.Is<List<NotificationOrder>>(list => list.Count == 0),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify contact point interactions
        contactPointServiceMock.Verify(
            cp => cp.AddEmailContactPoints(
            It.Is<List<Recipient>>(r => r.Any(rec => rec.OrganizationNumber == "312508729")),
            It.Is<string?>(s => s == "annual-report-2025")),
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
            .Setup(r => r.Create(It.Is<NotificationOrderChainRequest>(e => e.OrderChainId == orderChainId), It.Is<NotificationOrder>(e => e.Id == orderId), It.IsAny<List<NotificationOrder>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = GetTestService(repoMock.Object, null, orderId, currentTime);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.RegisterNotificationOrderChain(orderRequest));

        Assert.Equal("Failed to create the notification order chain.", exception.Message);

        // Verify the repository was called
        repoMock.Verify(r => r.Create(It.Is<NotificationOrderChainRequest>(e => e.OrderChainId == orderChainId), It.Is<NotificationOrder>(e => e.Id == orderId), It.IsAny<List<NotificationOrder>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        Guid mainOrderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        DateTime mainOrderSendTime = DateTime.UtcNow;

        var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(mainOrderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("test"))
            .SetIdempotencyId("test-cancellation-id")
            .SetRequestedSendTime(mainOrderSendTime.AddDays(1))
            .SetRecipient(new NotificationRecipient
            {
                RecipientEmail = new RecipientEmail
                {
                    EmailAddress = "test@example.com",
                    Settings = new EmailSendingOptions
                    {
                        Body = "Test body",
                        Subject = "Test subject",
                        ContentType = EmailContentType.Plain
                    }
                }
            })
            .Build();

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.Create(
                It.IsAny<NotificationOrderChainRequest>(),
                It.IsAny<NotificationOrder>(),
                It.IsAny<List<NotificationOrder>>(),
                It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, NotificationOrder, List<NotificationOrder>, CancellationToken>((_, _, _, token) => token.ThrowIfCancellationRequested())
            .ReturnsAsync([]);

        var service = GetTestService(orderRepositoryMock.Object, null, mainOrderId, mainOrderSendTime);

        // Create a cancellation token that's already canceled
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await service.RegisterNotificationOrderChain(orderChainRequest, cancellationTokenSource.Token));

        // Verify the repository was called with the cancellation token
        orderRepositoryMock.Verify(
            r => r.Create(
                It.IsAny<NotificationOrderChainRequest>(),
                It.IsAny<NotificationOrder>(),
                It.IsAny<List<NotificationOrder>>(),
                It.Is<CancellationToken>(token => token.IsCancellationRequested)),
            Times.Never);
    }

    [Fact]
    public async Task RetrieveOrderChainTracking_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string creatorName = "test-creator";
        string idempotencyId = "test-idempotency-id";

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetOrderChainTracking(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, token) => token.ThrowIfCancellationRequested())
            .ReturnsAsync((NotificationOrderChainResponse?)null);

        var service = GetTestService(orderRepositoryMock.Object);

        // Create a cancellation token that's already canceled
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.RetrieveOrderChainTracking(creatorName, idempotencyId, cancellationTokenSource.Token));

        // Verify the repository was called with the cancellation token
        orderRepositoryMock.Verify(
            r => r.GetOrderChainTracking(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<CancellationToken>(token => token.IsCancellationRequested)),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveOrderChainTracking_WhenOrderChainExists_ReturnsResponse()
    {
        // Arrange
        Guid shipmentId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        string creatorName = "test-creator";
        string idempotencyId = "test-idempotency-id";

        var expectedResponse = new NotificationOrderChainResponse
        {
            OrderChainId = orderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                Reminders = [],
                ShipmentId = shipmentId,
                SendersReference = "test-reference"
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetOrderChainTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var service = GetTestService(orderRepositoryMock.Object);

        // Act
        var result = await service.RetrieveOrderChainTracking(creatorName, idempotencyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.Equal(shipmentId, result.OrderChainReceipt.ShipmentId);
        Assert.Equal("test-reference", result.OrderChainReceipt.SendersReference);

        orderRepositoryMock.Verify(
            r => r.GetOrderChainTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveOrderChainTracking_WhenOrderChainDoesNotExist_ReturnsNull()
    {
        // Arrange
        string idempotencyId = "non-existent-id";
        string creatorName = "non-existent-creator";

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetOrderChainTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderChainResponse?)null);

        var service = GetTestService(orderRepositoryMock.Object);

        // Act
        var result = await service.RetrieveOrderChainTracking(creatorName, idempotencyId);

        // Assert
        Assert.Null(result);

        orderRepositoryMock.Verify(
            r => r.GetOrderChainTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveOrderChainTracking_WithReminders_ReturnsCompleteResponse()
    {
        // Arrange
        string creatorName = "test-creator";
        string idempotencyId = "test-idempotency-id";

        Guid orderChainId = Guid.NewGuid();
        Guid mainShipmentId = Guid.NewGuid();
        Guid firstReminderShipmentId = Guid.NewGuid();
        Guid secondReminderShipmentId = Guid.NewGuid();

        var expectedResponse = new NotificationOrderChainResponse
        {
            OrderChainId = orderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = mainShipmentId,
                SendersReference = "065C28CA-90B5-47FF-AFAD-A8DC084FAB9E",
                Reminders =
                [
                    new NotificationOrderChainShipment
                    {
                        ShipmentId = firstReminderShipmentId,
                        SendersReference = "FADAE91D-3352-47D6-9BC6-C68B6A2C8F11"
                    },
                    new NotificationOrderChainShipment
                    {
                        ShipmentId = secondReminderShipmentId,
                        SendersReference = "406B91C1-9DB2-422D-8B8A-A76BECF7FE40"
                    }
                ]
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetOrderChainTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var service = GetTestService(orderRepositoryMock.Object);

        // Act
        var result = await service.RetrieveOrderChainTracking(creatorName, idempotencyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.Equal(mainShipmentId, result.OrderChainReceipt.ShipmentId);
        Assert.Equal("065C28CA-90B5-47FF-AFAD-A8DC084FAB9E", result.OrderChainReceipt.SendersReference);

        // Verify reminders
        Assert.NotNull(result.OrderChainReceipt.Reminders);
        Assert.Equal(2, result.OrderChainReceipt.Reminders.Count);

        Assert.Equal(firstReminderShipmentId, result.OrderChainReceipt.Reminders[0].ShipmentId);
        Assert.Equal("FADAE91D-3352-47D6-9BC6-C68B6A2C8F11", result.OrderChainReceipt.Reminders[0].SendersReference);

        Assert.Equal(secondReminderShipmentId, result.OrderChainReceipt.Reminders[1].ShipmentId);
        Assert.Equal("406B91C1-9DB2-422D-8B8A-A76BECF7FE40", result.OrderChainReceipt.Reminders[1].SendersReference);

        // Verify repository method was called with correct parameters
        orderRepositoryMock.Verify(
            r => r.GetOrderChainTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveOrderChainTracking_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        string creatorName = "test-creator";
        string idempotencyId = "test-idempotency-id";

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetOrderChainTracking(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderChainResponse?)null);

        var service = GetTestService(orderRepositoryMock.Object);

        // Create a cancellation token
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Act
        await service.RetrieveOrderChainTracking(creatorName, idempotencyId, cancellationToken);

        // Assert
        orderRepositoryMock.Verify(
            r => r.GetOrderChainTracking(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<CancellationToken>(token => token == cancellationToken)),
            Times.Once);
    }

    [Fact]
    public async Task CreateNotificationOrder_WithMissingContactInformation_ThrowsRecipientLookupException()
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
        var exception = await Assert.ThrowsAsync<RecipientLookupException>(async () =>
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
                It.Is<string?>(s => s == "test")),
            Times.Once);
    }

    /// <summary>
    /// Test to ensure that the correct value for SendingTimePolicy is passed to the repository.
    /// </summary>
    /// <param name="sendingTimePolicyInput">If sendingTimePolicyInput is null, the value for sms should be set to the default value in the setter: Daytime</param>
    /// <param name="shouldEqual">Should be equal to sendingTimePolicyInput, unless value is null</param>
    /// <returns></returns>
    [Theory]
    [InlineData(SendingTimePolicy.Daytime, SendingTimePolicy.Daytime)]
    [InlineData(SendingTimePolicy.Anytime, SendingTimePolicy.Anytime)]
    [InlineData(null, SendingTimePolicy.Daytime)]
    public async Task CreateNotificationOrder_PassesCorrectValueForSendingTimePolicyToRepository(SendingTimePolicy? sendingTimePolicyInput, SendingTimePolicy shouldEqual)
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        var smsSettings = new SmsSendingOptions
        {
            Body = "Test Body",
            Sender = "TestSender"
        };

        if (sendingTimePolicyInput != null)
        {
            smsSettings = new SmsSendingOptions
            {
                Body = "Test Body",
                Sender = "TestSender",
                SendingTimePolicy = sendingTimePolicyInput.Value
            };
        }

        var mockResponse = new List<NotificationOrder>
        {
            new()
        };
        DateTime currentTime = DateTime.UtcNow;
        var recipient = new NotificationRecipient
        {
            RecipientSms = new RecipientSms
            {
                PhoneNumber = "+4799999999",
                Settings = smsSettings
            }
        };
        Mock<IOrderRepository> orderRepositoryMock = new();
        orderRepositoryMock
            .Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), It.IsAny<List<NotificationOrder>>(), CancellationToken.None))
            .Returns(Task.FromResult(mockResponse));

        var service = GetTestService(orderRepositoryMock.Object, null, orderId, currentTime);

        // Act
        await service.RegisterNotificationOrderChain(
            new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(orderId)
                .SetOrderChainId(orderChainId)
                .SetRecipient(recipient)
                .SetCreator(new Creator("test"))
                .SetRequestedSendTime(currentTime.AddHours(1))
                .SetIdempotencyId("C0A3FABE-D65F-48A0-8745-5D4CC6EA7968")
                .Build());

        // Assert
        orderRepositoryMock.Verify(
            r => r.Create(
            It.IsAny<NotificationOrderChainRequest>(),
            It.Is<NotificationOrder>(o => o.SendingTimePolicy == shouldEqual),
            It.IsAny<List<NotificationOrder>>(),
            CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task RegisterNotificationOrder_ShouldPassNullValueForSendingTimePolicyToRepository()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        var mockResponse = new NotificationOrder();

        var notificationOrder = new NotificationOrderRequest()
        {
        };

        Mock<IOrderRepository> orderRepositoryMock = new();
        orderRepositoryMock
            .Setup(r => r.Create(It.IsAny<NotificationOrder>()))
            .Returns(Task.FromResult(mockResponse));

        var service = GetTestService(orderRepositoryMock.Object, null, orderId, DateTime.UtcNow);

        // Act
        await service.RegisterNotificationOrder(notificationOrder);

        // Assert
        orderRepositoryMock.Verify(
            r => r.Create(
                It.Is<NotificationOrder>(o => o.SendingTimePolicy == null)),
            Times.Once);
    }

    private static OrderRequestService GetTestService(IOrderRepository? repository = null, IContactPointService? contactPointService = null, Guid? guid = null, DateTime? dateTime = null)
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
