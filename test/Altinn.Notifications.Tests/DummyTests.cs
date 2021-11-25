using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Persistence;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests
{
    public  class DummyTests
    {
        [Fact]
        public void InitialTest()
        {
            string actual = "Stephanie er kul";

            Assert.Equal("Stephanie er kul", actual);
        }

        /// <summary>
        /// This test depends on a running database server.
        /// </summary>
        ///[Fact]
        public async Task ForceNotificationInsert()
        {
            const string connectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password=Password;Database=notificationsdb";

            Mock<ILogger<NotificationRepository>> logger = new Mock<ILogger<NotificationRepository>>();
            NotificationRepository target = new NotificationRepository(connectionString, logger.Object);

            Notification notification = new()
            {
                SendTime = DateTime.UtcNow,
                InstanceId = "notreallyaninstanceid",
                Sender = "sender"
            };

            Notification actual = await target.SaveNotification(notification);

            Assert.NotNull(actual);
        }
    }
}
