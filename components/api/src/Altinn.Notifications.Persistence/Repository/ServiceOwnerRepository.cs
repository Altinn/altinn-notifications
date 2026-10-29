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
        SELECT sos.serviceowner, sat.name, sos.senderaddress
        FROM notifications.serviceowner sos
        INNER JOIN notifications.senderaddresstype sat ON sat.id = sos.addresstypeid";

    /// <summary>
    /// Reads all rows of the notifications.serviceowner table in a single query and maps each address type name to a <see cref="SenderAddressType"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list with one entry for each registered sender, or an empty list if none are registered.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a row refers to an address type name that is not a known <see cref="SenderAddressType"/>.</exception>
    public async Task<IReadOnlyList<RegisteredSender>> GetRegisteredSenders(CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getRegisteredSendersSql);
        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);

        List<RegisteredSender> senders = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            string typeName = reader.GetString(1);
            if (!Enum.TryParse(typeName, ignoreCase: true, out SenderAddressType addressType))
            {
                throw new InvalidOperationException($"Unknown sender address type '{typeName}'.");
            }

            senders.Add(new RegisteredSender(reader.GetString(0), addressType, reader.GetString(2)));
        }

        return senders;
    }
}
