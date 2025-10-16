using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface describing all repository operations related to an email notification
/// </summary>
public interface IEmailNotificationRepository : INotificationRepository
{
    /// <summary>
    /// Adds a new email notification to the database
    /// </summary>
    public Task AddNotification(EmailNotification notification, DateTime expiry);

    /// <summary>
    /// Retrieves pending email notifications.
    /// </summary>
    /// <param name="publishBatchSize">Maximum number of email notifications to retrieve in a single batch.</param>
    /// <param name="cancellationToken">A token used for cancelling the asynchronous operation.</param>
    /// <returns>A task that completes when retrieval finishes (no more eligible items) or when cancellation is requested.
    /// The task result contains a list of email notifications to be processed, limited by the specified batch size.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if cancellation is requested before or during retrieval.
    /// </exception>
    public Task<List<Email>> GetNewNotificationsAsync(int publishBatchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Sets result status of an email notification and update operation id
    /// </summary>
    public Task UpdateSendStatus(Guid? notificationId, EmailNotificationResultType status, string? operationId = null);

    /// <summary>
    /// Retrieves all processed email recipients for an order
    /// </summary>
    /// <returns>A list of email recipients</returns>
    public Task<List<EmailRecipient>> GetRecipients(Guid orderId);
}
