using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Provides validation methods for status feed requests using ValidationErrorBuilder.
/// Throws validation exceptions when validation fails.
/// </summary>
public static class StatusFeedValidationHelper
{
    /// <summary>
    /// Validates a <see cref="GetStatusFeedRequestExt"/> request and throws if validation fails.
    /// </summary>
    /// <param name="request">The status feed request to validate.</param>
    /// <exception cref="ProblemInstanceException">Thrown when validation fails.</exception>
    public static void ValidateStatusFeedRequest(GetStatusFeedRequestExt request)
    {
        var errors = default(ValidationErrorBuilder);

        if (request.Seq < 0)
        {
            errors.Add(ValidationErrors.SequenceNumber_Invalid, "Seq");
        }

        if (errors.TryBuild(out var problemInstance))
        {
            throw new ProblemInstanceException(problemInstance);
        }
    }
}
