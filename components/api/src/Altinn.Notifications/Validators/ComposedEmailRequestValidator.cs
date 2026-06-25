using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Recipient;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Validates <see cref="ComposedEmailRequestExt"/> requests submitted to the composed email order endpoint.
/// </summary>
internal sealed class ComposedEmailRequestValidator : AbstractValidator<ComposedEmailRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComposedEmailRequestValidator"/> class.
    /// </summary>
    public ComposedEmailRequestValidator()
    {
        Include(new NotificationOrderBaseValidator());

        RuleFor(order => order.IdempotencyId)
            .NotNull()
            .NotEmpty()
            .WithMessage("IdempotencyId cannot be null or empty.");

        RuleFor(order => order.Recipient)
            .NotNull()
            .SetValidator(validator: new RecipientComposedEmailValidator());

        When(order => order.Recipient?.Settings?.Attachments is { Count: > 0 }, () =>
        {
            RuleForEach(order => order.Recipient.Settings.Attachments)
                .Must((_, attachment) =>
                    attachment == null ||
                    string.IsNullOrWhiteSpace(attachment.SasUrl) ||
                    SasFileReferenceRules.ParseSasExpiry(attachment.SasUrl) != null)
                .WithMessage((_, attachment) => $"Attachment '{attachment.Filename}': sasUrl is missing a valid 'se' (signed expiry) parameter.");

            RuleForEach(order => order.Recipient.Settings.Attachments)
                .Must((order, attachment) =>
                {
                    if (attachment == null || string.IsNullOrWhiteSpace(attachment.SasUrl))
                    {
                        return true;
                    }

                    var expiry = SasFileReferenceRules.ParseSasExpiry(attachment.SasUrl);
                    return expiry == null || expiry >= order.RequestedSendTime.AddMinutes(15);
                })
                .WithMessage((_, attachment) => $"Attachment '{attachment.Filename}': sasUrl must be valid for at least 15 minutes after requestedSendTime.");
        });

        When(order => order.DialogportenAssociation != null, () =>
        {
            RuleFor(order => order.DialogportenAssociation)
                .SetValidator(validator: new DialogportenIdentifiersValidator());
        });
    }
}
