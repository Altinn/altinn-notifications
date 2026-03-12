using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Shared;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
/// Mock implementation of <see cref="ISmsClient"/> for local development.
/// Simulates successful SMS sends without requiring Link Mobility credentials.
/// </summary>
public class MockSmsClient : ISmsClient
{
    private readonly ILogger<MockSmsClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockSmsClient"/> class.
    /// </summary>
    public MockSmsClient(ILogger<MockSmsClient> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Result<string, SmsClientErrorResponse>> SendAsync(Core.Sending.Sms sms)
    {
        string gatewayReference = Guid.NewGuid().ToString();
        _logger.LogInformation("MockSmsClient: Simulated send for {NotificationId}, ref={GatewayReference}", sms.NotificationId, gatewayReference);
        Result<string, SmsClientErrorResponse> result = gatewayReference;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<Result<string, SmsClientErrorResponse>> SendAsync(Core.Sending.Sms sms, int timeToLiveInSeconds)
    {
        return SendAsync(sms);
    }
}
