using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class NotificationOrderChainMapperTests
{
    [Fact]
    public void MapToNotificationOrderChainRequest_WithDialogportenAssociation_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = DateTime.UtcNow,
            IdempotencyId = "63404F51-2079-4598-BD23-8F4467590FB4",
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test body",
                        Subject = "Test subject"
                    }
                }
            },
            DialogportenAssociation = new DialogportenIdentifiersExt
            {
                DialogId = "dialog-50E18947",
                TransmissionId = "transmission-9B0B2781"
            }
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.DialogportenAssociation);
        Assert.Equal("dialog-50E18947", result.DialogportenAssociation.DialogId);
        Assert.Equal("transmission-9B0B2781", result.DialogportenAssociation.TransmissionId);
    }

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
    public void MapToNotificationOrderChainRequest_WithEmailRecipientAndNullSenderName_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            SendersReference = "ref-E2F5A6B7",
            RequestedSendTime = DateTime.UtcNow,
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            IdempotencyId = "9C0D1E2F-3A4B-5C6D-7E8F-9A0B1C2D3E4F",
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        SenderName = null,
                        Body = "Email body",
                        Subject = "Email subject",
                        SenderEmailAddress = "sender@example.com",
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
        Assert.NotNull(result.Recipient.RecipientEmail);

        // Verify SenderName is null
        Assert.Null(result.Recipient.RecipientEmail.Settings.SenderName);

        // Verify SenderEmailAddress is set to empty string when SenderName is null
        Assert.Equal(string.Empty, result.Recipient.RecipientEmail.Settings.SenderEmailAddress);

        // Verify other properties are correctly mapped
        Assert.Equal("Email body", result.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("Email subject", result.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientEmail.Settings.ContentType);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientEmail.Settings.SendingTimePolicy);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithEmailRecipientAndNullSenderEmailAddress_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            SendersReference = "ref-C8D9E0F1",
            RequestedSendTime = DateTime.UtcNow,
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            IdempotencyId = "2D3E4F5A-6B7C-8D9E-0F1A-2B3C4D5E6F7A",
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
                        SenderEmailAddress = null,
                        ContentType = EmailContentTypeExt.Html,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    }
                }
            }
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Recipient.RecipientEmail);

        // Verify SenderName is preserved
        Assert.Equal("Email sender name", result.Recipient.RecipientEmail.Settings.SenderName);

        // Verify SenderEmailAddress is set to empty string when it's null
        Assert.Equal(string.Empty, result.Recipient.RecipientEmail.Settings.SenderEmailAddress);

        // Verify other properties are correctly mapped
        Assert.Equal("Email body", result.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("Email subject", result.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal(EmailContentType.Html, result.Recipient.RecipientEmail.Settings.ContentType);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientEmail.Settings.SendingTimePolicy);
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

    [Fact]
    public void MapToNotificationOrderChainRequest_WithPersonRecipientEmailChannel_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = DateTime.UtcNow,
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            IdempotencyId = "EBEF8B94-3F8C-444E-BF94-6F6B1FA0417C",
            SendersReference = "1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D",
            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    IgnoreReservation = false,
                    NationalIdentityNumber = "29105573746",
                    ResourceId = "urn:altinn:resource:5432",
                    ChannelSchema = NotificationChannelExt.Email,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Email body",
                        Subject = "Email subject",
                        SenderName = "Email sender",
                        ContentType = EmailContentTypeExt.Plain,
                        SenderEmailAddress = "sender@example.com",
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
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal("EBEF8B94-3F8C-444E-BF94-6F6B1FA0417C", result.IdempotencyId);
        Assert.Equal("1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D", result.SendersReference);
        Assert.Equal(requestExt.RequestedSendTime.ToUniversalTime(), result.RequestedSendTime);

        // Person recipient validation
        Assert.NotNull(result.Recipient.RecipientPerson);
        Assert.False(result.Recipient.RecipientPerson.IgnoreReservation);
        Assert.Equal("29105573746", result.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:5432", result.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.Email, result.Recipient.RecipientPerson.ChannelSchema);

        // Email settings validation
        Assert.NotNull(result.Recipient.RecipientPerson.EmailSettings);
        Assert.Equal("Email body", result.Recipient.RecipientPerson.EmailSettings.Body);
        Assert.Equal("Email subject", result.Recipient.RecipientPerson.EmailSettings.Subject);
        Assert.Equal("Email sender", result.Recipient.RecipientPerson.EmailSettings.SenderName);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientPerson.EmailSettings.ContentType);
        Assert.Equal("sender@example.com", result.Recipient.RecipientPerson.EmailSettings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientPerson.EmailSettings.SendingTimePolicy);

        // SMS settings should be null when using Email channel
        Assert.Null(result.Recipient.RecipientPerson.SmsSettings);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientOrganization);

        // Unused objects should be null
        Assert.Null(result.Reminders);
        Assert.Null(result.DialogportenAssociation);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithPersonRecipientSmsPreferredAndReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var baseTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = baseTime,
            IdempotencyId = "16E1A61B-F544-420B-BB6E-B40D8815C59C",
            SendersReference = "D8E7C6B5-A493-8271-3950-F1E2D3C4B5A6",
            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    IgnoreReservation = true,
                    NationalIdentityNumber = "09087833489",
                    ResourceId = "urn:altinn:resource:1234",
                    ChannelSchema = NotificationChannelExt.SmsPreferred,
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "SMS body",
                        Sender = "SMS sender",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    },
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Email body",
                        Subject = "Email subject",
                        SenderName = "Email sender",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
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
                        RecipientPerson = new RecipientPersonExt
                        {
                            IgnoreReservation = true,
                            NationalIdentityNumber = "09087833489",
                            ResourceId = "urn:altinn:resource:1234",
                            ChannelSchema = NotificationChannelExt.SmsPreferred,
                            SmsSettings = new SmsSendingOptionsExt
                            {
                                Body = "Reminder 1 SMS body",
                                Sender = "Reminder 1 SMS sender",
                                SendingTimePolicy = SendingTimePolicyExt.Daytime
                            },
                            EmailSettings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 1 email body",
                                Subject = "Reminder 1 email subject",
                                SenderName = "Reminder 1 email sender",
                                SenderEmailAddress = "sender@example.com",
                                ContentType = EmailContentTypeExt.Plain,
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
                        RecipientPerson = new RecipientPersonExt
                        {
                            IgnoreReservation = true,
                            NationalIdentityNumber = "09087833489",
                            ResourceId = "urn:altinn:resource:1234",
                            ChannelSchema = NotificationChannelExt.Sms,
                            SmsSettings = new SmsSendingOptionsExt
                            {
                                Body = "Reminder 2 SMS body",
                                Sender = "Reminder 2 SMS sender",
                                SendingTimePolicy = SendingTimePolicyExt.Anytime
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

        // Main order verification
        Assert.Equal(baseTime.ToUniversalTime(), result.RequestedSendTime);
        Assert.Equal("D8E7C6B5-A493-8271-3950-F1E2D3C4B5A6", result.SendersReference);

        // Person recipient validation
        Assert.NotNull(result.Recipient.RecipientPerson);
        Assert.True(result.Recipient.RecipientPerson.IgnoreReservation);
        Assert.Equal("09087833489", result.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:1234", result.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.SmsPreferred, result.Recipient.RecipientPerson.ChannelSchema);

        // SMS settings validation for main notification
        Assert.NotNull(result.Recipient.RecipientPerson.SmsSettings);
        Assert.Equal("SMS body", result.Recipient.RecipientPerson.SmsSettings.Body);
        Assert.Equal("SMS sender", result.Recipient.RecipientPerson.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientPerson.SmsSettings.SendingTimePolicy);

        // Email settings validation for main notification
        Assert.NotNull(result.Recipient.RecipientPerson.EmailSettings);
        Assert.Equal("Email body", result.Recipient.RecipientPerson.EmailSettings.Body);
        Assert.Equal("Email subject", result.Recipient.RecipientPerson.EmailSettings.Subject);
        Assert.Equal("Email sender", result.Recipient.RecipientPerson.EmailSettings.SenderName);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientPerson.EmailSettings.ContentType);
        Assert.Equal("sender@example.com", result.Recipient.RecipientPerson.EmailSettings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientPerson.EmailSettings.SendingTimePolicy);

        // Reminders verification
        Assert.NotNull(result.Reminders);
        Assert.Equal(2, result.Reminders.Count);

        // First reminder verification
        var firstReminder = result.Reminders[0];
        Assert.Equal(2, firstReminder.DelayDays);
        Assert.Equal("12236E1A-C7D9-4334-8CEE-873DAA64467F", firstReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[0].ConditionEndpoint, firstReminder.ConditionEndpoint);

        // Verify delivery time for the first reminder
        var expectedFirstReminderDeliveryTime = baseTime.AddDays(2).ToUniversalTime();
        Assert.Equal(expectedFirstReminderDeliveryTime, firstReminder.RequestedSendTime);

        // Person recipient validation for first reminder
        Assert.NotNull(firstReminder.Recipient.RecipientPerson);
        Assert.True(firstReminder.Recipient.RecipientPerson.IgnoreReservation);
        Assert.Equal("09087833489", firstReminder.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:1234", firstReminder.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.SmsPreferred, firstReminder.Recipient.RecipientPerson.ChannelSchema);

        // SMS settings validation for first reminder
        Assert.NotNull(firstReminder.Recipient.RecipientPerson.SmsSettings);
        Assert.Equal("Reminder 1 SMS body", firstReminder.Recipient.RecipientPerson.SmsSettings.Body);
        Assert.Equal("Reminder 1 SMS sender", firstReminder.Recipient.RecipientPerson.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, firstReminder.Recipient.RecipientPerson.SmsSettings.SendingTimePolicy);

        // Email settings validation for first reminder
        Assert.NotNull(firstReminder.Recipient.RecipientPerson.EmailSettings);
        Assert.Equal("Reminder 1 email body", firstReminder.Recipient.RecipientPerson.EmailSettings.Body);
        Assert.Equal("Reminder 1 email subject", firstReminder.Recipient.RecipientPerson.EmailSettings.Subject);
        Assert.Equal(EmailContentType.Plain, firstReminder.Recipient.RecipientPerson.EmailSettings.ContentType);
        Assert.Equal("Reminder 1 email sender", firstReminder.Recipient.RecipientPerson.EmailSettings.SenderName);
        Assert.Equal("sender@example.com", firstReminder.Recipient.RecipientPerson.EmailSettings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, firstReminder.Recipient.RecipientPerson.EmailSettings.SendingTimePolicy);

        // Verify first reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, firstReminder.OrderId);
        Assert.NotEqual(result.OrderId, firstReminder.OrderId);

        // Second reminder verification
        var secondReminder = result.Reminders[1];
        Assert.Equal(5, secondReminder.DelayDays);
        Assert.Equal("7B1A786D-4767-4113-8401-836D1D176BC2", secondReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[1].ConditionEndpoint, secondReminder.ConditionEndpoint);

        // Verify delivery time for the second reminder
        var expectedSecondReminderDeliveryTime = baseTime.AddDays(5).ToUniversalTime();
        Assert.Equal(expectedSecondReminderDeliveryTime, secondReminder.RequestedSendTime);

        // Person recipient validation for second reminder
        Assert.NotNull(secondReminder.Recipient.RecipientPerson);
        Assert.True(secondReminder.Recipient.RecipientPerson.IgnoreReservation);
        Assert.Equal("09087833489", secondReminder.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:1234", secondReminder.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.Sms, secondReminder.Recipient.RecipientPerson.ChannelSchema);

        // SMS settings validation for second reminder
        Assert.NotNull(secondReminder.Recipient.RecipientPerson.SmsSettings);
        Assert.Equal("Reminder 2 SMS body", secondReminder.Recipient.RecipientPerson.SmsSettings.Body);
        Assert.Equal("Reminder 2 SMS sender", secondReminder.Recipient.RecipientPerson.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Anytime, secondReminder.Recipient.RecipientPerson.SmsSettings.SendingTimePolicy);

        // Email settings should be null when using SMS channel
        Assert.Null(secondReminder.Recipient.RecipientPerson.EmailSettings);

        // Verify reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, secondReminder.OrderId);
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);

        // Verify reminders have unique OrderIds
        Assert.NotEqual(firstReminder.OrderId, secondReminder.OrderId);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithOrganizationRecipientEmailChannel_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = DateTime.UtcNow,
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            IdempotencyId = "2F3A4B5C-6D7E-8F9A-0B1C-2D3E4F5A6B7C",
            SendersReference = "CF537A1B-43E0-4917-9D61-83F28C8667C8",
            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "910150804",
                    ResourceId = "urn:altinn:resource:7890",
                    ChannelSchema = NotificationChannelExt.Email,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Organization email body",
                        Subject = "Organization email subject",
                        ContentType = EmailContentTypeExt.Plain,
                        SenderName = "Organization email sender",
                        SenderEmailAddress = "org-sender@example.com",
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
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal("2F3A4B5C-6D7E-8F9A-0B1C-2D3E4F5A6B7C", result.IdempotencyId);
        Assert.Equal("CF537A1B-43E0-4917-9D61-83F28C8667C8", result.SendersReference);
        Assert.Equal(requestExt.RequestedSendTime.ToUniversalTime(), result.RequestedSendTime);

        // Organization recipient validation
        Assert.NotNull(result.Recipient.RecipientOrganization);
        Assert.Equal("910150804", result.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:7890", result.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.Email, result.Recipient.RecipientOrganization.ChannelSchema);

        // Email settings validation
        Assert.NotNull(result.Recipient.RecipientOrganization.EmailSettings);
        Assert.Equal("Organization email body", result.Recipient.RecipientOrganization.EmailSettings.Body);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal("Organization email subject", result.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal("Organization email sender", result.Recipient.RecipientOrganization.EmailSettings.SenderName);
        Assert.Equal("org-sender@example.com", result.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);

        // SMS settings should be null when using Email channel
        Assert.Null(result.Recipient.RecipientOrganization.SmsSettings);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientPerson);

        // Unused objects should be null
        Assert.Null(result.Reminders);
        Assert.Null(result.DialogportenAssociation);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithOrganizationRecipientEmailPreferredAndReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var baseTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = baseTime,
            IdempotencyId = "7A8B9C0D-1E2F-3A4B-5C6D-7E8F9A0B1C2D",
            SendersReference = "4567890A-BCDE-F123-4567-89ABCDEF1234",
            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:5678",
                    ChannelSchema = NotificationChannelExt.EmailPreferred,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Organization email body",
                        Subject = "Organization email subject",
                        SenderName = "Organization email sender",
                        SenderEmailAddress = "org-sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    },
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "Organization SMS body",
                        Sender = "Organization SMS sender",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
                {
                    DelayDays = 3,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientOrganization = new RecipientOrganizationExt
                        {
                            OrgNumber = "987654321",
                            ResourceId = "urn:altinn:resource:5678",
                            ChannelSchema = NotificationChannelExt.EmailPreferred,
                            EmailSettings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 1 org email body",
                                Subject = "Reminder 1 org email subject",
                                SenderName = "Reminder 1 org email sender",
                                SenderEmailAddress = "reminder-org-sender@example.com",
                                ContentType = EmailContentTypeExt.Html,
                                SendingTimePolicy = SendingTimePolicyExt.Anytime
                            },
                            SmsSettings = new SmsSendingOptionsExt
                            {
                                Body = "Reminder 1 org SMS body",
                                Sender = "Reminder 1 org SMS sender",
                                SendingTimePolicy = SendingTimePolicyExt.Daytime
                            }
                        }
                    },
                    SendersReference = "5C6D7E8F-9A0B-1C2D-3E4F-5A6B7C8D9E0F",
                    ConditionEndpoint = new Uri("https://vg.no/first-org-reminder-condition")
                },
                new NotificationReminderExt
                {
                    DelayDays = 7,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientOrganization = new RecipientOrganizationExt
                        {
                            OrgNumber = "987654321",
                            ResourceId = "urn:altinn:resource:5678",
                            ChannelSchema = NotificationChannelExt.Email,
                            EmailSettings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 2 org email body",
                                Subject = "Reminder 2 org email subject",
                                SenderName = "Reminder 2 org email sender",
                                SenderEmailAddress = "reminder2-org-sender@example.com",
                                ContentType = EmailContentTypeExt.Plain,
                                SendingTimePolicy = SendingTimePolicyExt.Anytime
                            }
                        }
                    },
                    SendersReference = "1C2D3E4F-5A6B-7C8D-9E0F-1A2B3C4D5E6F",
                    ConditionEndpoint = new Uri("https://vg.no/second-org-reminder-condition")
                }
            ]
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);

        // Main order verification
        Assert.Equal(baseTime.ToUniversalTime(), result.RequestedSendTime);
        Assert.Equal("4567890A-BCDE-F123-4567-89ABCDEF1234", result.SendersReference);

        // Organization recipient validation
        Assert.NotNull(result.Recipient.RecipientOrganization);
        Assert.Equal("987654321", result.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:5678", result.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.EmailPreferred, result.Recipient.RecipientOrganization.ChannelSchema);

        // Email settings validation for main notification
        Assert.NotNull(result.Recipient.RecipientOrganization.EmailSettings);
        Assert.Equal("Organization email body", result.Recipient.RecipientOrganization.EmailSettings.Body);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal("Organization email subject", result.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal("Organization email sender", result.Recipient.RecipientOrganization.EmailSettings.SenderName);
        Assert.Equal("org-sender@example.com", result.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);

        // SMS settings validation for main notification
        Assert.NotNull(result.Recipient.RecipientOrganization.SmsSettings);
        Assert.Equal("Organization SMS body", result.Recipient.RecipientOrganization.SmsSettings.Body);
        Assert.Equal("Organization SMS sender", result.Recipient.RecipientOrganization.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientOrganization.SmsSettings.SendingTimePolicy);

        // Reminders verification
        Assert.NotNull(result.Reminders);
        Assert.Equal(2, result.Reminders.Count);

        // First reminder verification
        var firstReminder = result.Reminders[0];
        Assert.Equal(3, firstReminder.DelayDays);
        Assert.Equal("5C6D7E8F-9A0B-1C2D-3E4F-5A6B7C8D9E0F", firstReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[0].ConditionEndpoint, firstReminder.ConditionEndpoint);

        // Verify delivery time for the first reminder
        var expectedFirstReminderDeliveryTime = baseTime.AddDays(3).ToUniversalTime();
        Assert.Equal(expectedFirstReminderDeliveryTime, firstReminder.RequestedSendTime);

        // Organization recipient validation for first reminder
        Assert.NotNull(firstReminder.Recipient.RecipientOrganization);
        Assert.Equal("987654321", firstReminder.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:5678", firstReminder.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.EmailPreferred, firstReminder.Recipient.RecipientOrganization.ChannelSchema);

        // Email settings validation for first reminder
        Assert.NotNull(firstReminder.Recipient.RecipientOrganization.EmailSettings);
        Assert.Equal("Reminder 1 org email body", firstReminder.Recipient.RecipientOrganization.EmailSettings.Body);
        Assert.Equal("Reminder 1 org email subject", firstReminder.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal("Reminder 1 org email sender", firstReminder.Recipient.RecipientOrganization.EmailSettings.SenderName);
        Assert.Equal("reminder-org-sender@example.com", firstReminder.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);
        Assert.Equal(EmailContentType.Html, firstReminder.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal(SendingTimePolicy.Anytime, firstReminder.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);

        // SMS settings validation for first reminder
        Assert.NotNull(firstReminder.Recipient.RecipientOrganization.SmsSettings);
        Assert.Equal("Reminder 1 org SMS body", firstReminder.Recipient.RecipientOrganization.SmsSettings.Body);
        Assert.Equal("Reminder 1 org SMS sender", firstReminder.Recipient.RecipientOrganization.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, firstReminder.Recipient.RecipientOrganization.SmsSettings.SendingTimePolicy);

        // Verify first reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, firstReminder.OrderId);
        Assert.NotEqual(result.OrderId, firstReminder.OrderId);

        // Second reminder verification
        var secondReminder = result.Reminders[1];
        Assert.Equal(7, secondReminder.DelayDays);
        Assert.Equal("1C2D3E4F-5A6B-7C8D-9E0F-1A2B3C4D5E6F", secondReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[1].ConditionEndpoint, secondReminder.ConditionEndpoint);

        // Verify delivery time for the second reminder
        var expectedSecondReminderDeliveryTime = baseTime.AddDays(7).ToUniversalTime();
        Assert.Equal(expectedSecondReminderDeliveryTime, secondReminder.RequestedSendTime);

        // Organization recipient validation for second reminder
        Assert.NotNull(secondReminder.Recipient.RecipientOrganization);
        Assert.Equal("987654321", secondReminder.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:5678", secondReminder.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.Email, secondReminder.Recipient.RecipientOrganization.ChannelSchema);

        // Email settings validation for second reminder
        Assert.NotNull(secondReminder.Recipient.RecipientOrganization.EmailSettings);
        Assert.Equal("Reminder 2 org email body", secondReminder.Recipient.RecipientOrganization.EmailSettings.Body);
        Assert.Equal("Reminder 2 org email subject", secondReminder.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal("Reminder 2 org email sender", secondReminder.Recipient.RecipientOrganization.EmailSettings.SenderName);
        Assert.Equal("reminder2-org-sender@example.com", secondReminder.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);
        Assert.Equal(EmailContentType.Plain, secondReminder.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal(SendingTimePolicy.Anytime, secondReminder.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);

        // SMS settings should be null when using Email channel
        Assert.Null(secondReminder.Recipient.RecipientOrganization.SmsSettings);

        // Verify reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, secondReminder.OrderId);
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);

        // Verify reminders have unique OrderIds
        Assert.NotEqual(firstReminder.OrderId, secondReminder.OrderId);
    }

}
