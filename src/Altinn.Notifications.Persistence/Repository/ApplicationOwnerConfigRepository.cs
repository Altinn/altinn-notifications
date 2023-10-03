using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Persistence.Extensions;

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
            string? emailAddresses = reader.GetValue<string?>("emailaddresses");
            string? smsNames = reader.GetValue<string?>("smsnames");

            ApplicationOwnerConfig applicationOwnerConfig = new(orgId)
            {
                EmailAddresses = emailAddresses?.Split(',').ToList() ?? new List<string>(),
                SmsNames = smsNames?.Split(',').ToList() ?? new List<string>()
            };

            return applicationOwnerConfig;
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task WriteApplicationOwnerConfig(ApplicationOwnerConfig applicationOwnerConfig)
    {
        string? emailAddresses = applicationOwnerConfig.EmailAddresses.Count > 0 
            ? string.Join(',', applicationOwnerConfig.EmailAddresses)
            : null;

        string? smsNames = applicationOwnerConfig.SmsNames.Count > 0
            ? string.Join(',', applicationOwnerConfig.SmsNames)
            : null;

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_writeApplicationOwnerConfigSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, applicationOwnerConfig.OrgId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailAddresses ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, smsNames ?? (object)DBNull.Value);

        _ = await pgcom.ExecuteNonQueryAsync();
    }
}
