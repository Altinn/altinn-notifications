using Altinn.Notifications.Core.Models.Status;
using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Validator for GetStatusFeedRequest objects using FluentValidation.
    /// Ensures that requests to fetch status feed entries contain valid parameters.
    /// </summary>
    internal sealed class GetStatusFeedRequestValidator : AbstractValidator<GetStatusFeedRequest>
    {
        /// <summary>
        /// Initializes a new instance of the GetStatusFeedRequestValidator class.
        /// Sets up validation rules for GetStatusFeedRequest properties.
        /// </summary>
        public GetStatusFeedRequestValidator()
        {
            RuleFor(x => x.Seq)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Sequence number cannot be less than 0.");
        }
    }
}
