using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class NotificationOrderChainMapperTests
{
    [Fact]
    public void MapToNotificationOrderChainRequest_WithEmailRecipient_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            SendersReference = "ref-D1E4B80C",
            RequestedSendTime = DateTime.UtcNow,
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            IdempotencyId = "EBEF8B94-3F8C-444E-BF94-6F6B1FA0417C",
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Email body",
                        Subject = "Email subject",
                        SenderName = "Email sender name",
                        SenderEmailAddress = "Email sender address",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.Equal("ref-D1E4B80C", result.SendersReference);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal("EBEF8B94-3F8C-444E-BF94-6F6B1FA0417C", result.IdempotencyId);
        Assert.Equal(requestExt.RequestedSendTime.ToUniversalTime(), result.RequestedSendTime);

        // Email recipient validation
        Assert.NotNull(result.Recipient.RecipientEmail);
        Assert.Equal("Email body", result.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("Email subject", result.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal("recipient@example.com", result.Recipient.RecipientEmail.EmailAddress);
        Assert.Equal("Email sender name", result.Recipient.RecipientEmail.Settings.SenderName);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientEmail.Settings.ContentType);
        Assert.Equal("Email sender address", result.Recipient.RecipientEmail.Settings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientEmail.Settings.SendingTimePolicy);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientPerson);
        Assert.Null(result.Recipient.RecipientOrganization);

        // Unused objects should be null
        Assert.Null(result.Reminders);
        Assert.Null(result.DialogportenAssociation);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithEmailRecipientAndMultipleReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var baseTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = baseTime,
            IdempotencyId = "16E1A61B-F544-420B-BB6E-B40D8815C59C",
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Email body",
                        Subject = "Email subject",
                        SenderName = "Email sender name",
                        SenderEmailAddress = "Email sender address",
                        ContentType = EmailContentTypeExt.Plain
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
                {
                    DelayDays = 2,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientEmail = new RecipientEmailExt
                        {
                            EmailAddress = "recipient@example.com",
                            Settings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 1 body",
                                Subject = "Reminder 1 subject",
                                SenderName = "Reminder 1 sender name",
                                SenderEmailAddress = "Reminder 1 sender address",
                                ContentType = EmailContentTypeExt.Plain
                            }
                        }
                    },
                    SendersReference = "12236E1A-C7D9-4334-8CEE-873DAA64467F",
                    ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition")
                },
                new NotificationReminderExt
                {
                    DelayDays = 5,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientEmail = new RecipientEmailExt
                        {
                            EmailAddress = "recipient@example.com",
                            Settings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 2 body",
                                Subject = "Reminder 2 subject",
                                SenderName = "Reminder 2 sender name",
                                SenderEmailAddress = "Reminder 2 sender address",
                                ContentType = EmailContentTypeExt.Plain
                            }
                        }
                    },
                    SendersReference = "7B1A786D-4767-4113-8401-836D1D176BC2",
                    ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition")
                }
            ]
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Reminders);
        Assert.Equal(2, result.Reminders.Count);

        // Verify basic information for the first reminder
        var firstReminder = result.Reminders[0];
        Assert.Equal(2, firstReminder.DelayDays);
        Assert.Equal("12236E1A-C7D9-4334-8CEE-873DAA64467F", firstReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[0].ConditionEndpoint, firstReminder.ConditionEndpoint);

        // Verify delivery time for the first reminder
        var expectedFirstReminderDeliveryTime = baseTime.AddDays(2).ToUniversalTime();
        Assert.Equal(expectedFirstReminderDeliveryTime, firstReminder.RequestedSendTime);

        // Verify first reminder recipient
        Assert.NotNull(firstReminder.Recipient.RecipientEmail);
        Assert.Equal("Reminder 1 body", firstReminder.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("recipient@example.com", firstReminder.Recipient.RecipientEmail.EmailAddress);
        Assert.Equal("Reminder 1 subject", firstReminder.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal(EmailContentType.Plain, firstReminder.Recipient.RecipientEmail.Settings.ContentType);
        Assert.Equal("Reminder 1 sender name", firstReminder.Recipient.RecipientEmail.Settings.SenderName);
        Assert.Equal("Reminder 1 sender address", firstReminder.Recipient.RecipientEmail.Settings.SenderEmailAddress);

        // Verify first reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, firstReminder.OrderId);
        Assert.NotEqual(result.OrderId, firstReminder.OrderId);

        // Verify basic information for the second reminder
        var secondReminder = result.Reminders[1];
        Assert.Equal(5, secondReminder.DelayDays);
        Assert.Equal("7B1A786D-4767-4113-8401-836D1D176BC2", secondReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[1].ConditionEndpoint, secondReminder.ConditionEndpoint);

        // Verify delivery time for the second reminder
        var expectedSecondReminderDeliveryTime = baseTime.AddDays(5).ToUniversalTime();
        Assert.Equal(expectedSecondReminderDeliveryTime, secondReminder.RequestedSendTime);

        // Verify second reminder recipient
        Assert.NotNull(secondReminder.Recipient.RecipientEmail);
        Assert.Equal("Reminder 2 body", secondReminder.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("recipient@example.com", secondReminder.Recipient.RecipientEmail.EmailAddress);
        Assert.Equal("Reminder 2 subject", secondReminder.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal(EmailContentType.Plain, secondReminder.Recipient.RecipientEmail.Settings.ContentType);
        Assert.Equal("Reminder 2 sender name", secondReminder.Recipient.RecipientEmail.Settings.SenderName);
        Assert.Equal("Reminder 2 sender address", secondReminder.Recipient.RecipientEmail.Settings.SenderEmailAddress);

        // Verify reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, secondReminder.OrderId);
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);

        // Verify have unique OrderIds
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithSmsRecipient_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {   
            SendersReference = "ref-D1E4B80C",
            RequestedSendTime = DateTime.UtcNow,
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            IdempotencyId = "EBEF8B94-3F8C-444E-BF94-6F6B1FA0417C",
            Recipient = new NotificationRecipientExt
            {
                RecipientSms = new RecipientSmsExt
                {
                    PhoneNumber = "+4799999999",
                    Settings = new SmsSendingOptionsExt
                    {
                        Body = "SMS body",
                        Sender = "SMS sender",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    }
                }
            }
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.Equal("ref-D1E4B80C", result.SendersReference);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal("EBEF8B94-3F8C-444E-BF94-6F6B1FA0417C", result.IdempotencyId);
        Assert.Equal(requestExt.RequestedSendTime.ToUniversalTime(), result.RequestedSendTime);

        // SMS recipient validation
        Assert.NotNull(result.Recipient.RecipientSms);
        Assert.Equal("SMS body", result.Recipient.RecipientSms.Settings.Body);
        Assert.Equal("SMS sender", result.Recipient.RecipientSms.Settings.Sender);
        Assert.Equal("+4799999999", result.Recipient.RecipientSms.PhoneNumber);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientSms.Settings.SendingTimePolicy);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientPerson);
        Assert.Null(result.Recipient.RecipientOrganization);

        // Unused objects should be null
        Assert.Null(result.Reminders);
        Assert.Null(result.DialogportenAssociation);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithSmsRecipientAndMultipleReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var baseTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = baseTime,
            IdempotencyId = "16E1A61B-F544-420B-BB6E-B40D8815C59C",
            Recipient = new NotificationRecipientExt
            {
                RecipientSms = new RecipientSmsExt
                {
                    PhoneNumber = "+4799999999",
                    Settings = new SmsSendingOptionsExt
                    {
                        Body = "SMS body",
                        Sender = "SMS sender",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
            {
                DelayDays = 2,
                Recipient = new NotificationRecipientExt
                {
                    RecipientSms = new RecipientSmsExt
                    {
                        PhoneNumber = "+4799999999",
                        Settings = new SmsSendingOptionsExt
                        {
                            Body = "Reminder 1 SMS body",
                            Sender = "Reminder 1 SMS sender",
                            SendingTimePolicy = SendingTimePolicyExt.Anytime
                        }
                    }
                },
                SendersReference = "12236E1A-C7D9-4334-8CEE-873DAA64467F",
                ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition")
            },
            new NotificationReminderExt
            {
                DelayDays = 5,
                Recipient = new NotificationRecipientExt
                {
                    RecipientSms = new RecipientSmsExt
                    {
                        PhoneNumber = "+4799999999",
                        Settings = new SmsSendingOptionsExt
                        {
                            Body = "Reminder 2 SMS body",
                            Sender = "Reminder 2 SMS sender",
                            SendingTimePolicy = SendingTimePolicyExt.Daytime
                        }
                    }
                },
                SendersReference = "7B1A786D-4767-4113-8401-836D1D176BC2",
                ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition")
            }
            ]
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Reminders);
        Assert.Equal(2, result.Reminders.Count);

        // Verify basic information for the first reminder
        var firstReminder = result.Reminders[0];
        Assert.Equal(2, firstReminder.DelayDays);
        Assert.Equal("12236E1A-C7D9-4334-8CEE-873DAA64467F", firstReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[0].ConditionEndpoint, firstReminder.ConditionEndpoint);

        // Verify delivery time for the first reminder
        var expectedFirstReminderDeliveryTime = baseTime.AddDays(2).ToUniversalTime();
        Assert.Equal(expectedFirstReminderDeliveryTime, firstReminder.RequestedSendTime);

        // Verify first reminder recipient
        Assert.NotNull(firstReminder.Recipient.RecipientSms);
        Assert.Equal("Reminder 1 SMS body", firstReminder.Recipient.RecipientSms.Settings.Body);
        Assert.Equal("+4799999999", firstReminder.Recipient.RecipientSms.PhoneNumber);
        Assert.Equal("Reminder 1 SMS sender", firstReminder.Recipient.RecipientSms.Settings.Sender);
        Assert.Equal(SendingTimePolicy.Anytime, firstReminder.Recipient.RecipientSms.Settings.SendingTimePolicy);

        // Verify first reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, firstReminder.OrderId);
        Assert.NotEqual(result.OrderId, firstReminder.OrderId);

        // Verify basic information for the second reminder
        var secondReminder = result.Reminders[1];
        Assert.Equal(5, secondReminder.DelayDays);
        Assert.Equal("7B1A786D-4767-4113-8401-836D1D176BC2", secondReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[1].ConditionEndpoint, secondReminder.ConditionEndpoint);

        // Verify delivery time for the second reminder
        var expectedSecondReminderDeliveryTime = baseTime.AddDays(5).ToUniversalTime();
        Assert.Equal(expectedSecondReminderDeliveryTime, secondReminder.RequestedSendTime);

        // Verify second reminder recipient
        Assert.NotNull(secondReminder.Recipient.RecipientSms);
        Assert.Equal("Reminder 2 SMS body", secondReminder.Recipient.RecipientSms.Settings.Body);
        Assert.Equal("+4799999999", secondReminder.Recipient.RecipientSms.PhoneNumber);
        Assert.Equal("Reminder 2 SMS sender", secondReminder.Recipient.RecipientSms.Settings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, secondReminder.Recipient.RecipientSms.Settings.SendingTimePolicy);

        // Verify reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, secondReminder.OrderId);
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);

        // Verify reminders have unique OrderIds
        Assert.NotEqual(firstReminder.OrderId, secondReminder.OrderId);
    }
}
