using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Persistence.Mappers;

using Npgsql;

namespace Altinn.Notifications.Persistence.Utils;

/// <summary>
/// Utility class for notification-related operations shared between different repositories.
/// </summary>
internal static class NotificationUtil
{
    private static readonly string[] _legalRecipientTypes = ["email", "sms"];

    /// <summary>
    /// Method to read recipient level notifications from the data reader and populate the recipients list.
    /// </summary>
    /// <param name="recipients">The list of recipients to populate.</param>
    /// <param name="reader">The data reader to read from. Disposal should be handled by the caller</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation</param>
    /// <returns>A asynchronous Task</returns>
    internal static async Task ReadRecipientsAsync(List<Recipient> recipients, NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var statusOrdinal = reader.GetOrdinal("status");
        var destinationOrdinal = reader.GetOrdinal("destination");
        var typeOrdinal = reader.GetOrdinal("type");
        var lastUpdateOrdinal = reader.GetOrdinal("last_update");

        while (await reader.ReadAsync(cancellationToken))
        {
            var notificationType = await reader.GetFieldValueAsync<string>(typeOrdinal, cancellationToken);

            if (!_legalRecipientTypes.Contains(notificationType, StringComparer.OrdinalIgnoreCase))
            {
                // Skip non-recipient level notifications
                continue;
            }

            var status = await reader.GetFieldValueAsync<string>(statusOrdinal, cancellationToken);
            var destination = await reader.IsDBNullAsync(destinationOrdinal) ? string.Empty : await reader.GetFieldValueAsync<string>(destinationOrdinal, cancellationToken);

            Recipient recipient;

            if (notificationType.Equals("email", StringComparison.OrdinalIgnoreCase))
            {
                recipient = new Recipient
                {
                    Destination = destination,
                    LastUpdate = await reader.GetFieldValueAsync<DateTime>(lastUpdateOrdinal, cancellationToken),
                    Status = ProcessingLifecycleMapper.GetEmailLifecycleStage(status)
                };
                recipients.Add(recipient);
            }
            else if (notificationType.Equals("sms", StringComparison.OrdinalIgnoreCase))
            {
                recipient = new Recipient
                {
                    Destination = destination,
                    LastUpdate = await reader.GetFieldValueAsync<DateTime>(lastUpdateOrdinal, cancellationToken),
                    Status = ProcessingLifecycleMapper.GetSmsLifecycleStage(status)
                };
                recipients.Add(recipient);
            }
        }
    }
}
