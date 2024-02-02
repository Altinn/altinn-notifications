﻿using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Interface describing the notification summary service
    /// </summary>
    public interface INotificationSummaryService
    {
        /// <summary>
        /// Gets a summary of all the generated email notifications for the provided order id
        /// </summary>
        /// <param name="orderId">The order id to find notifications for</param>
        /// <param name="creator">The creator of the order</param>
        public Task<Result<EmailNotificationSummary, ServiceError>> GetEmailSummary(Guid orderId, string creator);
    }
}
