using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Integrations.Wolverine.Handlers;

/// <summary>
/// Handles email service rate limit commands received from the Azure Service Bus queue.
/// Published by the email service when Azure Communication Services returns a rate-limit (HTTP 429) response.
/// </summary>
public static class EmailServiceRateLimitHandler
{
    /// <summary>
    /// Processes an email service rate limit command by delegating to <see cref="IAltinnServiceUpdateService"/>.
    /// </summary>
    public static async Task Handle(EmailServiceRateLimitCommand command, IAltinnServiceUpdateService serviceUpdateService, ILogger logger)
    {
        logger.LogInformation(
            "Received email service rate limit signal from source: {Source}.",
            command.Source);

        await serviceUpdateService.HandleServiceUpdate(
            command.Source.ToLower().Trim(),
            AltinnServiceUpdateSchema.ResourceLimitExceeded,
            command.Data);
    }
}
