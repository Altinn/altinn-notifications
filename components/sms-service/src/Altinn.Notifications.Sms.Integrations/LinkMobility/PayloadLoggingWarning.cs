using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
/// Logs a warning at startup when <see cref="SmsGatewaySettings.LogPayloads"/> is <see cref="PayloadLogMode.Plain"/>, so it is
/// not left on by accident.
/// </summary>
internal sealed class PayloadLoggingWarning(ILogger<PayloadLoggingHandler> logger) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "SmsGatewaySettings:LogPayloads is Plain: each call to LinkMobility is logged with recipient numbers and message texts, which can hold one-time codes. Only <PW> and Authorization are masked. Set it to Off when done.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
