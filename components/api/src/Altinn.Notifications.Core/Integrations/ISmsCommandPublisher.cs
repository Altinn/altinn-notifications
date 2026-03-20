using Altinn.Notifications.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines the contract for publishing sms notifications from the API to the Sms Service via Azure Service Bus using Wolverine.
/// </summary>
public interface ISmsCommandPublisher
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
}
