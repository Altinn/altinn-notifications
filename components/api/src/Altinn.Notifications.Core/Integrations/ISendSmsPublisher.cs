using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines the contract for publishing sms notifications from the API to the Sms Service via Azure Service Bus using Wolverine.
/// </summary>
public interface ISendSmsPublisher
{
    /// <summary>
    /// Publishes an asynchronous operation and returns a unique identifier for the published item.
    /// </summary>
    /// <remarks>This method allows for cancellation of the publish operation. If the operation is canceled,
    /// the task will complete with a cancellation exception.</remarks>
    /// <param name="sms">The object containing the body of the message</param>
    /// <param name="cancellationToken">The cancellation token used to propagate notification that the operation should be canceled.</param>
    /// <returns>A task that represents the asynchronous operation, containing a GUID that uniquely identifies the published
    /// item.</returns>
    Task<Guid?> PublishAsync(Sms sms, CancellationToken cancellationToken);

    /// <summary>
    /// Gets or sets a value indicating whether this implementation of the Sms publlisher is real or a test dummy. Feature is enabled when the imeplementation is real.
    /// </summary>
    /// <remarks>This property controls the activation state of the feature. When set to <see
    /// langword="true"/>, the feature is enabled; otherwise, it is disabled.</remarks>
    bool IsEnabled { get; }
}
