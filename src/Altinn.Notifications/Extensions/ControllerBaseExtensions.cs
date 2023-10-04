using Altinn.Notifications.Core.Models;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Extensions;

/// <summary>
/// Extension methods for <see cref="ControllerBase"/>.
/// </summary>
public static class ControllerBaseExtensions
{
    /// <summary>
    /// Extension method for <see cref="ControllerBase"/> that can turn a <see cref="ServiceError"/> into an
    /// appropriate <see cref="ActionResult"/> response
    /// </summary>
    /// <param name="controller">The current controller</param>
    /// <param name="error">The identified <see cref="ServiceError"/></param>
    /// <returns>The correct type of ActionResult based on values in the ServiceError.</returns>
    public static ActionResult ServiceErrorResult(this ControllerBase controller, ServiceError error)
    {
        if (controller is null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        switch (error.ErrorCode)
        {
            case 400:
                error.AddToModelState(controller.ModelState);
                return controller.ValidationProblem(controller.ModelState);
        }

        return controller.Problem(error.ErrorMessage, statusCode: error.ErrorCode);
    }
}
