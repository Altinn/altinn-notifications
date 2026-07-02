using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Coordinates the processing of email send requests by submitting them to Azure Communication Services (ACS)
/// and directing the resulting outcome—success or failure—to the appropriate downstream handlers.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SendingService"/> class.
/// </remarks>
public class SendingService(
    IEmailServiceClient emailServiceClient,
    IEmailStatusCheckDispatcher emailStatusCheckDispatcher,
    IEmailSendResultDispatcher emailSendingStatusDispatcher,
    IEmailServiceRateLimitDispatcher emailServiceRateLimitDispatcher) : ISendingService
{
    private readonly IEmailServiceClient _emailServiceClient = emailServiceClient;
    private readonly IEmailStatusCheckDispatcher _emailStatusCheckDispatcher = emailStatusCheckDispatcher;
    private readonly IEmailSendResultDispatcher _emailSendingStatusDispatcher = emailSendingStatusDispatcher;
    private readonly IEmailServiceRateLimitDispatcher _emailServiceRateLimitDispatcher = emailServiceRateLimitDispatcher;

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
                await HandleSendFailAsync(email.NotificationId, emailSendFailResponse);
            });
    }

    /// <inheritdoc/>
    public async Task SendComposedAsync(ComposedEmail email)
    {
        try
        {
            Result<ComposedEmailSendResult, EmailClientErrorResponse> result = await _emailServiceClient.SendComposedEmail(email);

            await result.Match(
                async composedResult =>
                {
                    await _emailStatusCheckDispatcher.DispatchAsync(
                        email.NotificationId,
                        composedResult.OperationId,
                        composedResult.EncodedAttachmentsSize);
                },
                async emailSendFailResponse =>
                {
                    await HandleSendFailAsync(email.NotificationId, emailSendFailResponse);
                });
        }
        catch (InvalidSasUrlException)
        {
            var operationResult = new SendOperationResult
            {
                NotificationId = email.NotificationId,
                SendResult = EmailSendResult.Failed_InvalidSasUrl
            };

            try
            {
                await _emailSendingStatusDispatcher.DispatchAsync(operationResult);
            }
            catch (Exception)
            {
                // Non-fatal: the outer rethrow ensures Wolverine moves the message to the error queue
            }

            throw;
        }
    }

    /// <summary>
    /// Handles a failed email send attempt by dispatching a rate limit notification for transient errors
    /// and forwarding the send result to the status dispatcher.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the notification that failed to send.</param>
    /// <param name="emailSendFailResponse">The error response from the email client containing the failure details.</param>
    private async Task HandleSendFailAsync(Guid notificationId, EmailClientErrorResponse emailSendFailResponse)
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
            NotificationId = notificationId,
            SendResult = emailSendFailResponse.SendResult
        };

        await _emailSendingStatusDispatcher.DispatchAsync(operationResult);
    }
}
