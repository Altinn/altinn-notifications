using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Shared;
using Altinn.Notifications.Sms.Core.Status;

namespace Altinn.Notifications.Sms.Core.Sending;

/// <summary>
/// Service responsible for sending SMS messages.
/// </summary>
public class SendingService : ISendingService
{
    private readonly ISmsClient _smsClient;
    private readonly ISmsSendResultDispatcher _smsSendResultDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendingService"/> class.
    /// </summary>
    public SendingService(ISmsClient smsClient, ISmsSendResultDispatcher smsSendResultDispatcher)
    {
        _smsClient = smsClient;
        _smsSendResultDispatcher = smsSendResultDispatcher;
    }

    /// <inheritdoc/>
    public async Task SendAsync(Sms sms)
    {
        var result = await _smsClient.SendAsync(sms);

        await ProcessSendResult(sms, result);
    }

    /// <inheritdoc/>
    public async Task SendAsync(Sms sms, int timeToLiveInSeconds)
    {
        var result = await _smsClient.SendAsync(sms, timeToLiveInSeconds);

        await ProcessSendResult(sms, result);
    }

    /// <summary>
    /// Processes the result of the send operation.
    /// </summary>
    /// <param name="sms">The SMS message that was attempted to be sent.</param>
    /// <param name="result">The result of the send operation, containing either a gateway reference or an error response.</param>
    private async Task ProcessSendResult(Sms sms, Result<string, SmsClientErrorResponse> result)
    {
        var operationResult = new SendOperationResult
        {
            NotificationId = sms.NotificationId
        };

        await result.Match(
            async gatewayReference =>
            {
                operationResult.GatewayReference = gatewayReference;
                operationResult.SendResult = SmsSendResult.Accepted;

                await _smsSendResultDispatcher.DispatchAsync(operationResult);
            },
            async smsSendFailResponse =>
            {
                operationResult.GatewayReference = string.Empty;
                operationResult.SendResult = smsSendFailResponse.SendResult;

                await _smsSendResultDispatcher.DispatchAsync(operationResult);
            });
    }
}
