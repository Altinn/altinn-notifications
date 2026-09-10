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
            try
            {
                var transaction = await connection.BeginTransactionAsync();
                return new UnitOfWork
                {
                    Connection = connection,
                    Transaction = transaction
                };
            }
            catch
            {
                await connection.CloseAsync();
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task RollbackUnitOfWork(UnitOfWork unitOfWork)
        {
            try
            {
                await unitOfWork.Transaction.RollbackAsync();
            }
            finally
            {
                await unitOfWork.Connection.CloseAsync();
            }
        }

        /// <inheritdoc/>
        public async Task CommitUnitOfWork(UnitOfWork unitOfWork)
        {
            try
            {
                await unitOfWork.Transaction.CommitAsync();
            }
            finally
            {
                await unitOfWork.Connection.CloseAsync();
            }
        }
    }
}
