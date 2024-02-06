using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface describing all repository operations related to notification summaries
/// </summary>
public interface INotificationSummaryRepository
{
    /// <summary>
    /// Retrieves all email notifications for the provided order id in an email notification summary
    /// </summary>
    /// <returns>A partial email notification summary object</returns>
    public Task<EmailNotificationSummary?> GetEmailSummary(Guid orderId, string creator);
}
