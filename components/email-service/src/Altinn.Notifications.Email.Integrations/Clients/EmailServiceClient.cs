using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Clients.AzureCommunicationServices;
using Altinn.Notifications.Email.Integrations.Configuration;

using Azure;
using Azure.Communication.Email;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Clients;

/// <summary>
/// Represents an implementation of <see cref="IEmailServiceClient"/> that will use Azure Communication
/// Services to produce an email.
/// </summary>
[ExcludeFromCodeCoverage]
public class EmailServiceClient : IEmailServiceClient
{
    private readonly EmailClient _emailClient;
    private readonly ILogger<EmailServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailServiceClient"/> class.
    /// </summary>
    /// <param name="communicationServicesSettings">Settings for integration against Communication Services.</param>
    /// <param name="logger">A logger</param>
    public EmailServiceClient(CommunicationServicesSettings communicationServicesSettings, ILogger<EmailServiceClient> logger)
    {
        var emailClientOptions = new EmailClientOptions();
        emailClientOptions.AddPolicy(new TooManyRequestsPolicy(), HttpPipelinePosition.PerRetry);
        _emailClient = new EmailClient(communicationServicesSettings.ConnectionString);
        _logger = logger;
    }

    /// <summary>
    /// Send an email
    /// </summary>
    /// <param name="email">The email</param>
    /// <returns>A Task representing the asyncrhonous operation.</returns>
    public async Task<Result<string, EmailClientErrorResponse>> SendEmail(Core.Sending.Email email)
    {
        EmailContent emailContent = new(email.Subject);
        switch (email.ContentType)
        {
            case EmailContentType.Plain:
                emailContent.PlainText = email.Body;
                break;
            case EmailContentType.Html:
                emailContent.Html = email.Body;
                break;
            default:
                break;
        }

        EmailMessage emailMessage = new(email.FromAddress, email.ToAddress, emailContent);
        try
        {
            EmailSendOperation emailSendOperation = await _emailClient.SendAsync(WaitUntil.Started, emailMessage);

            return emailSendOperation.Id;
        }
        catch (RequestFailedException e)
        {
            _logger.LogError(e, "// EmailServiceClient // SendEmail // Failed to send email, NotificationId {NotificationId}", email.NotificationId);
            EmailClientErrorResponse emailSendFailResponse = new()
            {
                SendResult = GetEmailSendResult(e)
            };

            if (emailSendFailResponse.SendResult == Core.Status.EmailSendResult.Failed_TransientError)
            {
                emailSendFailResponse.IntermittentErrorDelay = GetDelayFromString(e.Message);
            }

            return emailSendFailResponse;
        }
    }

    /// <summary>
    /// Check the email sending operation status
    /// </summary>
    /// <returns>An email send result</returns>
    public async Task<Core.Status.EmailSendResult> GetOperationUpdate(string operationId)
    {
        var operation = new EmailSendOperation(operationId, _emailClient);
        try
        {
            await operation.UpdateStatusAsync();

            if (operation.HasCompleted && operation.HasValue)
            {
                var status = operation.Value.Status;
                if (status == EmailSendStatus.Succeeded)
                {
                    return Core.Status.EmailSendResult.Succeeded;
                }

                var response = await operation.WaitForCompletionResponseAsync();
                _logger.LogError(
                    "// EmailServiceClient // GetOperationUpdate // Operation {OperationId} failed with status {Status} and reason phrase {Reason}",
                    operationId,
                    status,
                    response.ReasonPhrase);
                return Core.Status.EmailSendResult.Failed;
            }
        }
        catch (RequestFailedException e)
        {
            if (e.ErrorCode == ErrorTypes.RecipientsSuppressedErrorCode)
            {
                _logger.LogWarning("A request failed because the recipient is on the suppression list of Azure Communcation Services, OperationId {OperationId}", operationId);
            }
            else
            {
                _logger.LogError(e, "// EmailServiceClient // GetOperationUpdate // Exception thrown when getting status, OperationId {OperationId}", operationId);
            }

            return GetEmailSendResult(e);
        }

        return Core.Status.EmailSendResult.Sending;
    }

    private static Core.Status.EmailSendResult GetEmailSendResult(RequestFailedException e)
    {
        Core.Status.EmailSendResult emailSendResult;

        if (e.ErrorCode == ErrorTypes.ExcessiveCallVolumeErrorCode)
        {
            emailSendResult = Core.Status.EmailSendResult.Failed_TransientError;
        }
        else if (e.ErrorCode == ErrorTypes.RecipientsSuppressedErrorCode)
        {
            emailSendResult = Core.Status.EmailSendResult.Failed_SupressedRecipient;
        }
        else if (e.Message.Contains(ErrorTypes.InvalidEmailFormatErrorMessage))
        {
            emailSendResult = Core.Status.EmailSendResult.Failed_InvalidEmailFormat;
        }
        else
        {
            emailSendResult = Core.Status.EmailSendResult.Failed;
        }

        return emailSendResult;
    }

    /// <summary>
    /// Gets the int proceeding the word seconds in the string.
    /// </summary>
    /// <param name="message">The messsage to find delay within</param>
    /// <returns></returns>
    internal static int GetDelayFromString(string message)
    {
        var secondsString = Regex.Match(
                message,
                @"(\d+)[^,.\d\n]+?(?=seconds)|(?<=seconds)[^,.\d\n]+?(\d+)",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(10))
                .Value;

        return string.IsNullOrEmpty(secondsString) ? 60 : int.Parse(secondsString);
    }
}
