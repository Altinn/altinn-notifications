using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels
{
    public class NotificationOrderWithStatusBuilderTests
    {
        [Fact]
        public void Build_NoPropertiesHaveDefaultValues()
        {
            var builder = new NotificationOrderWithStatusBuilder();
            var id = Guid.NewGuid();
            var requestedSendTime = DateTime.UtcNow;
            var creator = "ssb";
            var created = DateTime.UtcNow;
            var notificationChannel = NotificationChannel.Sms;
            var processingStatus = new ProcessingStatus { Status = OrderProcessingStatus.Registered };
            var sendersReference = "reference";
            var ignoreReservation = true;

            builder.SetId(id);
            builder.SetRequestedSendTime(requestedSendTime);
            builder.SetCreator(creator);
            builder.SetCreated(created);
            builder.SetNotificationChannel(notificationChannel);
            builder.SetProcessingStatus(processingStatus);
            builder.SetSendersReference(sendersReference);
            builder.SetIgnoreReservation(ignoreReservation);

            var order = builder.Build();

            var properties = typeof(NotificationOrderWithStatus).GetProperties();

            foreach (var property in properties)
            {
                var value = property.GetValue(order);
                var defaultValue = TestdataUtil.GetDefaultValue(property.PropertyType);

                Assert.NotEqual(defaultValue, value);
            }
        }
    }
}
