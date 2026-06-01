using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Coordinates the processing of email send requests by submitting them to Azure Communication Services (ACS)
/// and directing the resulting outcome—success or failure—to the appropriate downstream handlers.
/// </summary>
public class SendingService : ISendingService
{
    private readonly IEmailServiceClient _emailServiceClient;
    private readonly IEmailStatusCheckDispatcher _emailStatusCheckDispatcher;
    private readonly IEmailSendResultDispatcher _emailSendingStatusDispatcher;
    private readonly IEmailServiceRateLimitDispatcher _emailServiceRateLimitDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendingService"/> class.
    /// </summary>
    public SendingService(
        IEmailServiceClient emailServiceClient,
        IEmailStatusCheckDispatcher emailStatusCheckDispatcher,
        IEmailSendResultDispatcher emailSendingStatusDispatcher,
        IEmailServiceRateLimitDispatcher emailServiceRateLimitDispatcher)
    {
        _emailServiceClient = emailServiceClient;
        _emailStatusCheckDispatcher = emailStatusCheckDispatcher;
        _emailSendingStatusDispatcher = emailSendingStatusDispatcher;
        _emailServiceRateLimitDispatcher = emailServiceRateLimitDispatcher;
    }

    /// <inheritdoc/>
    public async Task SendAsync(Email email)
    {
        Result<string, EmailClientErrorResponse> result = await _emailServiceClient.SendEmail(email);

        await result.Match(
            async operationId =>
            {
                await _emailStatusCheckDispatcher.DispatchAsync(email.NotificationId, operationId);
            },
            async emailSendFailResponse =>
            {
                if (emailSendFailResponse.SendResult == EmailSendResult.Failed_TransientError)
                {
                    var resourceLimitExceeded = new ResourceLimitExceeded
                    {
                        Resource = "azure-communication-services-email",
                        ResetTime = DateTime.UtcNow.AddSeconds((double)emailSendFailResponse.IntermittentErrorDelay!)
                    };

                    var genericServiceUpdate = new GenericServiceUpdate
                    {
                        Source = "platform-notifications-email",
                        Data = resourceLimitExceeded.Serialize(),
                        Schema = AltinnServiceUpdateSchema.ResourceLimitExceeded
                    };

                    await _emailServiceRateLimitDispatcher.DispatchAsync(genericServiceUpdate);
                }

                var operationResult = new SendOperationResult
                {
                    NotificationId = email.NotificationId,
                    SendResult = emailSendFailResponse.SendResult
                };

                await _emailSendingStatusDispatcher.DispatchAsync(operationResult);
            });
    }
}
