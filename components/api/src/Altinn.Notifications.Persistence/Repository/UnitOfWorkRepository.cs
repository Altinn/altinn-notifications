using Altinn.Notifications.Core.Persistence;
using Npgsql;

namespace Altinn.Notifications.Persistence.Repository
{
    /// <summary>
    /// Repository for handling unit of work related operations.
    /// </summary>
    /// <param name="dataSource">The npgsql data source</param>
    public class UnitOfWorkRepository(NpgsqlDataSource dataSource) : IUnitOfWorkRepository
    {
        /// <inheritdoc/>
        public async Task<UnitOfWork> StartUnitOfWork()
        {
            var connection = await dataSource.OpenConnectionAsync();
            var transaction = await connection.BeginTransactionAsync();
            return new UnitOfWork
            {
                Connection = connection,
                Transaction = transaction
            };
        }

        /// <inheritdoc/>
        public async Task RollbackUnitOfWork(UnitOfWork unitOfWork)
        {
            await unitOfWork.Transaction.RollbackAsync();
            await unitOfWork.Connection.CloseAsync();
        }

        /// <inheritdoc/>
        public async Task CommitUnitOfWork(UnitOfWork unitOfWork)
        {
            await unitOfWork.Transaction.CommitAsync();
            await unitOfWork.Connection.CloseAsync();
        }
    }
}
