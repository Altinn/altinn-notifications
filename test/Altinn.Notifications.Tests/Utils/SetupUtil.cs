using Altinn.Notifications.Core;
using Altinn.Notifications.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Tests.Utils
{
    public static class SetupUtil
    {
        public static HttpClient GetTestClient(
        CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> customFactory)
        {
            WebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> factory = customFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IEmail, EmailServiceMock>();
                    services.AddSingleton<INotificationsRepository, NotificationRepositoryMock>();
                });
            });

            factory.Server.AllowSynchronousIO = true;
            return factory.CreateClient();
        }
    }
}
