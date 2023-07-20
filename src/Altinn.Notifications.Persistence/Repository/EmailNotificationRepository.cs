using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Repository.Interfaces;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of order repository logic
/// </summary>
public class EmailNotificationRepository : IEmailNotificationsRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _insertEmailNotificationSql = "select * from notifications.orders limit 1";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    public EmailNotificationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task AddEmailNotification(EmailNotification notification, DateTime expiry)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertEmailNotificationSql);

        await pgcom.ExecuteNonQueryAsync();
    }

    public Task<List<Email>> GetNewNotifications()
    {
        throw new NotImplementedException();
    }
}