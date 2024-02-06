﻿using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Interface describing the notification summary service
    /// </summary>
    public interface ISmsNotificationSummaryService
    {
        /// <summary>
        /// Gets a summary of all the generated sms notifications for the provided order id
        /// </summary>
        /// <param name="orderId">The order id to find notifications for</param>
        /// <param name="creator">The creator of the order</param>
        public Task<Result<SmsNotificationSummary, ServiceError>> GetSmsSummary(Guid orderId, string creator);
    }
}
