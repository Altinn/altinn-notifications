using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of the <see cref="IServiceOwnerRepository"/> interface
/// </summary>
public class ServiceOwnerRepository(NpgsqlDataSource dataSource) : IServiceOwnerRepository
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    private const string _getRegisteredSendersSql = @"
        SELECT serviceowner, addresstypeid, senderaddress
        FROM notifications.serviceowner";

    /// <summary>
    /// Reads all rows of the notifications.serviceowner table in a single query and maps each address type id to a <see cref="SenderAddressType"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list with one entry for each registered sender, or an empty list if none are registered.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a row refers to an address type id that is not a known <see cref="SenderAddressType"/>.</exception>
    public async Task<IReadOnlyList<RegisteredSender>> GetRegisteredSenders(CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getRegisteredSendersSql);
        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);

        List<RegisteredSender> senders = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            short typeId = reader.GetInt16(1);
            if (!Enum.IsDefined(typeof(SenderAddressType), (int)typeId))
            {
                throw new InvalidOperationException($"Unknown sender address type id '{typeId}'.");
            }

            senders.Add(new RegisteredSender(reader.GetString(0), (SenderAddressType)typeId, reader.GetString(2)));
        }

        return senders;
    }
}
