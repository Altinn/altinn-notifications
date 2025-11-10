namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Repository for managing notification log entries in the database.
/// Provides functionality to insert notification log records for tracking 
/// notification delivery status and related metadata.
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>
    /// Asynchronously inserts a new notification log entry into the database.
    /// </summary>
    /// <param name="shipmentId">The unique identifier for the notification shipment.</param>
    /// <param name="notificationType">The type of notification (e.g., 'Email', 'SMS').</param>
    /// <param name="orderChainId">Optional. The order chain identifier.</param>
    /// <param name="dialogId">Optional. The dialog identifier associated with the notification.</param>
    /// <param name="transmissionId">Optional. The transmission identifier.</param>
    /// <param name="operationId">Optional. The operation identifier.</param>
    /// <param name="gatewayReference">Optional. Reference to the gateway used for sending.</param>
    /// <param name="recipient">Optional. The recipient identifier (organization number or social security number).</param>
    /// <param name="destination">Optional. The destination address (email address or phone number).</param>
    /// <param name="resource">Optional. The resource associated with the notification.</param>
    /// <param name="status">Optional. The current status of the notification.</param>
    /// <param name="sentTimestamp">Optional. The timestamp when the notification was sent.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, containing the auto-generated ID of the inserted log entry.</returns>
    Task<long> InsertAsync(
        Guid shipmentId,
        string notificationType,
        long? orderChainId = null,
        Guid? dialogId = null,
        string? transmissionId = null,
        string? operationId = null,
        string? gatewayReference = null,
        string? recipient = null,
        string? destination = null,
        string? resource = null,
        string? status = null,
        DateTime? sentTimestamp = null);
}
