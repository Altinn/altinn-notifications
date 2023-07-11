using Altinn.Notifications.Models;

using FluentValidation;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Claass contining validation logic for the <see cref="EmailNotificationOrderRequest"/> model
/// </summary>
public class EmailNotificationOrderRequestValidator : AbstractValidator<EmailNotificationOrderRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrderRequestValidator"/> class.
    /// </summary>
    public EmailNotificationOrderRequestValidator()
    {
        RuleFor(order => order.Recipients)
            .Empty()
            .When(order => order.ToAddresses != null && order.ToAddresses.Any())
            .WithMessage("Provide either recipients or to addresses, not both.");

        RuleFor(order => order.Body).NotEmpty();
        RuleFor(order => order.Subject).NotEmpty();
        RuleFor(order => order.FromAddress).NotEmpty();
    }
}