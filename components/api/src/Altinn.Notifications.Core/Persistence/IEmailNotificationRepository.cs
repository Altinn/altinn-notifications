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
    public Task<List<ComposedEmail>> GetNewComposedNotificationsAsync(int publishBatchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the send status of an email notification and optionally persists
    /// the operation identifier, delivery report, and total attachment size in bytes.
    /// At least one of <paramref name="notificationId"/> or <paramref name="operationId"/> must be provided.
    /// </summary>
    /// <param name="notificationId">The alternate UUID of the notification. Takes precedence over <paramref name="operationId"/> when both are provided.</param>
    /// <param name="status">The new result status to set on the notification.</param>
    /// <param name="operationId">The operation identifier returned by the email provider. Used as a fallback lookup key when <paramref name="notificationId"/> is null.</param>
    /// <param name="deliveryReport">The raw delivery report payload from the email provider. Always overwritten when provided, regardless of expiry.</param>
    /// <param name="totalAttachmentSizeBytes">Total raw attachment size in bytes.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="Exceptions.InvalidNotificationIdentifierException">
    /// Thrown when both <paramref name="notificationId"/> and <paramref name="operationId"/> are null or empty.
    /// </exception>
    public Task UpdateSendStatus(Guid? notificationId, EmailNotificationResultType status, string? operationId = null, string? deliveryReport = null, long? totalAttachmentSizeBytes = null);

    /// <summary>
    /// Retrieves all processed email recipients for an order
    /// </summary>
    /// <returns>A list of email recipients</returns>
    public Task<List<EmailRecipient>> GetRecipients(Guid orderId);
}
