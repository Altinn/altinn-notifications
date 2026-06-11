using Altinn.Notifications.Core.Persistence;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Extends <see cref="INotificationLogRepository"/> with a transactional overload
/// for use within existing database transactions in the Persistence layer.
/// </summary>
public interface ITransactionalNotificationLogRepository : INotificationLogRepository
{
    /// <summary>
    /// Inserts a notification log entry using an existing open connection and transaction.
    /// </summary>
    /// <param name="orderId">The ID of the notification order.</param>
    /// <param name="connection">An existing open database connection.</param>
    /// <param name="transaction">An existing database transaction to enlist in.</param>
    /// <returns>The number of rows inserted.</returns>
    Task<int> InsertAsync(Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction);
}
