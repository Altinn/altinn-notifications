using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Rules;
using FluentValidation;

namespace Altinn.Notifications.Validators.Dashboard;

/// <summary>
/// Validator for <see cref="NotificationsByOrgNumberRequestExt"/>.
/// </summary>
internal sealed class NotificationsByOrgNumberRequestValidator : AbstractValidator<NotificationsByOrgNumberRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsByOrgNumberRequestValidator"/> class.
    /// </summary>
    public NotificationsByOrgNumberRequestValidator()
    {
        RuleFor(x => x.OrganizationNumber)
            .NotEmpty().WithMessage("'OrganizationNumber' is required and cannot be empty")
            .MustBeValidOrganizationNumber();

        RuleFor(x => x.From)
            .Must(from => from!.Value.Kind != DateTimeKind.Unspecified).When(x => x.From.HasValue).WithMessage("The 'from' value must have specified a time zone.");

        RuleFor(x => x.From)
            .Must((request, from) => from < request.To).When(x => x.From.HasValue && x.To.HasValue).WithMessage("'from' must be earlier than 'to'.");

        RuleFor(x => x.From)
            .Must(from => from <= DateTime.UtcNow).When(x => x.From.HasValue).WithMessage("'from' must not be in the future.");

        RuleFor(x => x.From)
            .Must(from => from >= DateTime.UtcNow.AddYears(-10)).When(x => x.From.HasValue).WithMessage("'from' must not be earlier than 10 years ago.");

        RuleFor(x => x.To)
            .Must(to => to!.Value.Kind != DateTimeKind.Unspecified).When(x => x.To.HasValue).WithMessage("The 'to' value must have specified a time zone.");

        RuleFor(x => x.To)
            .Must(to => to <= DateTime.UtcNow).When(x => x.To.HasValue).WithMessage("'to' must not be in the future.");

        RuleFor(x => x.To)
            .Must(to => to > DateTime.UtcNow.AddDays(-7)).When(x => x.To.HasValue && !x.From.HasValue).WithMessage("'to' must be later than the default 'from' (7 days ago).");
    }
}
