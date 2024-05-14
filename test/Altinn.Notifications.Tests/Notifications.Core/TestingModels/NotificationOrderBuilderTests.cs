using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels
{
    public class NotificationOrderBuilderTests
    {
        [Fact]
        public void Build_ThrowsExceptionWhenIdIsNotSet()
        {
            Assert.Throws<ArgumentException>(() => NotificationOrder.GetBuilder().Build());
        }

        [Fact]
        public void Build_NoPropertiesHaveDefaultValues()
        {
            var builder = new NotificationOrder.NotificationOrderBuilder();
            var id = Guid.NewGuid();
            var sendersReference = "reference";
            var requestedSendTime = DateTime.UtcNow;
            var notificationChannel = NotificationChannel.Sms;
            var ignoreReservation = true;
            var creator = new Creator("ssb");
            var created = DateTime.UtcNow;
            var templates = new List<INotificationTemplate>() { new SmsTemplate("Altinn", "SMS body") };
            var recipients = new List<Recipient>();

            builder.SetId(id);
            builder.SetSendersReference(sendersReference);
            builder.SetRequestedSendTime(requestedSendTime);
            builder.SetNotificationChannel(notificationChannel);
            builder.SetIgnoreReservation(ignoreReservation);
            builder.SetCreator(creator);
            builder.SetCreated(created);
            builder.SetTemplates(templates);
            builder.SetRecipients(recipients);

            var order = builder.Build();

            var properties = typeof(NotificationOrder).GetProperties();

            foreach (var property in properties)
            {
                var value = property.GetValue(order);
                var defaultValue = GetDefault(property.PropertyType);

                Assert.NotEqual(defaultValue, value);
            }
        }

        private object? GetDefault(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }
    }
}
