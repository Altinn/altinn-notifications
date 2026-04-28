using Altinn.Notifications.Email.Core.Models;

namespace Altinn.Notifications.Email.Core.Dependencies;

/// <summary>
/// Dispatches email-service rate-limit events to the Notifications API.
/// </summary>
public interface IEmailServiceRateLimitDispatcher
{
    /// <summary>
    /// Dispatches a rate-limit service update to the configured transport.
    /// </summary>
    /// <param name="update">The rate-limit service update to dispatch.</param>
    Task DispatchAsync(GenericServiceUpdate update);
}
