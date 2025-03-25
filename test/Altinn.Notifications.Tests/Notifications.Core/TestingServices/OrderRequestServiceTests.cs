﻿using System;
using System.Collections.Generic;
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
using Microsoft.IdentityModel.Tokens;

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
    public async Task RegisterNotificationOrderChain_WithReminders_OrderChainCreated()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        DateTime currentTime = DateTime.UtcNow;
        var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetCreator(new Creator("ttd"))
            .SetRequestedSendTime(currentTime.AddMinutes(5))
            .SetIdempotencyId("84CD3017-92E3-4C3D-80DE-C10338F30813")
            .SetSendersReference("43F2C14A-62E4-4258-8887-3414296E7D82")
            .SetRecipient(new NotificationRecipient { RecipientEmail = new RecipientEmail { EmailAddress = "recipient@example.com", Settings = new EmailSendingOptions { Body = "Email body", Subject = "Email subject" } } })
            .SetReminders(
            [
                    new NotificationReminder
                    {
                        DelayDays = 1,
                        Recipient = new NotificationRecipient { RecipientEmail = new RecipientEmail { EmailAddress = "reminder@example.com", Settings = new EmailSendingOptions { Body = "Reminder body", Subject = "Reminder subject" } } },
                        RequestedSendTime = currentTime.AddMinutes(20),
                        SendersReference = "DAFBD9F5-7CCA-468B-AD20-793BF44D9C44"
                    }
            ])
            .Build();

        var expectedMainOrder = new NotificationOrder(orderId, "43F2C14A-62E4-4258-8887-3414296E7D82", [], currentTime.AddMinutes(5), NotificationChannel.Email, new Creator("ttd"), currentTime, [], null, null, null);
        var expectedReminderOrder = new NotificationOrder(Guid.NewGuid(), "DAFBD9F5-7CCA-468B-AD20-793BF44D9C44", [], currentTime.AddMinutes(20), NotificationChannel.Email, new Creator("ttd"), currentTime, [], null, null, null);

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), It.IsAny<List<NotificationOrder>>()))
            .ReturnsAsync([expectedMainOrder, expectedReminderOrder]);

        var service = GetTestService(repoMock.Object, null, orderId, currentTime);

        // Act
        var response = await service.RegisterNotificationOrderChain(orderRequest);

        // Assert
        Assert.Equal(orderId, response.Id);
        Assert.Equal("43F2C14A-62E4-4258-8887-3414296E7D82", response.CreationResult.SendersReference);

        Assert.NotNull(response.CreationResult.Reminders);
        Assert.Single(response.CreationResult.Reminders);
        Assert.Equal("DAFBD9F5-7CCA-468B-AD20-793BF44D9C44", response.CreationResult.Reminders[0].SendersReference);

        repoMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_WithoutReminders_OrderChainCreated()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        DateTime currentTime = DateTime.UtcNow;
        var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetCreator(new Creator("ttd"))
            .SetRequestedSendTime(currentTime.AddMinutes(5))
            .SetIdempotencyId("65698F3A-7B27-478C-9E76-A190C34A8099")
            .SetSendersReference("674DF57E-5344-4106-95FA-24E19126FBD8")
            .SetRecipient(new NotificationRecipient { RecipientEmail = new RecipientEmail { EmailAddress = "recipient@example.com", Settings = new EmailSendingOptions { Body = "Email body", Subject = "Email subject" } } })
            .Build();

        var expectedMainOrder = new NotificationOrder(orderId, "674DF57E-5344-4106-95FA-24E19126FBD8", [], currentTime.AddMinutes(10), NotificationChannel.Email, new Creator("ttd"), currentTime, [], null, null, null);

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), It.IsAny<List<NotificationOrder>>()))
            .ReturnsAsync([expectedMainOrder]);

        var service = GetTestService(repoMock.Object, null, orderId, currentTime);

        // Act
        var response = await service.RegisterNotificationOrderChain(orderRequest);

        // Assert
        Assert.Equal(orderId, response.Id);
        Assert.Equal("674DF57E-5344-4106-95FA-24E19126FBD8", response.CreationResult.SendersReference);
        Assert.Null(response.CreationResult.Reminders);
        repoMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_RepositoryReturnsNull_ThrowsException()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        DateTime currentTime = DateTime.UtcNow;
        var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetCreator(new Creator("ttd"))
            .SetRequestedSendTime(currentTime.AddMinutes(10))
            .SetIdempotencyId("9E032152-8A09-4887-9F9F-E56FE3FBC8C7")
            .SetSendersReference("A6BFDADD-A3D1-476D-8F64-55574CDCADCC")
            .SetRecipient(new NotificationRecipient { RecipientEmail = new RecipientEmail { EmailAddress = "test@example.com", Settings = new EmailSendingOptions { Body = "Test body", Subject = "Test subject" } } })
            .Build();

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), It.IsAny<List<NotificationOrder>>()))
            .ReturnsAsync([]);

        var service = GetTestService(repoMock.Object, null, orderId, currentTime);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterNotificationOrderChain(orderRequest));
        repoMock.VerifyAll();
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_RepositoryReturnsEmptyList_ThrowsException()
    {
        // Arrange
        DateTime currentTime = DateTime.UtcNow;
        Guid orderId = Guid.NewGuid();
        var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetCreator(new Creator("ttd"))
            .SetRequestedSendTime(currentTime.AddMinutes(10))
            .SetIdempotencyId("5D69E05E-8BC7-4736-BADA-C6CB00ED8C0A")
            .SetSendersReference("D340DC99-E5B0-4153-B56E-B3946E8D4AC4")
            .SetRecipient(new NotificationRecipient { RecipientEmail = new RecipientEmail { EmailAddress = "test@example.com", Settings = new EmailSendingOptions { Body = "Test body", Subject = "Test subject" } } })
            .Build();

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), It.IsAny<List<NotificationOrder>>()))
            .ReturnsAsync([]);

        var service = GetTestService(repoMock.Object, null, orderId, currentTime);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterNotificationOrderChain(orderRequest));
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

        var config = Options.Create<NotificationConfig>(new()
        {
            DefaultEmailFromAddress = "noreply@altinn.no",
            DefaultSmsSenderNumber = "TestDefaultSmsSenderNumberNumber"
        });
        return new OrderRequestService(repository, contactPointService, guidMock.Object, dateTimeMock.Object, config);
    }
}
