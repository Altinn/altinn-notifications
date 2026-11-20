using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
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
    /// Retrieves the next pending email notification within the provided unit of work.
    /// </summary>
    /// <param name="unitOfWork">The unit of work that provides the active connection and transaction.</param>
    /// <param name="cancellationToken">A token used for cancelling the asynchronous operation.</param>
    /// <param name="sendingTimePolicy">
    /// Policy that determines which notifications are eligible for retrieval.
    /// <see cref="SendingTimePolicy.Daytime"/> retrieves notifications with the Daytime sending time policy;
    /// any other value retrieves notifications with the Anytime sending time policy.
    /// </param>
    /// <returns>
    /// A task that completes when retrieval finishes. The result is the next pending email notification,
    /// or <see langword="null"/> if none are available.
    /// </returns>
    public Task<Email?> GetNewNotificationAsync(UnitOfWork unitOfWork, CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Anytime);

    /// <summary>
    /// Retrieves the next pending composed email notification within the provided unit of work.
    /// </summary>
    /// <param name="unitOfWork">The unit of work that provides the active connection and transaction.</param>
    /// <param name="cancellationToken">A token used for cancelling the asynchronous operation.</param>
    /// <returns>
    /// A task that completes when retrieval finishes. The result is the next pending composed email notification,
    /// or <see langword="null"/> if none are available.
    /// </returns>
    public Task<ComposedEmail?> GetNewComposedNotificationAsync(UnitOfWork unitOfWork, CancellationToken cancellationToken);

    /// <summary>
    /// Claims an email notification in the retrying state that is due for a retry.
    /// </summary>
    /// <param name="unitOfWork">The unit of work to claim in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="sendingTimePolicy">The sending time policy of the notifications to claim.</param>
    /// <returns>The claimed email, or null if none is due.</returns>
    public Task<Email?> GetNewRetryNotificationAsync(UnitOfWork unitOfWork, CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Anytime);

    /// <summary>
    /// Claims a composed email notification in the retrying state that is due for a retry.
    /// </summary>
    /// <param name="unitOfWork">The unit of work to claim in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The claimed composed email, or null if none is due.</returns>
    public Task<ComposedEmail?> GetNewComposedRetryNotificationAsync(UnitOfWork unitOfWork, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the send condition endpoint of the order the notification belongs to.
    /// </summary>
    /// <param name="unitOfWork">The unit of work to query in.</param>
    /// <param name="notificationId">The notification alternate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The condition endpoint, or null if the order has none.</returns>
    public Task<Uri?> GetConditionEndpointAsync(UnitOfWork unitOfWork, Guid notificationId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the notification to the retrying state and increments its retry count.
    /// </summary>
    /// <param name="unitOfWork">The unit of work to update in.</param>
    /// <param name="notificationId">The notification alternate id.</param>
    /// <param name="retryReason">The reason for the retry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task SetRetryStatusAsync(UnitOfWork unitOfWork, Guid notificationId, string retryReason, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the send status of an email notification within <paramref name="unitOfWork"/> and optionally persists
    /// the operation identifier, delivery report, and total attachment size in bytes.
    /// At least one of <paramref name="notificationId"/> or <paramref name="operationId"/> must be provided.
    /// </summary>
    /// <param name="unitOfWork">The unit of work that provides the active connection and transaction.</param>
    /// <param name="notificationId">The alternate UUID of the notification. Takes precedence over <paramref name="operationId"/> when both are provided.</param>
    /// <param name="status">The new result status to set on the notification.</param>
    /// <param name="operationId">The operation identifier returned by the email provider. Used as a fallback lookup key when <paramref name="notificationId"/> is null.</param>
    /// <param name="deliveryReport">The raw delivery report payload from the email provider. Always overwritten when provided, regardless of expiry.</param>
    /// <param name="totalAttachmentSizeBytes">Total raw attachment size in bytes.</param>
    /// <param name="cancellationToken">A token used for cancelling the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidNotificationIdentifierException">Thrown when both <paramref name="notificationId"/> and <paramref name="operationId"/> are null or empty.</exception>
    /// <exception cref="NotificationNotFoundException">Thrown when no notification matches the provided identifier.</exception>
    /// <exception cref="NotificationExpiredException">Thrown when the notification has expired.</exception>
    /// <remarks>
    /// Completes the order and writes the status feed when a final status is set on the last pending notification.
    /// The caller is responsible for committing or rolling back the unit of work.
    /// </remarks>
    public Task UpdateSendStatusAsync(UnitOfWork unitOfWork, Guid? notificationId, EmailNotificationResultType status, string? operationId = null, string? deliveryReport = null, long? totalAttachmentSizeBytes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all processed email recipients for an order
    /// </summary>
    /// <returns>A list of email recipients</returns>
    public Task<List<EmailRecipient>> GetRecipients(Guid orderId);
}
