using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class NotificationOrderChainMapperTests
{
    [Fact]
    public void MapToNotificationOrderChainRequest_WithEmailRecipientAndDialogportenAssociation_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            SendersReference = "ref-AB12CD34",
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
                        Subject = "Test subject",
                        SenderEmailAddress = "sender@example.com"
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

        // Verify NotificationOrderChainRequest properties
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-AB12CD34", result.SendersReference);
        Assert.Equal("63404F51-2079-4598-BD23-8F4467590FB4", result.IdempotencyId);
        Assert.Equal(requestExt.RequestedSendTime.ToUniversalTime(), result.RequestedSendTime);

        // Verify DialogportenAssociation properties
        Assert.NotNull(result.DialogportenAssociation);
        Assert.Equal("dialog-50E18947", result.DialogportenAssociation.DialogId);
        Assert.Equal("transmission-9B0B2781", result.DialogportenAssociation.TransmissionId);

        // Verify RecipientEmail properties
        Assert.NotNull(result.Recipient);
        Assert.NotNull(result.Recipient.RecipientEmail);
        Assert.Equal("Test body", result.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("Test subject", result.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal("recipient@example.com", result.Recipient.RecipientEmail.EmailAddress);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientEmail.Settings.ContentType);
        Assert.Equal("sender@example.com", result.Recipient.RecipientEmail.Settings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientEmail.Settings.SendingTimePolicy);

        // Verify other recipient types are null
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientPerson);
        Assert.Null(result.Recipient.RecipientOrganization);

        // Verify no reminders
        Assert.Null(result.Reminders);
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
            SendersReference = "ref-D1E4B80C",
            ConditionEndpoint = new Uri("https://vg.no/condition"),
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
                        SenderEmailAddress = "Email sender address",
                        ContentType = EmailContentTypeExt.Plain
                    }
                }
            },

            Reminders =
            [
                new NotificationReminderExt
                {
                    DelayDays = 3,
                    SendersReference = "12236E1A-C7D9-4334-8CEE-873DAA64467F",
                    ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition"),

                    Recipient = new NotificationRecipientExt
                    {
                        RecipientEmail = new RecipientEmailExt
                        {
                            EmailAddress = "recipient@example.com",
                            Settings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 1 body",
                                Subject = "Reminder 1 subject",
                                SenderEmailAddress = "Reminder 1 sender address",
                                ContentType = EmailContentTypeExt.Plain
                            }
                        }
                    }
                },
                new NotificationReminderExt
                {
                    DelayDays = 5,
                    SendersReference = "7B1A786D-4767-4113-8401-836D1D176BC2",
                    ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition"),

                    Recipient = new NotificationRecipientExt
                    {
                        RecipientEmail = new RecipientEmailExt
                        {
                            EmailAddress = "recipient@example.com",
                            Settings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 2 body",
                                Subject = "Reminder 2 subject",
                                SenderEmailAddress = "Reminder 2 sender address",
                                ContentType = EmailContentTypeExt.Plain
                            }
                        }
                    }
                }
            ]
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);

        // Verify NotificationOrderChainRequest properties
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-D1E4B80C", result.SendersReference);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal("16E1A61B-F544-420B-BB6E-B40D8815C59C", result.IdempotencyId);
        Assert.Equal(requestExt.RequestedSendTime.ToUniversalTime(), result.RequestedSendTime);

        // Verify RecipientEmail properties
        Assert.NotNull(result.Recipient);
        Assert.NotNull(result.Recipient.RecipientEmail);
        Assert.Equal("recipient@example.com", result.Recipient.RecipientEmail.EmailAddress);

        // Verify EmailSendingOptions properties
        Assert.NotNull(result.Recipient.RecipientEmail.Settings);
        Assert.Equal("Email body", result.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("Email subject", result.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientEmail.Settings.ContentType);
        Assert.Equal("Email sender address", result.Recipient.RecipientEmail.Settings.SenderEmailAddress);

        // Verify Reminders collection properties
        Assert.NotNull(result.Reminders);
        Assert.Equal(2, result.Reminders.Count);

        // Verify NotificationReminder properties for first reminder
        var firstReminder = result.Reminders[0];
        Assert.Equal(3, firstReminder.DelayDays);
        Assert.NotEqual(Guid.Empty, firstReminder.OrderId);
        Assert.NotEqual(result.OrderId, firstReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, firstReminder.OrderId);
        Assert.Equal("12236E1A-C7D9-4334-8CEE-873DAA64467F", firstReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[0].ConditionEndpoint, firstReminder.ConditionEndpoint);

        // Verify DateTime properties for first reminder
        var expectedFirstReminderDeliveryTime = baseTime.AddDays(3).ToUniversalTime();
        Assert.Equal(expectedFirstReminderDeliveryTime, firstReminder.RequestedSendTime);

        // Verify RecipientEmail properties for first reminder
        Assert.NotNull(firstReminder.Recipient);
        Assert.NotNull(firstReminder.Recipient.RecipientEmail);
        Assert.Equal("recipient@example.com", firstReminder.Recipient.RecipientEmail.EmailAddress);

        // Verify EmailSendingOptions properties for first reminder
        Assert.NotNull(firstReminder.Recipient.RecipientEmail.Settings);
        Assert.Equal("Reminder 1 body", firstReminder.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("Reminder 1 subject", firstReminder.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal(EmailContentType.Plain, firstReminder.Recipient.RecipientEmail.Settings.ContentType);
        Assert.Equal("Reminder 1 sender address", firstReminder.Recipient.RecipientEmail.Settings.SenderEmailAddress);

        // Verify NotificationReminder properties for second reminder
        var secondReminder = result.Reminders[1];
        Assert.Equal(5, secondReminder.DelayDays);
        Assert.NotEqual(Guid.Empty, secondReminder.OrderId);
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, secondReminder.OrderId);
        Assert.Equal("7B1A786D-4767-4113-8401-836D1D176BC2", secondReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[1].ConditionEndpoint, secondReminder.ConditionEndpoint);

        // Verify DateTime properties for second reminder
        var expectedSecondReminderDeliveryTime = baseTime.AddDays(5).ToUniversalTime();
        Assert.Equal(expectedSecondReminderDeliveryTime, secondReminder.RequestedSendTime);

        // Verify RecipientEmail properties for second reminder
        Assert.NotNull(secondReminder.Recipient);
        Assert.NotNull(secondReminder.Recipient.RecipientEmail);
        Assert.Equal("recipient@example.com", secondReminder.Recipient.RecipientEmail.EmailAddress);

        // Verify EmailSendingOptions properties for second reminder
        Assert.NotNull(secondReminder.Recipient.RecipientEmail.Settings);
        Assert.Equal("Reminder 2 body", secondReminder.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("Reminder 2 subject", secondReminder.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal(EmailContentType.Plain, secondReminder.Recipient.RecipientEmail.Settings.ContentType);
        Assert.Equal("Reminder 2 sender address", secondReminder.Recipient.RecipientEmail.Settings.SenderEmailAddress);

        // Verify OrderId uniqueness
        Assert.NotEqual(firstReminder.OrderId, secondReminder.OrderId);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithEmailRecipientAndNoReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = requestTime,
            SendersReference = "ref-D1E4B80C",
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

        // Verify NotificationOrderChainRequest properties
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-D1E4B80C", result.SendersReference);
        Assert.Equal(requestTime.ToUniversalTime(), result.RequestedSendTime);
        Assert.Equal(new Uri("https://vg.no/condition"), result.ConditionEndpoint);
        Assert.Equal("EBEF8B94-3F8C-444E-BF94-6F6B1FA0417C", result.IdempotencyId);

        // Verify RecipientEmail properties
        Assert.NotNull(result.Recipient);
        Assert.NotNull(result.Recipient.RecipientEmail);
        Assert.Equal("recipient@example.com", result.Recipient.RecipientEmail.EmailAddress);

        // Verify EmailSendingOptions properties
        Assert.NotNull(result.Recipient.RecipientEmail.Settings);
        Assert.Equal("Email body", result.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("Email subject", result.Recipient.RecipientEmail.Settings.Subject);
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
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
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
    public void MapToNotificationOrderChainRequest_WithOrganizationRecipientEmailPreferredAndNoReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = requestTime,
            SendersReference = "ref-4617F6FFBE7D",
            IdempotencyId = "G1H2I3J4-K5L6-M7N8-O9P0-Q1R2S3T4U5V6",
            ConditionEndpoint = new Uri("https://vg.no/condition"),

            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:4321",
                    ChannelSchema = NotificationChannelExt.EmailPreferred,

                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Organization email body",
                        Subject = "Organization email subject",
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
            }
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-4617F6FFBE7D", result.SendersReference);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal(requestTime.ToUniversalTime(), result.RequestedSendTime);
        Assert.Equal("G1H2I3J4-K5L6-M7N8-O9P0-Q1R2S3T4U5V6", result.IdempotencyId);

        // Organization recipient validation
        Assert.NotNull(result.Recipient.RecipientOrganization);
        Assert.Equal("987654321", result.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:4321", result.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.EmailPreferred, result.Recipient.RecipientOrganization.ChannelSchema);

        // Email settings validation
        Assert.NotNull(result.Recipient.RecipientOrganization.EmailSettings);
        Assert.Equal("Organization email body", result.Recipient.RecipientOrganization.EmailSettings.Body);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal("Organization email subject", result.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal("org-sender@example.com", result.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);

        // SMS settings validation
        Assert.NotNull(result.Recipient.RecipientOrganization.SmsSettings);
        Assert.Equal("Organization SMS body", result.Recipient.RecipientOrganization.SmsSettings.Body);
        Assert.Equal("Organization SMS sender", result.Recipient.RecipientOrganization.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientOrganization.SmsSettings.SendingTimePolicy);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientPerson);

        // Verify no reminders or DialogportenAssociation
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
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
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
        Assert.Equal(EmailContentType.Html, firstReminder.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal("Reminder 1 org email subject", firstReminder.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal(SendingTimePolicy.Anytime, firstReminder.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);
        Assert.Equal("reminder-org-sender@example.com", firstReminder.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);

        // SMS settings validation for first reminder
        Assert.NotNull(firstReminder.Recipient.RecipientOrganization.SmsSettings);
        Assert.Equal("Reminder 1 org SMS body", firstReminder.Recipient.RecipientOrganization.SmsSettings.Body);
        Assert.Equal("Reminder 1 org SMS sender", firstReminder.Recipient.RecipientOrganization.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, firstReminder.Recipient.RecipientOrganization.SmsSettings.SendingTimePolicy);

        // Verify first reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, firstReminder.OrderId);
        Assert.NotEqual(result.OrderId, firstReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, firstReminder.OrderId);

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
        Assert.Equal(EmailContentType.Plain, secondReminder.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal("Reminder 2 org email subject", secondReminder.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal(SendingTimePolicy.Anytime, secondReminder.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);
        Assert.Equal("reminder2-org-sender@example.com", secondReminder.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);

        // SMS settings should be null when using Email channel
        Assert.Null(secondReminder.Recipient.RecipientOrganization.SmsSettings);

        // Verify reminder has a unique OrderId
        Assert.NotEqual(Guid.Empty, secondReminder.OrderId);
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, secondReminder.OrderId);

        // Verify reminders have unique OrderIds
        Assert.NotEqual(firstReminder.OrderId, secondReminder.OrderId);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithOrganizationRecipientSmsPreferredAndNoReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = requestTime,
            SendersReference = "ref-4617F6FFBE7D",
            IdempotencyId = "F1E2F3G4-H5I6-J7K8-L9M0-N1O2P3Q4R5S6",
            ConditionEndpoint = new Uri("https://vg.no/condition"),

            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:4321",
                    ChannelSchema = NotificationChannelExt.SmsPreferred,
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "Organization SMS body",
                        Sender = "Organization SMS sender",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    },
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Organization email body",
                        Subject = "Organization email subject",
                        SenderEmailAddress = "org-sender@example.com",
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
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-4617F6FFBE7D", result.SendersReference);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal(requestTime.ToUniversalTime(), result.RequestedSendTime);
        Assert.Equal("F1E2F3G4-H5I6-J7K8-L9M0-N1O2P3Q4R5S6", result.IdempotencyId);

        // Organization recipient validation
        Assert.NotNull(result.Recipient.RecipientOrganization);
        Assert.Equal("987654321", result.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:4321", result.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.SmsPreferred, result.Recipient.RecipientOrganization.ChannelSchema);

        // SMS settings validation
        Assert.NotNull(result.Recipient.RecipientOrganization.SmsSettings);
        Assert.Equal("Organization SMS body", result.Recipient.RecipientOrganization.SmsSettings.Body);
        Assert.Equal("Organization SMS sender", result.Recipient.RecipientOrganization.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientOrganization.SmsSettings.SendingTimePolicy);

        // Email settings validation
        Assert.NotNull(result.Recipient.RecipientOrganization.EmailSettings);
        Assert.Equal("Organization email body", result.Recipient.RecipientOrganization.EmailSettings.Body);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal("Organization email subject", result.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal("org-sender@example.com", result.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientPerson);

        // Verify no reminders or DialogportenAssociation
        Assert.Null(result.Reminders);
        Assert.Null(result.DialogportenAssociation);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithOrganizationRecipientSmsPreferredAndReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var baseTime = DateTime.UtcNow;

        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = baseTime,
            SendersReference = "ref-F1E2D3C4B5A6",
            IdempotencyId = "H1I2J3K4-L5M6-N7O8-P9Q0-R1S2T3U4V5W6",

            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:4321",
                    ChannelSchema = NotificationChannelExt.SmsPreferred,

                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "Organization SMS body",
                        Sender = "Organization SMS sender",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    },
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Organization email body",
                        Subject = "Organization email subject",
                        SenderEmailAddress = "org-sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
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
                            ResourceId = "urn:altinn:resource:4321",
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
                                SenderEmailAddress = "org-sender@example.com",
                                ContentType = EmailContentTypeExt.Plain,
                                SendingTimePolicy = SendingTimePolicyExt.Anytime
                            }
                        }
                    },
                    SendersReference = "ref-A1B2C3D4E5F6",
                    ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition")
                },
                new NotificationReminderExt
                {
                    DelayDays = 7,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientOrganization = new RecipientOrganizationExt
                        {
                            OrgNumber = "987654321",
                            ResourceId = "urn:altinn:resource:4321",
                            ChannelSchema = NotificationChannelExt.Sms,
                            SmsSettings = new SmsSendingOptionsExt
                            {
                                Body = "Reminder 2 SMS body",
                                Sender = "Reminder 2 SMS sender",
                                SendingTimePolicy = SendingTimePolicyExt.Daytime
                            }
                        }
                    },
                    SendersReference = "ref-G1H2I3J4K5L6",
                    ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition")
                }
            ]
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal("ref-F1E2D3C4B5A6", result.SendersReference);
        Assert.Equal(baseTime.ToUniversalTime(), result.RequestedSendTime);

        // Organization recipient validation
        Assert.NotNull(result.Recipient.RecipientOrganization);
        Assert.Equal("987654321", result.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:4321", result.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.SmsPreferred, result.Recipient.RecipientOrganization.ChannelSchema);

        // SMS settings validation for main notification
        Assert.NotNull(result.Recipient.RecipientOrganization.SmsSettings);
        Assert.Equal("Organization SMS body", result.Recipient.RecipientOrganization.SmsSettings.Body);
        Assert.Equal("Organization SMS sender", result.Recipient.RecipientOrganization.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientOrganization.SmsSettings.SendingTimePolicy);

        // Email settings validation for main notification
        Assert.NotNull(result.Recipient.RecipientOrganization.EmailSettings);
        Assert.Equal("Organization email body", result.Recipient.RecipientOrganization.EmailSettings.Body);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal("Organization email subject", result.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal("org-sender@example.com", result.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);

        // Reminders verification
        Assert.NotNull(result.Reminders);
        Assert.Equal(2, result.Reminders.Count);

        // First reminder verification
        var firstReminder = result.Reminders[0];
        Assert.Equal(3, firstReminder.DelayDays);
        Assert.Equal("ref-A1B2C3D4E5F6", firstReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[0].ConditionEndpoint, firstReminder.ConditionEndpoint);

        var expectedFirstReminderDeliveryTime = baseTime.AddDays(3).ToUniversalTime();
        Assert.Equal(expectedFirstReminderDeliveryTime, firstReminder.RequestedSendTime);

        Assert.NotNull(firstReminder.Recipient.RecipientOrganization);
        Assert.Equal("987654321", firstReminder.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:4321", firstReminder.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.SmsPreferred, firstReminder.Recipient.RecipientOrganization.ChannelSchema);

        Assert.NotNull(firstReminder.Recipient.RecipientOrganization.SmsSettings);
        Assert.Equal("Reminder 1 SMS body", firstReminder.Recipient.RecipientOrganization.SmsSettings.Body);
        Assert.Equal("Reminder 1 SMS sender", firstReminder.Recipient.RecipientOrganization.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, firstReminder.Recipient.RecipientOrganization.SmsSettings.SendingTimePolicy);

        Assert.NotNull(firstReminder.Recipient.RecipientOrganization.EmailSettings);
        Assert.Equal("Reminder 1 email body", firstReminder.Recipient.RecipientOrganization.EmailSettings.Body);
        Assert.Equal("Reminder 1 email subject", firstReminder.Recipient.RecipientOrganization.EmailSettings.Subject);
        Assert.Equal(EmailContentType.Plain, firstReminder.Recipient.RecipientOrganization.EmailSettings.ContentType);
        Assert.Equal("org-sender@example.com", firstReminder.Recipient.RecipientOrganization.EmailSettings.SenderEmailAddress);
        Assert.Equal(SendingTimePolicy.Anytime, firstReminder.Recipient.RecipientOrganization.EmailSettings.SendingTimePolicy);

        // Second reminder verification
        var secondReminder = result.Reminders[1];
        Assert.Equal(7, secondReminder.DelayDays);
        Assert.Equal("ref-G1H2I3J4K5L6", secondReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[1].ConditionEndpoint, secondReminder.ConditionEndpoint);

        var expectedSecondReminderDeliveryTime = baseTime.AddDays(7).ToUniversalTime();
        Assert.Equal(expectedSecondReminderDeliveryTime, secondReminder.RequestedSendTime);

        Assert.NotNull(secondReminder.Recipient.RecipientOrganization);
        Assert.Equal("987654321", secondReminder.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:4321", secondReminder.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.Sms, secondReminder.Recipient.RecipientOrganization.ChannelSchema);

        Assert.NotNull(secondReminder.Recipient.RecipientOrganization.SmsSettings);
        Assert.Equal("Reminder 2 SMS body", secondReminder.Recipient.RecipientOrganization.SmsSettings.Body);
        Assert.Equal("Reminder 2 SMS sender", secondReminder.Recipient.RecipientOrganization.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, secondReminder.Recipient.RecipientOrganization.SmsSettings.SendingTimePolicy);

        Assert.Null(secondReminder.Recipient.RecipientOrganization.EmailSettings);

        // Verify OrderId uniqueness
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.NotEqual(result.OrderId, firstReminder.OrderId);
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, firstReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, secondReminder.OrderId);
        Assert.NotEqual(firstReminder.OrderId, secondReminder.OrderId);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithOrganizationUsingRecipientSmsChannel_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = DateTime.UtcNow,
            SendersReference = "ref-9B8D303243B6",
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            IdempotencyId = "C1D2E3F4-G5H6-I7J8-K9L0-M1N2O3P4Q5R6",

            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:4321",
                    ChannelSchema = NotificationChannelExt.Sms,
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "Organization SMS body",
                        Sender = "Organization SMS sender",
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
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-9B8D303243B6", result.SendersReference);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal("C1D2E3F4-G5H6-I7J8-K9L0-M1N2O3P4Q5R6", result.IdempotencyId);
        Assert.Equal(requestExt.RequestedSendTime.ToUniversalTime(), result.RequestedSendTime);

        // Organization recipient validation
        Assert.NotNull(result.Recipient.RecipientOrganization);
        Assert.Equal("987654321", result.Recipient.RecipientOrganization.OrgNumber);
        Assert.Equal("urn:altinn:resource:4321", result.Recipient.RecipientOrganization.ResourceId);
        Assert.Equal(NotificationChannel.Sms, result.Recipient.RecipientOrganization.ChannelSchema);

        // SMS settings validation
        Assert.NotNull(result.Recipient.RecipientOrganization.SmsSettings);
        Assert.Equal("Organization SMS body", result.Recipient.RecipientOrganization.SmsSettings.Body);
        Assert.Equal("Organization SMS sender", result.Recipient.RecipientOrganization.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientOrganization.SmsSettings.SendingTimePolicy);

        // Email settings should be null when using SMS channel
        Assert.Null(result.Recipient.RecipientOrganization.EmailSettings);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientPerson);

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
            SendersReference = "ref-D3C9BA54",
            IdempotencyId = "F1E2D3C4-B5A6-9876-5432-1098ABCDEF01",

            Recipient = new NotificationRecipientExt
            {
                RecipientSms = new RecipientSmsExt
                {
                    PhoneNumber = "+4799999999",
                    Settings = new SmsSendingOptionsExt
                    {
                        Body = "Main SMS body",
                        Sender = "Main SMS sender",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
                {
                    DelayDays = 3,
                    SendersReference = "ref-reminder-A3BCFE4284D6",
                    ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition"),

                    Recipient = new NotificationRecipientExt
                    {
                        RecipientSms = new RecipientSmsExt
                        {
                            PhoneNumber = "+4799999999",
                            Settings = new SmsSendingOptionsExt
                            {
                                Body = "Reminder 1 SMS body",
                                Sender = "Reminder 1 SMS sender",
                                SendingTimePolicy = SendingTimePolicyExt.Daytime
                            }
                        }
                    }
                },
                new NotificationReminderExt
                {
                    DelayDays = 7,
                    SendersReference = "ref-reminder-F2491E785C2D",
                    ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition"),

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
                    }
                }
            ]
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);

        // Verify NotificationOrderChainRequest properties
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-D3C9BA54", result.SendersReference);
        Assert.Equal(baseTime.ToUniversalTime(), result.RequestedSendTime);
        Assert.Equal("F1E2D3C4-B5A6-9876-5432-1098ABCDEF01", result.IdempotencyId);

        // Verify RecipientSms properties
        Assert.NotNull(result.Recipient);
        Assert.NotNull(result.Recipient.RecipientSms);
        Assert.Equal("+4799999999", result.Recipient.RecipientSms.PhoneNumber);

        // Verify SmsSendingOptions properties
        Assert.NotNull(result.Recipient.RecipientSms.Settings);
        Assert.Equal("Main SMS body", result.Recipient.RecipientSms.Settings.Body);
        Assert.Equal("Main SMS sender", result.Recipient.RecipientSms.Settings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientSms.Settings.SendingTimePolicy);

        // Verify Reminders collection properties
        Assert.NotNull(result.Reminders);
        Assert.Equal(2, result.Reminders.Count);

        // Verify NotificationReminder properties for first reminder
        var firstReminder = result.Reminders[0];
        Assert.Equal(3, firstReminder.DelayDays);
        Assert.NotEqual(Guid.Empty, firstReminder.OrderId);
        Assert.NotEqual(result.OrderId, firstReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, firstReminder.OrderId);
        Assert.Equal("ref-reminder-A3BCFE4284D6", firstReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[0].ConditionEndpoint, firstReminder.ConditionEndpoint);

        // Verify DateTime properties for first reminder
        var expectedFirstReminderDeliveryTime = baseTime.AddDays(3).ToUniversalTime();
        Assert.Equal(expectedFirstReminderDeliveryTime, firstReminder.RequestedSendTime);

        // Verify RecipientSms properties for first reminder
        Assert.NotNull(firstReminder.Recipient);
        Assert.NotNull(firstReminder.Recipient.RecipientSms);
        Assert.Equal("+4799999999", firstReminder.Recipient.RecipientSms.PhoneNumber);

        // Verify SmsSendingOptions properties for first reminder
        Assert.NotNull(firstReminder.Recipient.RecipientSms.Settings);
        Assert.Equal("Reminder 1 SMS body", firstReminder.Recipient.RecipientSms.Settings.Body);
        Assert.Equal("Reminder 1 SMS sender", firstReminder.Recipient.RecipientSms.Settings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, firstReminder.Recipient.RecipientSms.Settings.SendingTimePolicy);

        // Verify NotificationReminder properties for second reminder
        var secondReminder = result.Reminders[1];
        Assert.Equal(7, secondReminder.DelayDays);
        Assert.NotEqual(Guid.Empty, secondReminder.OrderId);
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, secondReminder.OrderId);
        Assert.Equal("ref-reminder-F2491E785C2D", secondReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[1].ConditionEndpoint, secondReminder.ConditionEndpoint);

        // Verify DateTime properties for second reminder
        var expectedSecondReminderDeliveryTime = baseTime.AddDays(7).ToUniversalTime();
        Assert.Equal(expectedSecondReminderDeliveryTime, secondReminder.RequestedSendTime);

        // Verify RecipientSms properties for second reminder
        Assert.NotNull(secondReminder.Recipient);
        Assert.NotNull(secondReminder.Recipient.RecipientSms);
        Assert.Equal("+4799999999", secondReminder.Recipient.RecipientSms.PhoneNumber);

        // Verify SmsSendingOptions properties for second reminder
        Assert.NotNull(secondReminder.Recipient.RecipientSms.Settings);
        Assert.Equal("Reminder 2 SMS body", secondReminder.Recipient.RecipientSms.Settings.Body);
        Assert.Equal("Reminder 2 SMS sender", secondReminder.Recipient.RecipientSms.Settings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, secondReminder.Recipient.RecipientSms.Settings.SendingTimePolicy);

        // Verify OrderId uniqueness
        Assert.NotEqual(firstReminder.OrderId, secondReminder.OrderId);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithSmsRecipientAndNoReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = requestTime,
            SendersReference = "ref-4617F6FFBE7D",
            IdempotencyId = "CCDDEE22-3456-7890-ABCD-EF0123456789",
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            Recipient = new NotificationRecipientExt
            {
                RecipientSms = new RecipientSmsExt
                {
                    PhoneNumber = "+4799999999",
                    Settings = new SmsSendingOptionsExt
                    {
                        Body = "SMS test body",
                        Sender = "SMS sender name",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime
                    }
                }
            }
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);

        // Verify NotificationOrderChainRequest properties
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-4617F6FFBE7D", result.SendersReference);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal(requestTime.ToUniversalTime(), result.RequestedSendTime);
        Assert.Equal("CCDDEE22-3456-7890-ABCD-EF0123456789", result.IdempotencyId);

        // Verify RecipientSms properties
        Assert.NotNull(result.Recipient);
        Assert.NotNull(result.Recipient.RecipientSms);
        Assert.Equal("+4799999999", result.Recipient.RecipientSms.PhoneNumber);

        // Verify SmsSendingOptions properties
        Assert.NotNull(result.Recipient.RecipientSms.Settings);
        Assert.Equal("SMS test body", result.Recipient.RecipientSms.Settings.Body);
        Assert.Equal("SMS sender name", result.Recipient.RecipientSms.Settings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientSms.Settings.SendingTimePolicy);

        // Verify other recipient types are null
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientPerson);
        Assert.Null(result.Recipient.RecipientOrganization);

        // Verify no reminders or DialogportenAssociation
        Assert.Null(result.Reminders);
        Assert.Null(result.DialogportenAssociation);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithoutRequestedSendTime_UsesCurrentUtcTime()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            // Not setting RequestedSendTime to test default behavior
            IdempotencyId = "BC47D9EA-3CD5-48A6-B5B7-CF5B95D53F9B",
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
            }
        };

        // Act
        var beforeMapping = DateTime.UtcNow;
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);
        var afterMapping = DateTime.UtcNow;

        // Assert
        Assert.NotNull(result);

        // Verify RequestedSendTime is set to a value between our before and after timestamps
        Assert.True(result.RequestedSendTime.AddTicks(-(result.RequestedSendTime.Ticks % TimeSpan.TicksPerSecond)) <= afterMapping.AddTicks(-(afterMapping.Ticks % TimeSpan.TicksPerSecond)));
        Assert.True(result.RequestedSendTime.AddTicks(-(result.RequestedSendTime.Ticks % TimeSpan.TicksPerSecond)) >= beforeMapping.AddTicks(-(beforeMapping.Ticks % TimeSpan.TicksPerSecond)));

        // Verify other properties are correctly mapped
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("BC47D9EA-3CD5-48A6-B5B7-CF5B95D53F9B", result.IdempotencyId);

        // Verify Email recipient properties
        Assert.NotNull(result.Recipient.RecipientEmail);
        Assert.Equal("Test body", result.Recipient.RecipientEmail.Settings.Body);
        Assert.Equal("Test subject", result.Recipient.RecipientEmail.Settings.Subject);
        Assert.Equal("recipient@example.com", result.Recipient.RecipientEmail.EmailAddress);
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
            IdempotencyId = "2F3A4B5C-6D7E-8F9A-0B1C-2D3E4F5A6B7C",
            SendersReference = "CF537A1B-43E0-4917-9D61-83F28C8667C8",

            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    IgnoreReservation = true,
                    NationalIdentityNumber = "18874198354",
                    ResourceId = "urn:altinn:resource:7890",
                    ChannelSchema = NotificationChannelExt.Email,

                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Person email body",
                        Subject = "Person email subject",
                        ContentType = EmailContentTypeExt.Plain,
                        SenderEmailAddress = "person-sender@example.com",
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
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal("2F3A4B5C-6D7E-8F9A-0B1C-2D3E4F5A6B7C", result.IdempotencyId);
        Assert.Equal("CF537A1B-43E0-4917-9D61-83F28C8667C8", result.SendersReference);
        Assert.Equal(requestExt.RequestedSendTime.ToUniversalTime(), result.RequestedSendTime);

        // Person recipient validation
        Assert.NotNull(result.Recipient.RecipientPerson);
        Assert.Equal("18874198354", result.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:7890", result.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.Email, result.Recipient.RecipientPerson.ChannelSchema);
        Assert.True(result.Recipient.RecipientPerson.IgnoreReservation);

        // Email settings validation
        Assert.NotNull(result.Recipient.RecipientPerson.EmailSettings);
        Assert.Equal("Person email body", result.Recipient.RecipientPerson.EmailSettings.Body);
        Assert.Equal("Person email subject", result.Recipient.RecipientPerson.EmailSettings.Subject);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientPerson.EmailSettings.ContentType);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientPerson.EmailSettings.SendingTimePolicy);
        Assert.Equal("person-sender@example.com", result.Recipient.RecipientPerson.EmailSettings.SenderEmailAddress);

        // SMS settings should be null when using Email channel
        Assert.Null(result.Recipient.RecipientPerson.SmsSettings);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientOrganization);

        // Unused objects should be null
        Assert.Null(result.Reminders);
        Assert.Null(result.DialogportenAssociation);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithPersonRecipientSmsChannel_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = DateTime.UtcNow,
            SendersReference = "ref-9B8D303243B6",
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            IdempotencyId = "C1D2E3F4-G5H6-I7J8-K9L0-M1N2O3P4Q5R6",

            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    NationalIdentityNumber = "18874198354",
                    ResourceId = "urn:altinn:resource:4321",
                    ChannelSchema = NotificationChannelExt.Sms,
                    IgnoreReservation = true,
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "Person SMS body",
                        Sender = "Person SMS sender",
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
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-9B8D303243B6", result.SendersReference);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal("C1D2E3F4-G5H6-I7J8-K9L0-M1N2O3P4Q5R6", result.IdempotencyId);
        Assert.Equal(requestExt.RequestedSendTime.ToUniversalTime(), result.RequestedSendTime);

        // Person recipient validation
        Assert.NotNull(result.Recipient.RecipientPerson);
        Assert.True(result.Recipient.RecipientPerson.IgnoreReservation);
        Assert.Equal("18874198354", result.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:4321", result.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.Sms, result.Recipient.RecipientPerson.ChannelSchema);

        // SMS settings validation
        Assert.NotNull(result.Recipient.RecipientPerson.SmsSettings);
        Assert.Equal("Person SMS body", result.Recipient.RecipientPerson.SmsSettings.Body);
        Assert.Equal("Person SMS sender", result.Recipient.RecipientPerson.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientPerson.SmsSettings.SendingTimePolicy);

        // Email settings should be null when using SMS channel
        Assert.Null(result.Recipient.RecipientPerson.EmailSettings);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientOrganization);

        // Unused objects should be null
        Assert.Null(result.Reminders);
        Assert.Null(result.DialogportenAssociation);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithPersonRecipientEmailPreferredAndNoReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var requestTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = requestTime,
            SendersReference = "ref-4617F6FFBE7D",
            IdempotencyId = "F1E2F3G4-H5I6-J7K8-L9M0-N1O2P3Q4R5S6",
            ConditionEndpoint = new Uri("https://vg.no/condition"),

            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    NationalIdentityNumber = "18874198354",
                    ResourceId = "urn:altinn:resource:4321",
                    IgnoreReservation = true,
                    ChannelSchema = NotificationChannelExt.EmailPreferred,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Person email body",
                        Subject = "Person email subject",
                        SenderEmailAddress = "person-sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    },
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "Person SMS body",
                        Sender = "Person SMS sender",
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
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.Equal(creatorName, result.Creator.ShortName);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-4617F6FFBE7D", result.SendersReference);
        Assert.Equal(requestExt.ConditionEndpoint, result.ConditionEndpoint);
        Assert.Equal(requestTime.ToUniversalTime(), result.RequestedSendTime);
        Assert.Equal("F1E2F3G4-H5I6-J7K8-L9M0-N1O2P3Q4R5S6", result.IdempotencyId);

        // Person recipient validation
        Assert.NotNull(result.Recipient.RecipientPerson);
        Assert.True(result.Recipient.RecipientPerson.IgnoreReservation);
        Assert.Equal("18874198354", result.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:4321", result.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.EmailPreferred, result.Recipient.RecipientPerson.ChannelSchema);

        // Email settings validation
        Assert.NotNull(result.Recipient.RecipientPerson.EmailSettings);
        Assert.Equal("Person email body", result.Recipient.RecipientPerson.EmailSettings.Body);
        Assert.Equal("Person email subject", result.Recipient.RecipientPerson.EmailSettings.Subject);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientPerson.EmailSettings.ContentType);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientPerson.EmailSettings.SendingTimePolicy);
        Assert.Equal("person-sender@example.com", result.Recipient.RecipientPerson.EmailSettings.SenderEmailAddress);

        // SMS settings validation
        Assert.NotNull(result.Recipient.RecipientPerson.SmsSettings);
        Assert.Equal("Person SMS body", result.Recipient.RecipientPerson.SmsSettings.Body);
        Assert.Equal("Person SMS sender", result.Recipient.RecipientPerson.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientPerson.SmsSettings.SendingTimePolicy);

        // All other recipients should be null
        Assert.Null(result.Recipient.RecipientSms);
        Assert.Null(result.Recipient.RecipientEmail);
        Assert.Null(result.Recipient.RecipientOrganization);

        // Verify no reminders or DialogportenAssociation
        Assert.Null(result.Reminders);
        Assert.Null(result.DialogportenAssociation);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithPersonRecipientEmailPreferredAndReminders_MapsCorrectly()
    {
        // Arrange
        var creatorName = "ttd";
        var baseTime = DateTime.UtcNow;
        var requestExt = new NotificationOrderChainRequestExt
        {
            RequestedSendTime = baseTime,
            SendersReference = "ref-F1E2D3C4B5A6",
            IdempotencyId = "H1I2J3K4-L5M6-N7O8-P9Q0-R1S2T3U4V5W6",

            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    IgnoreReservation = true,
                    NationalIdentityNumber = "18874198354",
                    ResourceId = "urn:altinn:resource:4321",
                    ChannelSchema = NotificationChannelExt.EmailPreferred,

                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Person email body",
                        Subject = "Person email subject",
                        SenderEmailAddress = "person-sender@example.com",
                        ContentType = EmailContentTypeExt.Plain,
                        SendingTimePolicy = SendingTimePolicyExt.Anytime
                    },
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "Person SMS body",
                        Sender = "Person SMS sender",
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
                        RecipientPerson = new RecipientPersonExt
                        {
                            IgnoreReservation = true,
                            NationalIdentityNumber = "18874198354",
                            ResourceId = "urn:altinn:resource:4321",
                            ChannelSchema = NotificationChannelExt.EmailPreferred,

                            EmailSettings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 1 person email body",
                                Subject = "Reminder 1 person email subject",
                                SenderEmailAddress = "reminder-person-sender@example.com",
                                ContentType = EmailContentTypeExt.Html,
                                SendingTimePolicy = SendingTimePolicyExt.Anytime
                            },
                            SmsSettings = new SmsSendingOptionsExt
                            {
                                Body = "Reminder 1 person SMS body",
                                Sender = "Reminder 1 person SMS sender",
                                SendingTimePolicy = SendingTimePolicyExt.Daytime
                            }
                        }
                    },
                    SendersReference = "ref-A1B2C3D4E5F6",
                    ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition")
                },
                new NotificationReminderExt
                {
                    DelayDays = 7,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientPerson = new RecipientPersonExt
                        {
                            IgnoreReservation = false,
                            NationalIdentityNumber = "18874198354",
                            ResourceId = "urn:altinn:resource:4321",
                            ChannelSchema = NotificationChannelExt.Email,

                            EmailSettings = new EmailSendingOptionsExt
                            {
                                Body = "Reminder 2 person email body",
                                Subject = "Reminder 2 person email subject",
                                SenderEmailAddress = "reminder2-person-sender@example.com",
                                ContentType = EmailContentTypeExt.Plain,
                                SendingTimePolicy = SendingTimePolicyExt.Anytime
                            }
                        }
                    },
                    SendersReference = "ref-G1H2I3J4K5L6",
                    ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition")
                }
            ]
        };

        // Act
        var result = requestExt.MapToNotificationOrderChainRequest(creatorName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);
        Assert.NotEqual(result.OrderId, result.OrderChainId);
        Assert.Equal("ref-F1E2D3C4B5A6", result.SendersReference);
        Assert.Equal(baseTime.ToUniversalTime(), result.RequestedSendTime);

        // Person recipient validation
        Assert.NotNull(result.Recipient.RecipientPerson);
        Assert.True(result.Recipient.RecipientPerson.IgnoreReservation);
        Assert.Equal("18874198354", result.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:4321", result.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.EmailPreferred, result.Recipient.RecipientPerson.ChannelSchema);

        // Email settings validation for main notification
        Assert.NotNull(result.Recipient.RecipientPerson.EmailSettings);
        Assert.Equal("Person email body", result.Recipient.RecipientPerson.EmailSettings.Body);
        Assert.Equal("Person email subject", result.Recipient.RecipientPerson.EmailSettings.Subject);
        Assert.Equal(EmailContentType.Plain, result.Recipient.RecipientPerson.EmailSettings.ContentType);
        Assert.Equal(SendingTimePolicy.Anytime, result.Recipient.RecipientPerson.EmailSettings.SendingTimePolicy);
        Assert.Equal("person-sender@example.com", result.Recipient.RecipientPerson.EmailSettings.SenderEmailAddress);

        // SMS settings validation for main notification
        Assert.NotNull(result.Recipient.RecipientPerson.SmsSettings);
        Assert.Equal("Person SMS body", result.Recipient.RecipientPerson.SmsSettings.Body);
        Assert.Equal("Person SMS sender", result.Recipient.RecipientPerson.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, result.Recipient.RecipientPerson.SmsSettings.SendingTimePolicy);

        // Reminders verification
        Assert.NotNull(result.Reminders);
        Assert.Equal(2, result.Reminders.Count);

        // First reminder verification
        var firstReminder = result.Reminders[0];
        Assert.Equal(3, firstReminder.DelayDays);
        Assert.Equal("ref-A1B2C3D4E5F6", firstReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[0].ConditionEndpoint, firstReminder.ConditionEndpoint);

        var expectedFirstReminderDeliveryTime = baseTime.AddDays(3).ToUniversalTime();
        Assert.Equal(expectedFirstReminderDeliveryTime, firstReminder.RequestedSendTime);

        Assert.NotNull(firstReminder.Recipient.RecipientPerson);
        Assert.True(firstReminder.Recipient.RecipientPerson.IgnoreReservation);
        Assert.Equal("18874198354", firstReminder.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:4321", firstReminder.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.EmailPreferred, firstReminder.Recipient.RecipientPerson.ChannelSchema);

        Assert.NotNull(firstReminder.Recipient.RecipientPerson.EmailSettings);
        Assert.Equal(EmailContentType.Html, firstReminder.Recipient.RecipientPerson.EmailSettings.ContentType);
        Assert.Equal("Reminder 1 person email body", firstReminder.Recipient.RecipientPerson.EmailSettings.Body);
        Assert.Equal("Reminder 1 person email subject", firstReminder.Recipient.RecipientPerson.EmailSettings.Subject);
        Assert.Equal(SendingTimePolicy.Anytime, firstReminder.Recipient.RecipientPerson.EmailSettings.SendingTimePolicy);
        Assert.Equal("reminder-person-sender@example.com", firstReminder.Recipient.RecipientPerson.EmailSettings.SenderEmailAddress);

        Assert.NotNull(firstReminder.Recipient.RecipientPerson.SmsSettings);
        Assert.Equal("Reminder 1 person SMS body", firstReminder.Recipient.RecipientPerson.SmsSettings.Body);
        Assert.Equal("Reminder 1 person SMS sender", firstReminder.Recipient.RecipientPerson.SmsSettings.Sender);
        Assert.Equal(SendingTimePolicy.Daytime, firstReminder.Recipient.RecipientPerson.SmsSettings.SendingTimePolicy);

        // Second reminder verification
        var secondReminder = result.Reminders[1];
        Assert.Equal(7, secondReminder.DelayDays);
        Assert.Equal("ref-G1H2I3J4K5L6", secondReminder.SendersReference);
        Assert.Equal(requestExt.Reminders[1].ConditionEndpoint, secondReminder.ConditionEndpoint);

        var expectedSecondReminderDeliveryTime = baseTime.AddDays(7).ToUniversalTime();
        Assert.Equal(expectedSecondReminderDeliveryTime, secondReminder.RequestedSendTime);

        Assert.NotNull(secondReminder.Recipient.RecipientPerson);
        Assert.False(secondReminder.Recipient.RecipientPerson.IgnoreReservation);
        Assert.Equal("18874198354", secondReminder.Recipient.RecipientPerson.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:4321", secondReminder.Recipient.RecipientPerson.ResourceId);
        Assert.Equal(NotificationChannel.Email, secondReminder.Recipient.RecipientPerson.ChannelSchema);

        Assert.NotNull(secondReminder.Recipient.RecipientPerson.EmailSettings);
        Assert.Equal(EmailContentType.Plain, secondReminder.Recipient.RecipientPerson.EmailSettings.ContentType);
        Assert.Equal("Reminder 2 person email body", secondReminder.Recipient.RecipientPerson.EmailSettings.Body);
        Assert.Equal("Reminder 2 person email subject", secondReminder.Recipient.RecipientPerson.EmailSettings.Subject);
        Assert.Equal(SendingTimePolicy.Anytime, secondReminder.Recipient.RecipientPerson.EmailSettings.SendingTimePolicy);
        Assert.Equal("reminder2-person-sender@example.com", secondReminder.Recipient.RecipientPerson.EmailSettings.SenderEmailAddress);

        Assert.Null(secondReminder.Recipient.RecipientPerson.SmsSettings);

        // Verify OrderId uniqueness
        Assert.NotEqual(result.OrderId, firstReminder.OrderId);
        Assert.NotEqual(result.OrderId, secondReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, firstReminder.OrderId);
        Assert.NotEqual(result.OrderChainId, secondReminder.OrderId);
        Assert.NotEqual(firstReminder.OrderId, secondReminder.OrderId);
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithNullCreator_ThrowsArgumentNullException()
    {
        // Arrange
        var notificationOrderChainRequestBuilder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => notificationOrderChainRequestBuilder.SetCreator(null!));
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithNullIdempotencyId_ThrowsArgumentNullException()
    {
        // Arrange
        var creatorName = "ttd";
        var requestExt = new NotificationOrderChainRequestExt
        {
            IdempotencyId = null!,
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
            }
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => requestExt.MapToNotificationOrderChainRequest(creatorName));
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithNullRecipient_ThrowsArgumentNullException()
    {
        // Arrange
        var notificationOrderChainRequestBuilder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => notificationOrderChainRequestBuilder.SetRecipient(null!));
    }

    [Fact]
    public void MapToNotificationOrderChainRequest_WithNullRequestedSendTime_SetsCurrentUtcTime()
    {
        // Arrange
        var builder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder();

        builder.SetRequestedSendTime(null);
        builder.SetOrderId(Guid.NewGuid());
        builder.SetCreator(new Creator("ttd"));
        builder.SetOrderChainId(Guid.NewGuid());
        builder.SetIdempotencyId("E4DD3079FCAC");

        // Act
        var beforeBuild = DateTime.UtcNow;
        var notificationOrderChainRequest = builder.Build();
        var afterBuild = DateTime.UtcNow;

        // Assert
        Assert.NotNull(notificationOrderChainRequest);

        var requestedSendTime = notificationOrderChainRequest.RequestedSendTime;
        Assert.True(requestedSendTime >= beforeBuild.AddSeconds(-1) && requestedSendTime <= afterBuild.AddSeconds(1));
    }

    [Fact]
    public void Build_WithEmptyOrderId_ThrowsArgumentException()
    {
        // Arrange
        var builder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(Guid.Empty)
            .SetCreator(new Creator("ttd"))
            .SetOrderChainId(Guid.NewGuid())
            .SetRecipient(new NotificationRecipient())
            .SetIdempotencyId("63404F51-2079-4598-BD23-8F4467590FB4");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithEmptyOrderChainId_ThrowsArgumentException()
    {
        // Arrange
        var builder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(Guid.NewGuid())
            .SetOrderChainId(Guid.Empty)
            .SetCreator(new Creator("ttd"))
            .SetRecipient(new NotificationRecipient())
            .SetIdempotencyId("63404F51-2079-4598-BD23-8F4467590FB4");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithoutOrderId_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetCreator(new Creator("ttd"))
            .SetOrderChainId(Guid.NewGuid())
            .SetRecipient(new NotificationRecipient())
            .SetIdempotencyId("63404F51-2079-4598-BD23-8F4467590FB4");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithEmptyOrderChainId_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(Guid.NewGuid())
            .SetCreator(new Creator("ttd"))
            .SetRecipient(new NotificationRecipient())
            .SetIdempotencyId("63404F51-2079-4598-BD23-8F4467590FB4");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithEmptyIdempotencyId_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(Guid.NewGuid())
            .SetCreator(new Creator("ttd"))
            .SetOrderChainId(Guid.NewGuid())
            .SetRecipient(new NotificationRecipient());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithNullCreator_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(Guid.NewGuid())
            .SetOrderChainId(Guid.NewGuid())
            .SetRecipient(new NotificationRecipient())
            .SetIdempotencyId("63404F51-2079-4598-BD23-8F4467590FB4");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithEmptyCreatorName_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(Guid.NewGuid())
            .SetOrderChainId(Guid.NewGuid())
            .SetCreator(new Creator(string.Empty))
            .SetRecipient(new NotificationRecipient())
            .SetIdempotencyId("63404F51-2079-4598-BD23-8F4467590FB4");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
}
