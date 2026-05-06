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
    /// <param name="sendingTimePolicy">
    /// The sending time policy to claim. <see cref="SendingTimePolicy.Anytime"/> claims orders with policy = Anytime
    /// or NULL (legacy). <see cref="SendingTimePolicy.Daytime"/> claims only orders with policy = Daytime.
    /// </param>
    /// <returns>
    /// A task that completes when the retrieval for a single batch finishes or when cancellation is requested.
    /// The result contains up to <paramref name="publishBatchSize"/> pending email notifications.
    /// May return an empty list if none are available.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if cancellation is requested before or during retrieval.</exception>
    public Task<List<Email>> GetNewNotificationsAsync(int publishBatchSize, CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Anytime);

    /// <summary>
    /// Sets result status of an email notification, updates the operation id, and persists the raw delivery report.
    /// </summary>
    public Task UpdateSendStatus(Guid? notificationId, EmailNotificationResultType status, string? operationId = null, string? deliveryReport = null);

    /// <summary>
    /// Retrieves all processed email recipients for an order
    /// </summary>
    /// <returns>A list of email recipients</returns>
    public Task<List<EmailRecipient>> GetRecipients(Guid orderId);
}
