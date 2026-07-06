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
    /// <returns>
    /// A task that completes when the retrieval for a single batch finishes or when cancellation is requested.
    /// The result contains up to <paramref name="publishBatchSize"/> pending email notifications.
    /// May return an empty list if none are available.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if cancellation is requested before or during retrieval.</exception>
    public Task<List<Email>> GetNewNotificationsAsync(int publishBatchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves pending composed email notifications.
    /// </summary>
    /// <param name="publishBatchSize">Maximum number of notifications to retrieve in one batch.</param>
    /// <param name="cancellationToken">A token used for cancelling the asynchronous operation.</param>
    /// <returns>
    /// A task that completes when retrieval finishes. The result contains up to
    /// <paramref name="publishBatchSize"/> composed email notifications. May return an empty list.
    /// </returns>
    public Task<List<ComposedEmail>> GetNewComposedEmailNotificationsAsync(int publishBatchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Sets result status of an email notification, updates the operation id, and persists the raw delivery report.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the email notification (optional if <paramref name="operationId"/> is provided).</param>
    /// <param name="status">The result status of the email notification.</param>
    /// <param name="operationId">The operation identifier from the email provider (optional if <paramref name="notificationId"/> is provided).</param>
    /// <param name="deliveryReport">The raw delivery report payload received from the email provider (optional).</param>
    /// <param name="encodedAttachmentsSize">Total base64-encoded attachment size in bytes; 0 for standard emails.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="Exceptions.InvalidNotificationIdentifierException">
    /// Thrown when both <paramref name="notificationId"/> and <paramref name="operationId"/> are null or empty.
    /// </exception>
    public Task UpdateSendStatus(Guid? notificationId, EmailNotificationResultType status, string? operationId = null, string? deliveryReport = null, long? encodedAttachmentsSize = null);

    /// <summary>
    /// Retrieves all processed email recipients for an order
    /// </summary>
    /// <returns>A list of email recipients</returns>
    public Task<List<EmailRecipient>> GetRecipients(Guid orderId);
}
