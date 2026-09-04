using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Shared;
using Npgsql;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface describing all repository actions for notification orders
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Creates a new notification order in the database with processing status set to <see cref="OrderProcessingStatus.Registered"/>.
    /// </summary>
    /// <param name="order">The order to save</param>
    /// <returns>The saved notification order</returns>
    /// <remarks>
    /// This method persists the notification order with <see cref="OrderProcessingStatus.Registered"/>, 
    /// indicating it is ready for asynchronous processing by the notification pipeline.
    /// </remarks>
    public Task<NotificationOrder> Create(NotificationOrder order);

    /// <summary>
    /// Creates a new notification order chain in the database, consisting of a main notification and optional reminders.
    /// </summary>
    /// <param name="orderChain">The chain containing settings for the notification sequence.</param>
    /// <param name="mainOrder">The primary notification order that will be sent first.</param>
    /// <param name="reminders">A list of follow-up notification orders that will be sent after the main notification conditions.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// An <see cref="OrderChainCreateResult"/> containing the tracking data needed to build the response.
    /// When <see cref="OrderChainCreateResult.IsNewlyCreated"/> is <c>true</c>, the orders were persisted.
    /// When <c>false</c>, a duplicate idempotency key was detected and the existing chain's data is returned.
    /// </returns>
    /// <remarks>
    /// This method atomically inserts the order chain row and, if successful, persists the notification orders.
    /// When a concurrent request with the same idempotency key has already committed the chain,
    /// the insert is silently skipped and the existing chain's tracking data is returned.
    /// </remarks>
    public Task<OrderChainCreateResult> Create(NotificationOrderChainRequest orderChain, NotificationOrder mainOrder, List<NotificationOrder>? reminders, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new new high-priority instant notification order in the database.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The <see cref="InstantNotificationOrder"/> containing recipient, message, and delivery details.
    /// </param>
    /// <param name="notificationOrder">
    /// The <see cref="NotificationOrder"/> representing the standard notification order.
    /// </param>
    /// <param name="smsNotification">
    /// The <see cref="SmsNotification"/> instance containing SMS-specific delivery information.
    /// </param>
    /// <param name="smsExpiryDateTime">
    /// The <see cref="DateTime"/> indicating when the SMS notification expires and should no longer be delivered.
    /// </param>
    /// <param name="smsMessageCount">
    /// The number of SMS messages to be sent based on the message content.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="InstantNotificationOrderTracking"/> with tracking information.
    /// </returns>
    Task<InstantNotificationOrderTracking> Create(InstantNotificationOrder instantNotificationOrder, NotificationOrder notificationOrder, SmsNotification smsNotification, DateTime smsExpiryDateTime, int smsMessageCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new high-priority instant SMS notification order with flattened structure in the database.
    /// </summary>
    /// <param name="instantSmsNotificationOrder">
    /// The <see cref="InstantSmsNotificationOrder"/> containing SMS delivery details.
    /// </param>
    /// <param name="notificationOrder">
    /// The <see cref="NotificationOrder"/> representing the standard notification order.
    /// </param>
    /// <param name="smsNotification">
    /// The <see cref="SmsNotification"/> instance containing SMS-specific delivery information.
    /// </param>
    /// <param name="smsExpiryDateTime">
    /// The <see cref="DateTime"/> indicating when the SMS notification expires and should no longer be delivered.
    /// </param>
    /// <param name="smsMessageCount">
    /// The number of SMS messages to be sent based on the message content.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="InstantNotificationOrderTracking"/> with tracking information.
    /// </returns>
    Task<InstantNotificationOrderTracking> Create(InstantSmsNotificationOrder instantSmsNotificationOrder, NotificationOrder notificationOrder, SmsNotification smsNotification, DateTime smsExpiryDateTime, int smsMessageCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new high-priority instant email notification order in the database.
    /// </summary>
    /// <param name="instantEmailNotificationOrder">
    /// The <see cref="InstantEmailNotificationOrder"/> containing email delivery details.
    /// </param>
    /// <param name="notificationOrder">
    /// The <see cref="NotificationOrder"/> representing the standard notification order.
    /// </param>
    /// <param name="emailNotification">
    /// The <see cref="EmailNotification"/> instance containing email-specific delivery information.
    /// </param>
    /// <param name="emailExpiryDateTime">
    /// The <see cref="DateTime"/> when the email notification expires and should be considered failed if not delivered.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="InstantNotificationOrderTracking"/> with tracking information.
    /// </returns>
    Task<InstantNotificationOrderTracking> Create(InstantEmailNotificationOrder instantEmailNotificationOrder, NotificationOrder notificationOrder, EmailNotification emailNotification, DateTime emailExpiryDateTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves notification orders that are past their requested send time and atomically updates their processing status to <see cref="OrderProcessingStatus.Processing"/>.
    /// </summary>
    /// <param name="unitOfWork">
    /// The <see cref="UnitOfWork"/> used to execute the query and status update in the current database context.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> The first <see cref="NotificationOrder"/> object that was retrieved and marked for processing.
    /// Returns null if no orders are past due or available for processing.
    /// </returns>
    public Task<NotificationOrder?> GetNextPastDueOrder(UnitOfWork unitOfWork, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets processing status of an order
    /// </summary>
    public Task SetProcessingStatus(Guid orderId, OrderProcessingStatus status);

    /// <summary>
    /// Gets an order based on the provided id within the provided creator scope
    /// </summary>
    /// <param name="id">The order id</param>
    /// <param name="creator">The short name of the order creator</param>
    /// <returns>A notification order if it exists</returns>
    public Task<NotificationOrder?> GetOrderById(Guid id, string creator);

    /// <summary>
    /// Gets an order with process and notification status based on the provided id within the provided creator scope
    /// </summary>
    /// <param name="id">The order id</param>
    /// <param name="creator">The short name of the order creator</param>
    /// <returns>A notification order if it exists</returns>
    public Task<NotificationOrderWithStatus?> GetOrderWithStatusById(Guid id, string creator);

    /// <summary>
    /// Retrieves tracking information for a notification order chain using the creator's name and idempotency identifier.
    /// </summary>
    /// <param name="creatorName">
    /// The short name of the creator that originally submitted the notification order chain.
    /// </param>
    /// <param name="idempotencyId">
    /// The idempotency identifier that was defined when the order chain was created.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="NotificationOrderChainResponse"/> with 
    /// identifiers and sender references for both the notification order chain and its components, or 
    /// <c>null</c> if no matching notification order chain is found with the provided parameters.
    /// </returns>
    /// <remarks>
    /// The returned <see cref="NotificationOrderChainResponse"/> contains the order chain identifier that uniquely 
    /// identifies the entire notification sequence, along with the <see cref="NotificationOrderChainReceipt"/> 
    /// that includes shipment identifiers and sender references for both the main notification order and any associated reminders.
    /// </remarks>
    Task<NotificationOrderChainResponse?> GetOrderChainTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tracking information for a composed email order chain using the creator's name and idempotency identifier.
    /// </summary>
    /// <param name="creatorName">
    /// The short name of the creator that originally submitted the composed email order chain.
    /// </param>
    /// <param name="idempotencyId">
    /// The idempotency identifier that was defined when the order chain was created.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="NotificationOrderChainResponse"/> with 
    /// identifiers and sender reference for the composed email order chain, or 
    /// <c>null</c> if no matching order chain is found with the provided parameters.
    /// </returns>
    /// <remarks>
    /// Scoped exclusively to composed email orders (OrderType = 3). Composed orders do not support
    /// reminders, so the returned receipt always has <see cref="NotificationOrderChainReceipt.Reminders"/> set to <c>null</c>.
    /// </remarks>
    Task<NotificationOrderChainResponse?> GetComposedOrderChainTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an order based on the provided senders reference within the provided creator scope
    /// </summary>
    /// <param name="sendersReference">The senders reference</param>
    /// <param name="creator">The short name of the order creator</param>
    /// <returns>A list of notification orders</returns>
    public Task<List<NotificationOrder>> GetOrdersBySendersReference(string sendersReference, string creator);

    /// <summary>
    /// Cancels the order corresponding to the provided id within the provided creator scope if processing has not started yet
    /// </summary>
    /// <param name="id">The order id</param>
    /// <param name="creator">The short name of the order creator</param>
    /// <returns>If successful the cancelled notification order with status info. If error a cancellation error type.</returns>
    public Task<Result<NotificationOrderWithStatus, CancellationError>> CancelOrder(Guid id, string creator);

    /// <summary>
    /// Updates the status of a notification order to 'Completed' when all associated SMS and Email notifications have reached their respective terminal states.
    /// </summary>
    /// <param name="notificationId">
    /// The identifier of the notification (SMS or Email) that triggered the evaluation. If null, the operation is skipped.
    /// </param>
    /// <param name="source">
    /// The source type of the alternate identifier.
    /// </param>
    /// <returns>
    /// <c>true</c> if the order status was successfully updated to 'Completed';
    /// <c>false</c> if the order was already completed or if not all notifications have reached terminal states.
    /// </returns>
    /// <remarks>
    /// This method locates the order linked to the provided notification identifier and verifies whether all
    /// related notifications have reached terminal states. The status is only updated to 'Completed' if this condition is met.
    /// </remarks>
    public Task<bool> TryCompleteOrderBasedOnNotificationsState(Guid? notificationId, AlternateIdentifierSource source);

    /// <summary>
    /// Retrieves tracking information for an instant notification order using the creator's name and idempotency identifier.
    /// </summary>
    /// <param name="creatorName">
    /// The short name of the creator who originally submitted the instant notification order.
    /// </param>
    /// <param name="idempotencyId">
    /// The idempotency identifier specified when the instant notification order was created.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="InstantNotificationOrderTracking"/> with tracking information,
    /// or <c>null</c> if no matching order is found for the provided parameters.
    /// </returns>
    Task<InstantNotificationOrderTracking?> RetrieveInstantOrderTrackingInformation(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically inserts all notifications produced during order processing and transitions the order
    /// to either <see cref="OrderProcessingStatus.Completed"/> or <see cref="OrderProcessingStatus.Processed"/>
    /// within a single database transaction. If all notifications are immediately terminal (recipient not
    /// identified or reserved), the order is completed and a status feed entry is written in the same transaction.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the order was transitioned to <see cref="OrderProcessingStatus.Completed"/>;
    /// <c>false</c> if any notifications are still pending delivery.
    /// </returns>
    Task<bool> PersistProcessingResultAsync(
        UnitOfWork unitOfWork,
        NotificationOrder order,
        EmailOrderProcessingResult emailOrderProcessingResult,
        SmsOrderProcessingResult smsOrderProcessingResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically sets the order status to <see cref="OrderProcessingStatus.SendConditionNotMet"/> and
    /// inserts the corresponding status feed entry within a single database transaction.
    /// </summary>
    Task SetOrderSendConditionNotMetAsync(UnitOfWork unitOfWork, NotificationOrder order, CancellationToken cancellationToken = default);
}
