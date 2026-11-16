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
    /// Reads all rows of the notifications.serviceowner table in a single query and maps each address type id to an <see cref="AddressType"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list with one entry for each registered sender, or an empty list if none are registered.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a row refers to an address type id that is not in the notifications.senderaddresstype table.</exception>
    public async Task<IReadOnlyList<RegisteredSender>> GetRegisteredSenders(CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getRegisteredSendersSql);
        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);

        List<RegisteredSender> senders = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            senders.Add(new RegisteredSender(reader.GetString(0), ToAddressType(reader.GetInt16(1)), reader.GetString(2)));
        }

        return senders;
    }

    /// <summary>
    /// Maps an id of the notifications.senderaddresstype table to the matching <see cref="AddressType"/>.
    /// </summary>
    /// <param name="addressTypeId">The id of the address type in the notifications.senderaddresstype table.</param>
    /// <returns>The <see cref="AddressType"/> that the id represents.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the id is not in the notifications.senderaddresstype table.</exception>
    private static AddressType ToAddressType(short addressTypeId)
    {
        return addressTypeId switch
        {
            1 => AddressType.Sms,
            2 => AddressType.Email,
            _ => throw new InvalidOperationException($"Unknown sender address type id '{addressTypeId}'.")
        };
    }
}
