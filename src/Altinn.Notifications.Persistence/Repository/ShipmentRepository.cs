using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of the <see cref="IShipmentRepository"/>
/// providing persistence operations for notification shipment data.
/// </summary>
public class ShipmentRepository : IShipmentRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private const string _getShipmentTracking = "select * from notifications.get_shipment_tracking($1, $2)"; // _alternateid, _creatorname

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipmentRepository"/> class.
    /// </summary>
    public ShipmentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<IShipmentDeliveryManifest?> GetDeliveryManifest(Guid shipmentId, string creatorName)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(_getShipmentTracking);
        command.Parameters.AddWithValue(NpgsqlDbType.Uuid, shipmentId);
        command.Parameters.AddWithValue(NpgsqlDbType.Text, creatorName);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        if (!reader.HasRows)
        {
            return null;
        }

        await reader.ReadAsync();

        return ReadShipmentDeliveryManifest(reader, shipmentId);
    }

    /// <summary>
    /// Reads shipment delivery manifest data from the database reader.
    /// </summary>
    /// <param name="reader">The database reader containing shipment tracking data.</param>
    /// <param name="shipmentId">The shipment identifier.</param>
    /// <returns>A ShipmentDeliveryManifest containing order and recipient delivery information.</returns>
    private static ShipmentDeliveryManifest ReadShipmentDeliveryManifest(NpgsqlDataReader reader, Guid shipmentId)
    {
        string? orderStatus = null;
        string? sendersReference = null;
        DateTime orderLastUpdate = DateTime.MinValue;
        var deliverables = new List<IDeliverableEntity>();

        if (!reader.IsDBNull(reader.GetOrdinal("status")))
        {
            orderStatus = reader.GetString(reader.GetOrdinal("status"));
        }

        if (!reader.IsDBNull(reader.GetOrdinal("reference")))
        {
            sendersReference = reader.GetString(reader.GetOrdinal("reference"));
        }

        if (!reader.IsDBNull(reader.GetOrdinal("last_update")))
        {
            orderLastUpdate = reader.GetDateTime(reader.GetOrdinal("last_update"));
        }

        do
        {
            if (!reader.IsDBNull(reader.GetOrdinal("destination")))
            {
                string? destination = reader.GetString(reader.GetOrdinal("destination"));

                string? status = null;
                if (!reader.IsDBNull(reader.GetOrdinal("status")))
                {
                    status = reader.GetString(reader.GetOrdinal("status"));
                }

                DateTime lastUpdate = DateTime.MinValue;
                if (!reader.IsDBNull(reader.GetOrdinal("last_update")))
                {
                    lastUpdate = reader.GetDateTime(reader.GetOrdinal("last_update"));
                }

                if (destination.Contains('@'))
                {
                    deliverables.Add(new EmailDeliveryManifest
                    {
                        LastUpdate = lastUpdate,
                        Destination = destination,
                        Status = status ?? "Unknown",
                        StatusDescription = status ?? "Unknown"
                    });
                }
                else
                {
                    deliverables.Add(new SmsDeliveryManifest
                    {
                        LastUpdate = lastUpdate,
                        Destination = destination,
                        Status = status ?? "Unknown",
                        StatusDescription = status ?? "Unknown"
                    });
                }
            }
        }
        while (reader.Read());

        return new ShipmentDeliveryManifest
        {
            Type = "Notification",
            ShipmentId = shipmentId,
            LastUpdate = orderLastUpdate,
            Status = orderStatus ?? "Unknown",
            SendersReference = sendersReference,
            Recipients = deliverables.ToImmutableList(),
            StatusDescription = orderStatus ?? "Unknown"
        };
    }
}
