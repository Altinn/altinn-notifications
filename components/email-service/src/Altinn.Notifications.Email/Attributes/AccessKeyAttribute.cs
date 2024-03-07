using Altinn.Notifications.Email.Configuration;

using Microsoft.AspNetCore.Mvc.Filters;

namespace Altinn.Notifications.Email.Attributes;

/// <summary>
/// Attribute for marking a controller or action that requires an access key for authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AccessKeyAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>
    /// Method that is called before the action is executed.
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        EmailDeliveryReportSettings emailDeliveryReportSettings = 
            context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()!.GetSection("EmailDeliveryReportSettings").Get<EmailDeliveryReportSettings>()!;
        string? accessKey = context.HttpContext.Request.Query["accesskey"];
        if (accessKey != emailDeliveryReportSettings.AccessKey)
        {
            context.HttpContext.Response.StatusCode = 401;
            await context.HttpContext.Response.WriteAsync("Unauthorized client");
            return;
        }

        await next();
    }
}
