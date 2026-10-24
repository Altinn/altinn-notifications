using Altinn.Notifications.Tools.Simulator.Configuration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Altinn.Notifications.Tools.Tests.Simulator;

/// <summary>
/// The simulator with its triggers paused and pointed at an address that does not resolve, so a test never triggers a local API on 5090.
/// </summary>
public class SimulatorFactory : WebApplicationFactory<SimulatorSettings>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("TriggerSettings:Enabled", "false");
        builder.UseSetting("TriggerSettings:ApiBaseUrl", "http://api.invalid");
    }
}
