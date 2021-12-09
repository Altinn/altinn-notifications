using System.Net.Http;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core;
using Altinn.Notifications.Tests.Mocks;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Tests.IntegrationTests.Utils
{
    public static class SetupUtil
    {
        public static HttpClient GetTestClient(CustomWebApplicationFactory<NotificationsController> customFactory)
        {
            WebApplicationFactory<NotificationsController> factory = customFactory.WithWebHostBuilder(builder =>
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
