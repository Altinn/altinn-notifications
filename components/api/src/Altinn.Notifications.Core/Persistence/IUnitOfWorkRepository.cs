using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Repository for handling unit of work related operations.
/// </summary>
public interface IUnitOfWorkRepository
{
    /// <summary>
    /// Starts a new database transaction by opening a connection and beginning a transaction.
    /// </summary>
    /// <returns></returns>
    public Task<UnitOfWork> StartUnitOfWork();

    /// <summary>
    /// Rolls back the changes made during the unit of work by rolling back the transaction and closing the connection.
    /// </summary>
    /// <param name="unitOfWork">The unit of work to roll back.</param>
    /// <returns></returns>
    public Task RollbackUnitOfWork(UnitOfWork unitOfWork);

    /// <summary>
    /// Commits the changes made during the unit of work by committing the transaction and closing the connection.
    /// </summary>
    /// <param name="unitOfWork">The unit of work to commit.</param>
    /// <returns></returns>
    public Task CommitUnitOfWork(UnitOfWork unitOfWork);
}
