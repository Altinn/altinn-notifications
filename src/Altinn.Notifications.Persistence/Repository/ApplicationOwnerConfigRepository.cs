using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Repository.Interfaces;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of order repository logic
/// </summary>
public sealed class ApplicationOwnerConfigRepository : IApplicationOwnerConfigRepository
{
    private const string _selectApplicationOwnerConfigSql =
        "SELECT _id, orgid, emailaddresses, smsnames " +
        "FROM notifications.applicationownerconfig " +
        "WHERE orgid=$1";

    private const string _writeApplicationOwnerConfigSql =
        "INSERT INTO notifications.applicationownerconfig(orgid, emailaddresses, smsnames) " +
        "  VALUES ($1, $2, $3) " +
        "ON CONFLICT (orgid) " +
        "  DO UPDATE SET emailaddresses = $2, smsnames = $3";

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
    public async Task<ApplicationOwnerConfig?> GetApplicationOwnerConfig(string orgId)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_selectApplicationOwnerConfigSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, orgId);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            ApplicationOwnerConfig applicationOwnerConfig = new(reader.GetString(1))
            {
                EmailAddresses = reader.GetString(2).Split(',').ToList(),
                SmsNames = reader.GetString(3).Split(',').ToList()
            };

            return applicationOwnerConfig;
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task WriteApplicationOwnerConfig(ApplicationOwnerConfig applicationOwnerConfig)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_writeApplicationOwnerConfigSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, applicationOwnerConfig.OrgId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.Join(',', applicationOwnerConfig.EmailAddresses));
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.Join(',', applicationOwnerConfig.SmsNames));

        _ = await pgcom.ExecuteNonQueryAsync();
    }
}
