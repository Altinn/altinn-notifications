﻿using System;
using System.Collections.Generic;

using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators;

using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

/// <summary>
/// Tests for validating various scenarios of notification order chain requests
/// </summary>
public class NotificationOrderChainRequestValidatorTests
{
    private readonly NotificationOrderChainRequestValidator _validator = new();

    [Fact]
    public void Notification_With_Invalid_Phone_Number_Should_Fail_Validation()
    {
        // Arrange
        var request = new NotificationOrderChainRequestExt
        {
            IdempotencyId = "id",
            SendersReference = "te-123-123",
            RequestedSendTime = DateTime.UtcNow,
            ConditionEndpoint = new Uri("https://api.te.no/altinn/te-123-123/?seen=true"),
            DialogportenAssociation = new DialogportenIdentifiersExt
            {
                DialogId = "dialog-guid",
                TransmissionId = "transmissionId-utsending-1"
            },
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "ola.normann@example.com",
                    Settings = new EmailSendingOptionsExt
                    {
                        SendingTimePolicy = SendingTimePolicyExt.Anytime,
                        Subject = "Ny melding fra TE",
                        Body = "Du har fått en ny melding fra TE i Altinn meldingsboks. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                    }
                }
            },
            Reminders = new List<NotificationReminderExt>
            {
                new NotificationReminderExt
                {
                    ConditionEndpoint = new Uri("https://api.te.no/altinn/te-123-123/?seen=true"),
                    SendersReference = "te-123-123-rem-1",
                    DelayDays = 7,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientSms = new RecipientSmsExt
                        {
                            PhoneNumber = "55555555",
                            Settings = new SmsSendingOptionsExt
                            {
                                SendingTimePolicy = SendingTimePolicyExt.Daytime,
                                Sender = "1234 TE",
                                Body = "Du har en melding fra TE som krever handling. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                            }
                        }
                    }
                },
                new NotificationReminderExt
                {
                    ConditionEndpoint = new Uri("https://api.te.no/altinn/te-123-123/?seen=true"),
                    SendersReference = "te-123-123-rem-2",
                    DelayDays = 14,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientOrganization = new RecipientOrganizationExt
                        {
                            OrgNumber = "111222333",
                            ResourceId = "urn:altinn:resource:te_svc123",
                            ChannelSchema = NotificationChannelExt.SmsPreferred,
                            SmsSettings = new SmsSendingOptionsExt
                            {
                                Sender = "1234 TE",
                                SendingTimePolicy = SendingTimePolicyExt.Daytime,
                                Body = "Din bedrift $recipientName$ har en melding fra TE som krever handling. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                            },
                            EmailSettings = new EmailSendingOptionsExt
                            {
                                Subject = "Ny melding fra TE",
                                Body = "Ditt firma $recipientName$ har fått en ny melding fra TE i Altinn meldingsboks. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrors().WithErrorMessage("Recipient phone number is not a valid mobile number.");
    }

    [Fact]
    public void Functional_Validation_Should_Validate_Successfully()
    {
        // Arrange
        var order = new NotificationOrderChainRequestExt
        {
            IdempotencyId = "id",
            SendersReference = "te-123-123",
            DialogportenAssociation = new DialogportenIdentifiersExt
            {
                DialogId = "dialog-guid",
                TransmissionId = "transmissionId-utsending-1"
            },
            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    NationalIdentityNumber = "00000000000",
                    ResourceId = "urn:altinn:resource:te_svc123",
                    IgnoreReservation = false,
                    ChannelSchema = NotificationChannelExt.EmailPreferred,
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Sender = "1234 TE",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime,
                        Body = "Du har en ny melding fra TE. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                    },
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        SenderEmailAddress = "noreply-te@example.com",
                        Subject = "Ny melding fra TE",
                        Body = "Du har en ny melding fra TE. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                    }
                }
            }
        };

        // Act
        var result = _validator.TestValidate(order);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Notification_With_Reminder_Should_Validate_Successfully()
    {
        // Arrange
        var order = new NotificationOrderChainRequestExt
        {
            IdempotencyId = "id",
            SendersReference = "te-123-123",
            DialogportenAssociation = new DialogportenIdentifiersExt
            {
                DialogId = "123",
                TransmissionId = "456"
            },
            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    NationalIdentityNumber = "11122233300",
                    ResourceId = "urn:altinn:resource:te_svc123",
                    IgnoreReservation = false,
                    ChannelSchema = NotificationChannelExt.EmailPreferred,
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        SendingTimePolicy = SendingTimePolicyExt.Daytime,
                        Sender = "1234 TE",
                        Body = "Du har en ny melding fra TE. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                    },
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Subject = "Ny melding fra TE",
                        Body = "Du har en ny melding fra TE. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
                {
                    ConditionEndpoint = new Uri("https://api.te.no/altinn/te-123-123/?seen=true"),
                    SendersReference = "te-123-123",
                    DelayDays = 7,
                    Recipient = new NotificationRecipientExt
                    {
                        RecipientPerson = new RecipientPersonExt
                        {
                            NationalIdentityNumber = "11122233300",
                            ResourceId = "urn:altinn:resource:te_svc123",
                            IgnoreReservation = false,
                            ChannelSchema = NotificationChannelExt.EmailPreferred,
                            SmsSettings = new SmsSendingOptionsExt
                            {
                                SendingTimePolicy = SendingTimePolicyExt.Daytime,
                                Sender = "1234 TE",
                                Body = "Du har en melding fra TE som krever handling. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                            },
                            EmailSettings = new EmailSendingOptionsExt
                            {
                                SenderEmailAddress = "noreply-te@example.com",
                                SendingTimePolicy = SendingTimePolicyExt.Anytime,
                                Subject = "Påminnelse: Melding fra TE",
                                Body = "Du har en melding fra TE som krever handling"
                            }
                        }
                    }
                }
            ]
        };

        // Act
        var result = _validator.TestValidate(order);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Notification_With_IgnoreReservation_Should_Validate_Successfully()
    {
        // Arrange
        var order = new NotificationOrderChainRequestExt
        {
            IdempotencyId = "id",
            SendersReference = "te-123-123",
            DialogportenAssociation = new DialogportenIdentifiersExt
            {
                DialogId = "dialog-guid",
                TransmissionId = "transmissionId-utsending-1"
            },
            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    NationalIdentityNumber = "11122233300",
                    ResourceId = "urn:altinn:resource:te_svc123",
                    IgnoreReservation = false,
                    ChannelSchema = NotificationChannelExt.EmailPreferred,
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Sender = "1234 TE",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime,
                        Body = "Du har en ny melding fra TE. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                    },
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Subject = "Ny melding fra TE",
                        Body = "Du har en ny melding fra TE. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                    }
                }
            },
            Reminders =
            [
                new NotificationReminderExt
            {
                ConditionEndpoint = new Uri("https://api.te.no/altinn/te-123-123/?seen=true"),
                SendersReference = "te-123-123",
                DelayDays = 7,
                Recipient = new NotificationRecipientExt
                {
                    RecipientPerson = new RecipientPersonExt
                    {
                        NationalIdentityNumber = "11122233300",
                        ResourceId = "urn:altinn:resource:te_svc123",
                        IgnoreReservation = true,
                        ChannelSchema = NotificationChannelExt.SmsPreferred,
                        SmsSettings = new SmsSendingOptionsExt
                        {
                            SendingTimePolicy = SendingTimePolicyExt.Daytime,
                            Body = "Du har en melding fra TE som krever handling. Logg inn i Altinn for å gjøre deg kjent med innholdet.",
                            Sender = "1234 TE"
                        },
                        EmailSettings = new EmailSendingOptionsExt
                        {
                            SendingTimePolicy = SendingTimePolicyExt.Anytime,
                            Subject = "Påminnelse: Melding fra TE",
                            Body = "Du har en melding fra TE som krever handling. Logg inn i Altinn for å gjøre deg kjent med innholdet."
                        }
                    }
                }
            }
            ]
        };

        // Act
        var result = _validator.TestValidate(order);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RequestedSendTimeValidation_Should_Include_Validation_From_BaseValidator()
    {
        // Arrange
        var order = new NotificationOrderChainRequestExt
        {
            IdempotencyId = "id",
            SendersReference = "te-123-123",
            RequestedSendTime = DateTime.UtcNow.AddDays(-1),
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "noreply@altinn.no",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "test",
                        SendingTimePolicy = SendingTimePolicyExt.Anytime,
                        Subject = "test"
                    }
                }
            }
        };

        // Act
        var result = _validator.TestValidate(order);

        // Assert
        result.ShouldHaveValidationErrors().WithErrorMessage("RequestedSendTime must be greater than or equal to now.");
    }

    [Fact]
    public void Notification_With_EmailAndSmsChannel_WithMissingEmailSettings_ShouldFailValidation()
    {
        // Arrange
        var order = new NotificationOrderChainRequestExt
        {
            IdempotencyId = "id-0841A839C83D",
            SendersReference = "ref-2B4759F7CADF",
            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    NationalIdentityNumber = "25280522832",
                    ChannelSchema = NotificationChannelExt.EmailAndSms,
                    ResourceId = "urn:altinn:resource:skd_app-0CFDFD86EFC8",
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Sender = "Skatteetaten",
                        SendingTimePolicy = SendingTimePolicyExt.Daytime,
                        Body = "Du har en ny melding fra Skatteetaten. Logg inn i Altinn."
                    }
                }
            }
        };

        // Act
        var result = _validator.TestValidate(order);

        // Assert
        result.ShouldHaveValidationErrors().WithErrorMessage("EmailSettings must be set when ChannelSchema is EmailAndSms");
    }

    [Fact]
    public void Notification_With_EmailAndSmsChannel_WithMissingSmsSettings_ShouldFailValidation()
    {
        // Arrange
        var order = new NotificationOrderChainRequestExt
        {
            IdempotencyId = "id-0841A839C83D",
            SendersReference = "ref-2B4759F7CADF",
            Recipient = new NotificationRecipientExt
            {
                RecipientPerson = new RecipientPersonExt
                {
                    NationalIdentityNumber = "25280522832",
                    ChannelSchema = NotificationChannelExt.EmailAndSms,
                    ResourceId = "urn:altinn:resource:skd_app-0CFDFD86EFC8",
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        SenderEmailAddress = "Skatteetaten",
                        ContentType = EmailContentTypeExt.Plain,
                        Subject = "Ny melding fra Skatteetaten",
                        SendingTimePolicy = SendingTimePolicyExt.Anytime,
                        Body = "Du har en ny melding fra Skatteetaten. Logg inn i Altinn."
                    }
                }
            }
        };

        // Act
        var result = _validator.TestValidate(order);

        // Assert
        result.ShouldHaveValidationErrors().WithErrorMessage("SmsSettings must be set when ChannelSchema is EmailAndSms");
    }
}
