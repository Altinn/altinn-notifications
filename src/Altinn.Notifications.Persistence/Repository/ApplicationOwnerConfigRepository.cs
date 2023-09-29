using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Repository.Interfaces;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of order repository logic
/// </summary>
public class ApplicationOwnerConfigRepository : IApplicationOwnerConfigRepository
{
    private const string _getOrgSettingsSql =
        "SELECT _id, orgid, emailaddresses, smsnames FROM notifications.applicationownerconfig WHERE orgid=$1";

    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationOwnerConfigRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    public ApplicationOwnerConfigRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task<ApplicationOwnerConfig?> GetOrgSettings(string orgId)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrgSettingsSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, orgId);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            ApplicationOwnerConfig applicationOwnerConfig = new ApplicationOwnerConfig
            {
                OrgId = reader.GetString(1),
                EmailAddresses = reader.GetString(2)?.Split(',').ToList() ?? new List<string>(),
                SmsNames = reader.GetString(3)?.Split(',').ToList() ?? new List<string>()
            };
            return applicationOwnerConfig;
        }

        return null;
    }
}
