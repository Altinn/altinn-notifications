using Altinn.Notifications.Functions;
using Altinn.Notifications.Functions.Configurations;
using Altinn.Notifications.Functions.Integrations;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: WebJobsStartup(typeof(Startup))]

namespace Altinn.Notifications.Functions
{
    /// <summary>
    /// Function events startup
    /// </summary>
    public class Startup : IWebJobsStartup
    {
        /// <summary>
        /// Gets functions project configuration
        /// </summary>
        public void Configure(IWebJobsBuilder builder)
        {
            builder.Services.AddOptions<PlatformSettings>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("Platform").Bind(settings);
            });
            builder.Services.AddOptions<KeyVaultSettings>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("KeyVault").Bind(settings);
            });

            builder.Services.AddSingleton<ISchedule, ScheduleService>();
            builder.Services.AddHttpClient<INotifications, NotificationsClient>();
            // builder.Services.AddSingleton<IAccessTokenGenerator, AccessTokenGenerator>();
            builder.Services.AddSingleton<IKeyVaultService, KeyVaultService>();
            builder.Services.AddSingleton<IQueue, QueueService>();
            //builder.Services.AddSingleton<IToken, TokenService>();
        }
    }
}
