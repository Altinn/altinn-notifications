using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Represents a command publisher that does not send SMS messages.
/// </summary>
/// <remarks>This implementation of ISmsCommandPublisher is disabled and will always throw a
/// NotImplementedException when attempting to publish an SMS command. Use this class when SMS publishing functionality
/// should be explicitly disabled, such as in testing or development environments.</remarks>
[ExcludeFromCodeCoverage]
public class DisabledSmsCommandPublisher : ISmsCommandPublisher
{
    /// <inheritdoc/>
    public bool IsEnabled => false;

    /// <summary>
    /// This method is intentionally not implemented to indicate that SMS command publishing is disabled. Attempting to call this method will result in a NotImplementedException being thrown.
    /// </summary>
    /// <param name="sms">The sms contract to be sent</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException">Will always throw NotImplementedException</exception>
    public Task<Guid?> PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
