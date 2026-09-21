namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Interface describing the required features of an implementation of a client for the Dialogporten service.
/// </summary>
public interface IDialogportenClient
{
    /// <summary>
    /// Performs a check to determine if the current user has access to a specific dialog identified by its ID.
    /// </summary>
    /// <param name="dialogId">The ID of the dialog to check access for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the user has access to the dialog.</returns>
    public Task<bool> CheckUserAccessToDialog(Guid dialogId, CancellationToken cancellationToken);
}
