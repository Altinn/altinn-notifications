using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core;

/// <summary>
/// Describes the required public method of the status service
/// </summary>
public interface IStatusService
{
    /// <summary>
    /// Updates the send status of an email
    /// </summary>
    /// <param name="operationIdentifier">The operationIdentifier</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpdateSendStatus(SendNotificationOperationIdentifier operationIdentifier);

    /// <summary>
    /// Updates the send status of an email, received from Azure Communication Service
    /// </summary>
    /// <param name="sendOperationResult">OperationResult with status and operationId</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpdateSendStatus(SendOperationResult sendOperationResult);
}
