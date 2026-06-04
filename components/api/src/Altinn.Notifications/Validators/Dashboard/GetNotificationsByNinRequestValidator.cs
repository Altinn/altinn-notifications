using Altinn.Notifications.Models.Dashboard;

using FluentValidation;

namespace Altinn.Notifications.Validators.Dashboard;

/// <summary>
/// Validator for <see cref="GetNotificationsByNinRequestExt"/>.
/// </summary>
internal sealed class GetNotificationsByNinRequestValidator : AbstractValidator<GetNotificationsByNinRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetNotificationsByNinRequestValidator"/> class.
    /// </summary>
    public GetNotificationsByNinRequestValidator()
    {
        RuleFor(x => x.Nin)
            .NotEmpty()
            .WithMessage("'nin' is required and cannot be empty");

        RuleFor(x => x.From)
            .Must((request, from) => from < request.To)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'from' must be earlier than 'to'.");

        RuleFor(x => x.To)
            .Must(to => to > DateTimeOffset.UtcNow.AddDays(-7))
            .When(x => x.To.HasValue && !x.From.HasValue)
            .WithMessage("'to' must be later than the default 'from' (7 days ago).");
    }
}
