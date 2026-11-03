using System;
using System.Collections.Generic;
using System.Linq;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators;
using Altinn.Notifications.Validators.Extensions;
using Altinn.Notifications.Validators.Rules;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class SenderOwnershipRulesTests
{
    private const string _owner = "ttd";

    [Fact]
    public void Validate_LegacyOrderSendersNotAllowed_ReportsEmailAndSmsTogetherWithoutEchoingSenders()
    {
        var service = ServiceReturning(false);

        var result = new NotificationOrderRequestValidator().Validate(LegacyOrder(), _owner, service.Object);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("The sender email address is not registered for the service owner.", result.Errors.Single(e => e.PropertyName == "EmailTemplate.FromAddress").ErrorMessage);
        Assert.Equal("The SMS sender is not registered for the service owner.", result.Errors.Single(e => e.PropertyName == "SmsTemplate.SenderNumber").ErrorMessage);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage.Contains("noreply@altinn.no") || e.ErrorMessage.Contains("TestSender"));
    }

    [Fact]
    public void Validate_LegacyOrderSendersAllowed_IsValid()
    {
        var service = ServiceReturning(true);

        var result = new NotificationOrderRequestValidator().Validate(LegacyOrder(), _owner, service.Object);

        Assert.True(result.IsValid);
        service.Verify(s => s.IsSenderAllowed(_owner, AddressType.Email, "noreply@altinn.no"), Times.Once);
        service.Verify(s => s.IsSenderAllowed(_owner, AddressType.Sms, "TestSender"), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_NoServiceOwner_SkipsSenderChecks(string? serviceOwner)
    {
        var service = ServiceReturning(false);

        var result = new NotificationOrderRequestValidator().Validate(LegacyOrder(), serviceOwner, service.Object);

        Assert.True(result.IsValid);
        service.Verify(s => s.IsSenderAllowed(It.IsAny<string>(), It.IsAny<AddressType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void Validate_ChainSendersNotAllowed_ReportsPathsForRecipientAndReminder()
    {
        var service = ServiceReturning(false);

        var result = new NotificationOrderChainRequestValidator().Validate(ChainOrder(), _owner, service.Object);

        Assert.Contains(result.Errors, e => e.PropertyName == "Recipient.RecipientPerson.EmailSettings.SenderEmailAddress");
        Assert.Contains(result.Errors, e => e.PropertyName == "Reminders[0].Recipient.RecipientEmail.Settings.SenderEmailAddress");
        Assert.All(result.Errors.Where(e => e.PropertyName.EndsWith("SenderEmailAddress")), e => Assert.Equal("The sender email address is not registered for the service owner.", e.ErrorMessage));
    }

    [Fact]
    public void Validate_ChainSendersAllowed_IsValid()
    {
        var result = new NotificationOrderChainRequestValidator().Validate(ChainOrder(), _owner, ServiceReturning(true).Object);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_SmsSenderWithWhitespace_ChecksTrimmedSenderForOwner()
    {
        var service = ServiceReturning(true);
        var options = new SmsSendingOptionsExt { Body = "Body", Sender = " Sender " };

        new SmsSendingOptionsValidator().Validate(options, _owner, service.Object);

        service.Verify(s => s.IsSenderAllowed(_owner, AddressType.Sms, "Sender"), Times.Once);
    }

    [Fact]
    public void Validate_ServiceOwnerWithoutServiceOwnerServiceInContext_ThrowsInvalidOperationException()
    {
        var context = new ValidationContext<SmsSendingOptionsExt?>(new SmsSendingOptionsExt { Body = "Body", Sender = "Sender" });
        context.RootContextData[SenderOwnershipRules.ServiceOwnerContextKey] = _owner;

        Assert.Throws<InvalidOperationException>(() => new SmsSendingOptionsValidator().Validate(context));
    }

    private static Mock<IServiceOwnerService> ServiceReturning(bool allowed)
    {
        var service = new Mock<IServiceOwnerService>();
        service
            .Setup(s => s.IsSenderAllowed(It.IsAny<string>(), It.IsAny<AddressType>(), It.IsAny<string?>()))
            .Returns(allowed);

        return service;
    }

    private static NotificationOrderRequestExt LegacyOrder() => new()
    {
        NotificationChannel = NotificationChannelExt.EmailPreferred,
        EmailTemplate = new EmailTemplateExt { Subject = "Test", Body = "Test Body", FromAddress = "noreply@altinn.no" },
        SmsTemplate = new SmsTemplateExt { Body = "Test Body", SenderNumber = "TestSender" },
        Recipients = [new RecipientExt { NationalIdentityNumber = "16069412345" }]
    };

    private static NotificationOrderChainRequestExt ChainOrder() => new()
    {
        IdempotencyId = "id",
        Recipient = new NotificationRecipientExt
        {
            RecipientPerson = new RecipientPersonExt
            {
                NationalIdentityNumber = "00000000000",
                ResourceId = "urn:altinn:resource:te_svc123",
                ChannelSchema = NotificationChannelExt.Email,
                EmailSettings = new EmailSendingOptionsExt
                {
                    SenderEmailAddress = "noreply-te@example.com",
                    Subject = "Subject",
                    Body = "Body"
                }
            }
        },
        Reminders = new List<NotificationReminderExt>
        {
            new NotificationReminderExt
            {
                DelayDays = 7,
                Recipient = new NotificationRecipientExt
                {
                    RecipientEmail = new RecipientEmailExt
                    {
                        EmailAddress = "ola.normann@example.com",
                        Settings = new EmailSendingOptionsExt
                        {
                            SenderEmailAddress = "reminder@example.com",
                            Subject = "Subject",
                            Body = "Body"
                        }
                    }
                }
            }
        }
    };
}
