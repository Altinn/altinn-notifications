using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels
{
    public class NotificationOrderRequestBuilderTests
    {
        [Fact]
        public void Build_ThrowsExceptionWhenNotAllPropertiesAreSet()
        {
            var builder = new NotificationOrderRequestBuilder();
            Assert.Throws<ArgumentException>(() => builder.Build());
        }

        [Fact]
        public void Build_NoPropertiesHaveDefaultValues()
        {
            var builder = new NotificationOrderRequestBuilder();
            var sendersReference = "reference";
            var requestedSendTime = DateTime.UtcNow;
            var notificationChannel = NotificationChannel.Sms;
            var templates = new List<INotificationTemplate>() { new SmsTemplate("Altinn", "SMS body") };
            var recipients = new List<Recipient>();
            var creator = "ssb";
            var ignoreReservation = true;

            builder.SetSendersReference(sendersReference);
            builder.SetRequestedSendTime(requestedSendTime);
            builder.SetNotificationChannel(notificationChannel);
            builder.SetTemplates(templates);
            builder.SetRecipients(recipients);
            builder.SetCreator(creator);
            builder.SetIgnoreReservation(ignoreReservation);

            var orderRequest = builder.Build();

            var properties = typeof(NotificationOrderRequest).GetProperties();

            foreach (var property in properties)
            {
                var value = property.GetValue(orderRequest);
                var defaultValue = TestdataUtil.GetDefaultValue(property.PropertyType);

                Assert.NotEqual(defaultValue, value);
            }
        }
    }
}
