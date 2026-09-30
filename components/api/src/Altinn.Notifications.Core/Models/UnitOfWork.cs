using Npgsql;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents a unit of work that encapsulates a database connection and transaction.
/// </summary>
public class UnitOfWork
{
    /// <summary>
    /// Gets or sets the NpgsqlConnection used for the unit of work.
    /// </summary>
    public NpgsqlConnection Connection { get; set; } = null!;

    /// <summary>
    /// Gets or sets the NpgsqlTransaction used for the unit of work.
    /// </summary>
    public NpgsqlTransaction Transaction { get; set; } = null!;
}
