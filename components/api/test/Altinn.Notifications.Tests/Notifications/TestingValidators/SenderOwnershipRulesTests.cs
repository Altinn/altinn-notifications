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
    public void WhenLegacySmsOrderSenderIsAllowed_ThenOrderIsValid()
    {
        var service = CreateServiceOwnerService(true);

        var result = new SmsNotificationOrderRequestValidator().Validate(CreateLegacySmsOrderRequest(), _owner, service.Object);

        Assert.True(result.IsValid);
        service.Verify(s => s.IsSenderAllowed(_owner, AddressType.Sms, "TestSender"), Times.Once);
    }

    [Fact]
    public void WhenLegacyOrderSendersAreAllowed_ThenOrderIsValid()
    {
        var service = CreateServiceOwnerService(true);

        var result = new NotificationOrderRequestValidator().Validate(CreateNotificationOrderRequest(), _owner, service.Object);

        Assert.True(result.IsValid);

        service.Verify(s => s.IsSenderAllowed(_owner, AddressType.Sms, "TestSender"), Times.Once);
        service.Verify(s => s.IsSenderAllowed(_owner, AddressType.Email, "noreply@altinn.no"), Times.Once);
    }

    [Fact]
    public void WhenLegacySmsOrderSenderIsNotAllowed_ThenSmsErrorIsReported()
    {
        var service = CreateServiceOwnerService(false);

        var result = new SmsNotificationOrderRequestValidator().Validate(CreateLegacySmsOrderRequest(), _owner, service.Object);

        var error = Assert.Single(result.Errors);

        Assert.Equal("SenderNumber", error.PropertyName);
        Assert.Equal("The SMS sender is not registered for the service owner.", error.ErrorMessage);

        Assert.DoesNotContain("TestSender", error.ErrorMessage);
    }

    [Fact]
    public void WhenLegacyOrderSendersAreNotAllowed_ThenEmailAndSmsErrorsAreReported()
    {
        var service = CreateServiceOwnerService(false);

        var result = new NotificationOrderRequestValidator().Validate(CreateNotificationOrderRequest(), _owner, service.Object);

        Assert.Equal(2, result.Errors.Count);

        Assert.Equal("The SMS sender is not registered for the service owner.", result.Errors.Single(e => e.PropertyName == "SmsTemplate.SenderNumber").ErrorMessage);
        Assert.Equal("The sender email address is not registered for the service owner.", result.Errors.Single(e => e.PropertyName == "EmailTemplate.FromAddress").ErrorMessage);
        
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage.Contains("noreply@altinn.no") || e.ErrorMessage.Contains("TestSender"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("  ")]
    public void WhenServiceOwnerIsMissing_ThenSenderChecksAreSkipped(string? serviceOwner)
    {
        var service = CreateServiceOwnerService(false);

        var result = new NotificationOrderRequestValidator().Validate(CreateNotificationOrderRequest(), serviceOwner, service.Object);

        Assert.True(result.IsValid);

        service.Verify(s => s.IsSenderAllowed(It.IsAny<string>(), It.IsAny<AddressType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void WhenSendersInOrderChainAreAllowed_ThenOrderIsValid()
    {
        var result = new NotificationOrderChainRequestValidator().Validate(CreateOrderChainRequest(), _owner, CreateServiceOwnerService(true).Object);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void WhenSendersInOrderChainAreNotAllowed_ThenErrorsAreReportedForRecipientAndReminder()
    {
        var service = CreateServiceOwnerService(false);

        var result = new NotificationOrderChainRequestValidator().Validate(CreateOrderChainRequest(), _owner, service.Object);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("The sender email address is not registered for the service owner.", result.Errors.Single(e => e.PropertyName == "Recipient.RecipientPerson.EmailSettings.SenderEmailAddress").ErrorMessage);
        Assert.Equal("The SMS sender is not registered for the service owner.", result.Errors.Single(e => e.PropertyName == "Reminders[0].Recipient.RecipientSms.Settings.Sender").ErrorMessage);
    }

    [Fact]
    public void WhenSmsSenderHasSurroundingWhitespace_ThenTrimmedSenderIsChecked()
    {
        var service = CreateServiceOwnerService(true);
        var options = new SmsSendingOptionsExt { Body = "Body", Sender = " Sender " };

        new SmsSendingOptionsValidator().Validate(options, _owner, service.Object);

        service.Verify(s => s.IsSenderAllowed(_owner, AddressType.Sms, "Sender"), Times.Once);
    }

    [Fact]
    public void WhenServiceOwnerIsSetWithoutServiceOwnerService_ThenInvalidOperationExceptionIsThrown()
    {
        var context = new ValidationContext<SmsSendingOptionsExt?>(new SmsSendingOptionsExt { Body = "Body", Sender = "Sender" });
        context.RootContextData[SenderOwnershipRules.ServiceOwnerContextKey] = _owner;

        Assert.Throws<InvalidOperationException>(() => new SmsSendingOptionsValidator().Validate(context));
    }

    /// <summary>
    /// Creates a mocked service owner service that returns the same result for every sender check.
    /// </summary>
    /// <param name="allowed">The result returned by every call to <see cref="IServiceOwnerService.IsSenderAllowed"/>.</param>
    /// <returns>The mocked service owner service.</returns>
    private static Mock<IServiceOwnerService> CreateServiceOwnerService(bool allowed)
    {
        var service = new Mock<IServiceOwnerService>();
        service
            .Setup(s => s.IsSenderAllowed(It.IsAny<string>(), It.IsAny<AddressType>(), It.IsAny<string?>()))
            .Returns(allowed);

        return service;
    }

    /// <summary>
    /// Creates a valid order request for the legacy endpoint <c>notifications/api/v1/orders</c>,
    /// with the email sender "noreply@altinn.no" and the SMS sender "TestSender".
    /// </summary>
    /// <returns>The legacy order request.</returns>
    private static NotificationOrderRequestExt CreateNotificationOrderRequest() => new()
    {
        NotificationChannel = NotificationChannelExt.EmailPreferred,
        Recipients = [new RecipientExt { NationalIdentityNumber = "16069412345" }],
        SmsTemplate = new SmsTemplateExt { Body = "Test Body", SenderNumber = "TestSender" },
        EmailTemplate = new EmailTemplateExt { Subject = "Test", Body = "Test Body", FromAddress = "noreply@altinn.no" }
    };

    /// <summary>
    /// Creates a valid SMS order request for the legacy endpoint <c>notifications/api/v1/orders/sms</c>,
    /// with the SMS sender "TestSender".
    /// </summary>
    /// <returns>The legacy SMS order request.</returns>
    private static SmsNotificationOrderRequestExt CreateLegacySmsOrderRequest() => new()
    {
        Body = "Test Body",
        SenderNumber = "TestSender",
        Recipients = [new RecipientExt { NationalIdentityNumber = "16069412345" }]
    };

    /// <summary>
    /// Creates a valid order chain request with the email sender "noreply-te@example.com" for the main recipient
    /// and the SMS sender "ReminderSender" for its single reminder.
    /// </summary>
    /// <returns>The order chain request.</returns>
    private static NotificationOrderChainRequestExt CreateOrderChainRequest() => new()
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
                    Body = "Body",
                    Subject = "Subject",
                    SenderEmailAddress = "noreply-te@example.com"
                }
            }
        },
        Reminders =
        [
            new()
            {
                DelayDays = 7,
                Recipient = new NotificationRecipientExt
                {
                    RecipientSms = new RecipientSmsExt
                    {
                        PhoneNumber = "+4799999999",
                        Settings = new SmsSendingOptionsExt
                        {
                            Body = "Body",
                            Sender = "ReminderSender"
                        }
                    }
                }
            }
        ]
    };
}
