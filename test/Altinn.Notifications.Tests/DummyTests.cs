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
        /// This test depends on a running database server. It was used to test the data access code and database entities.
        /// </summary>
        ///[Fact]
        public async Task AddNotification_ActualServer()
        {
            const string connectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password=Password;Database=notificationsdb";

            Mock<ILogger<NotificationRepository>> logger = new Mock<ILogger<NotificationRepository>>();
            NotificationRepository target = new NotificationRepository(connectionString, logger.Object);

            Notification notification = new()
            {
                SendTime = DateTime.UtcNow,
                InstanceId = "averyvierdinstanceid",
                Sender = "sender"
            };

            Notification actual = await target.AddNotification(notification);

            Assert.NotNull(actual);
        }

        /// <summary>
        /// This test depends on a running database server. It was used to test the data access code and database entities.
        /// </summary>
        ///[Fact]
        public async Task AddTarget_ActualServer()
        {
            const string connectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password=Password;Database=notificationsdb";

            Mock<ILogger<NotificationRepository>> logger = new Mock<ILogger<NotificationRepository>>();
            NotificationRepository targeted = new NotificationRepository(connectionString, logger.Object);

            Target target = new Target();
            target.NotificationId = 234;
            target.ChannelType = "SMS";
            target.Address = "terje er kul";

            Target actual = await targeted.AddTarget(target);

            Assert.NotNull(actual);
        }

        /// <summary>
        /// This test depends on a running database server. It was used to test the data access code and database entities.
        /// </summary>
        ///[Fact]
        public async Task AddMessage_ActualServer()
        {
            const string connectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password=Password;Database=notificationsdb";

            Mock<ILogger<NotificationRepository>> logger = new Mock<ILogger<NotificationRepository>>();
            NotificationRepository targeted = new NotificationRepository(connectionString, logger.Object);

            Message message = new Message();
            message.NotificationId = 234;
            message.EmailSubject = "The coolest";
            message.EmailBody = "terje er kul";
            message.Language = "nb";

            Message actual = await targeted.AddMessage(message);

            Assert.NotNull(actual);
        }

        /// <summary>
        /// This test depends on a running database server. It was used to test the data access code and database entities.
        /// </summary>
        ///[Fact]
        public async Task GetNotification_ActualServer()
        {
            const string connectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password=Password;Database=notificationsdb";

            Mock<ILogger<NotificationRepository>> logger = new Mock<ILogger<NotificationRepository>>();
            NotificationRepository target = new NotificationRepository(connectionString, logger.Object);

            Notification actual = await target.GetNotification(1);

            Assert.NotNull(actual);

            Assert.Equal("averyvierdinstanceid", actual.InstanceId);
        }

        /// <summary>
        /// This test depends on a running database server. It was used to test the data access code and database entities.
        /// </summary>
        ///[Fact]
        public async Task GetUnsentTargets_ActualServer()
        {
            const string connectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password=Password;Database=notificationsdb";

            Mock<ILogger<NotificationRepository>> logger = new Mock<ILogger<NotificationRepository>>();
            NotificationRepository target = new NotificationRepository(connectionString, logger.Object);

            List<Target> actual = await target.GetUnsentTargets();

            Assert.NotNull(actual);
        }
    }
}
