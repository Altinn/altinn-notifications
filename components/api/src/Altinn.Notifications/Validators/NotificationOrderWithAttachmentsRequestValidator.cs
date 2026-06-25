using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Validates <see cref="NotificationOrderWithAttachmentsRequestExt"/> requests submitted to the
/// email-with-attachments order endpoint.
/// </summary>
internal sealed class NotificationOrderWithAttachmentsRequestValidator : AbstractValidator<NotificationOrderWithAttachmentsRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderWithAttachmentsRequestValidator"/> class.
    /// </summary>
    public NotificationOrderWithAttachmentsRequestValidator()
    {
        Include(new NotificationOrderBaseValidator());

        RuleFor(order => order.IdempotencyId)
            .NotNull()
            .NotEmpty()
            .WithMessage("IdempotencyId cannot be null or empty.");

        // required
        RuleFor(order => order.Recipient)
            .NotNull()
            .SetValidator(validator: new RecipientEmailWithAttachmentsValidator());

        When(order => order.Recipient?.Settings?.Attachments is { Count: > 0 }, () =>
        {
            RuleForEach(order => order.Recipient.Settings.Attachments)
                .Must((_, attachment) =>
                    attachment == null ||
                    string.IsNullOrWhiteSpace(attachment.SasUrl) ||
                    EmailAttachmentRules.ParseSasExpiry(attachment.SasUrl) != null)
                .WithMessage((_, attachment) => $"Attachment '{attachment.Filename}': sasUrl is missing a valid 'se' (signed expiry) parameter.");

            RuleForEach(order => order.Recipient.Settings.Attachments)
                .Must((order, attachment) =>
                {
                    if (attachment == null || string.IsNullOrWhiteSpace(attachment.SasUrl))
                    {
                        return true;
                    }

                    var expiry = EmailAttachmentRules.ParseSasExpiry(attachment.SasUrl);
                    return expiry == null || expiry >= order.RequestedSendTime.AddMinutes(15);
                })
                .WithMessage((_, attachment) => $"Attachment '{attachment.Filename}': sasUrl must be valid for at least 15 minutes after requestedSendTime.");
        });

        // should not run if DialogportenAssociation is null
        When(order => order.DialogportenAssociation != null, () =>
        {
            RuleFor(order => order.DialogportenAssociation)
                .SetValidator(validator: new DialogportenIdentifiersValidator());
        });
    }
}
