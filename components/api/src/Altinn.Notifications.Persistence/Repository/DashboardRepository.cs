using Altinn.Notifications.Core.Persistence;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of dashboard repository logic
/// </summary>
public class DashboardRepository : IDashboardRepository
{
    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    public DashboardRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }
}
