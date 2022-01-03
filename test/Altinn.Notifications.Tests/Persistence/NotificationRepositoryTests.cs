using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Persistence;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Persistence
{
    public  class NotificationRepositoryTests
    {
        /// <summary>
        /// This test depends on a running database server. It was used to test the data access code and database entities.
        /// </summary>
        ///[Fact]
        public async Task AddNotification_ActualServer()
        {
            PostgreSQLSettings postgreSQLSettings = new() 
            { 
                ConnectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password={0};Database=notificationsdb",
                NotificationsDbPwd = "Password"
            };
            
            Mock<IOptions<PostgreSQLSettings>> options = new();
            options.Setup(s => s.Value).Returns(postgreSQLSettings);

            NotificationRepository target = new NotificationRepository(options.Object);

            Notification notification = new()
            {
                SendTime = DateTime.UtcNow,
                InstanceId = "partyid/instanceguid",
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
            PostgreSQLSettings postgreSQLSettings = new()
            {
                ConnectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password={0};Database=notificationsdb",
                NotificationsDbPwd = "Password"
            };

            Mock<IOptions<PostgreSQLSettings>> options = new();
            options.Setup(s => s.Value).Returns(postgreSQLSettings);

            NotificationRepository targeted = new NotificationRepository(options.Object);

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
            PostgreSQLSettings postgreSQLSettings = new()
            {
                ConnectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password={0};Database=notificationsdb",
                NotificationsDbPwd = "Password"
            };

            Mock<IOptions<PostgreSQLSettings>> options = new();
            options.Setup(s => s.Value).Returns(postgreSQLSettings);

            NotificationRepository targeted = new NotificationRepository(options.Object);

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
            PostgreSQLSettings postgreSQLSettings = new()
            {
                ConnectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password={0};Database=notificationsdb",
                NotificationsDbPwd = "Password"
            };

            Mock<IOptions<PostgreSQLSettings>> options = new();
            options.Setup(s => s.Value).Returns(postgreSQLSettings);

            NotificationRepository target = new NotificationRepository(options.Object);

            Notification actual = await target.GetNotification(1);

            Assert.NotNull(actual);

            Assert.Equal("averyvierdinstanceid", actual.InstanceId);
        }

        /// <summary>
        /// This test depends on a running database server. It was used to test the data access code and database entities.
        /// </summary>
        ///[Fact]
        public async Task GetTarget_ActualServer()
        {
            PostgreSQLSettings postgreSQLSettings = new()
            {
                ConnectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password={0};Database=notificationsdb",
                NotificationsDbPwd = "Password"
            };

            Mock<IOptions<PostgreSQLSettings>> options = new();
            options.Setup(s => s.Value).Returns(postgreSQLSettings);

            NotificationRepository target = new NotificationRepository(options.Object);

            Target actual = await target.GetTarget(2);

            Assert.NotNull(actual);
        }

        /// <summary>
        /// This test depends on a running database server. It was used to test the data access code and database entities.
        /// </summary>
        ///[Fact]
        public async Task GetUnsentTargets_ActualServer()
        {
            PostgreSQLSettings postgreSQLSettings = new()
            {
                ConnectionString = "Host=localhost;Port=5432;Username=platform_notifications;Password={0};Database=notificationsdb",
                NotificationsDbPwd = "Password"
            };

            Mock<IOptions<PostgreSQLSettings>> options = new();
            options.Setup(s => s.Value).Returns(postgreSQLSettings);

            NotificationRepository target = new NotificationRepository(options.Object);

            List<Target> actual = await target.GetUnsentTargets();

            Assert.NotNull(actual);
        }
    }
}
