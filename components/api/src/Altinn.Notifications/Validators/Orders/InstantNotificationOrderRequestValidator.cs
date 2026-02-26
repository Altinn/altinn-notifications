using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Recipient;

using FluentValidation;

namespace Altinn.Notifications.Validators.Orders;

/// <summary>
/// Represents validation rules for a request to send a notification immediately to a single recipient.
/// </summary>
internal sealed class InstantNotificationOrderRequestValidator : AbstractValidator<InstantNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstantNotificationOrderRequestValidator"/> class.
    /// </summary>
    public InstantNotificationOrderRequestValidator()
    {
        RuleFor(request => request.InstantNotificationRecipient)
            .SetValidator(new InstantNotificationRecipientValidator());
    }
}
