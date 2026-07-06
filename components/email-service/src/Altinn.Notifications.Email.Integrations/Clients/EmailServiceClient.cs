using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Clients.AzureCommunicationServices;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

using Azure;
using Azure.Communication.Email;
using Azure.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Email.Integrations.Clients;

/// <summary>
/// Represents an implementation of <see cref="IEmailServiceClient"/> that will use Azure Communication
/// Services to produce an email.
/// </summary>
[ExcludeFromCodeCoverage]
public class EmailServiceClient : IEmailServiceClient
{
    private readonly EmailClient _emailClient;
    private readonly int _blobDownloadConcurrency;
    private readonly ILogger<EmailServiceClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailServiceAdminSettings _emailServiceAdminSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailServiceClient"/> class.
    /// </summary>
    public EmailServiceClient(
        CommunicationServicesSettings communicationServicesSettings,
        EmailServiceAdminSettings emailServiceAdminSettings,
        IHttpClientFactory httpClientFactory,
        IOptions<WolverineSettings> wolverineSettings,
        ILogger<EmailServiceClient> logger)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _emailServiceAdminSettings = emailServiceAdminSettings;
        _blobDownloadConcurrency = wolverineSettings.Value.BlobDownloadConcurrency;

        var emailClientOptions = new EmailClientOptions();
        emailClientOptions.AddPolicy(new TooManyRequestsPolicy(), HttpPipelinePosition.PerRetry);
        _emailClient = new EmailClient(communicationServicesSettings.ConnectionString, emailClientOptions);
    }

    /// <summary>
    /// Send an email
    /// </summary>
    /// <param name="email">The email</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
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

    /// <inheritdoc/>
    public async Task<Result<ComposedEmailSendResult, EmailClientErrorResponse>> SendComposedEmail(ComposedEmail email, CancellationToken cancellationToken = default)
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
        }

        EmailMessage emailMessage = new(email.FromAddress, email.ToAddress, emailContent);

        using var httpClient = _httpClientFactory.CreateClient();
        using var semaphore = new SemaphoreSlim(_blobDownloadConcurrency);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var downloadTasks = email.Attachments
            .Select(attachment => DownloadAttachmentAsync(email.NotificationId, httpClient, semaphore, attachment, linkedCts.Token))
            .ToList();

        (string Filename, string MimeType, byte[] Data)[] downloaded = await Task.WhenAll(downloadTasks);

        long encodedAttachmentsSize = 0;
        foreach (var (filename, mimeType, data) in downloaded)
        {
            encodedAttachmentsSize += (long)Math.Ceiling(data.Length / 3.0) * 4;
            emailMessage.Attachments.Add(new EmailAttachment(filename, mimeType, BinaryData.FromBytes(data)));
        }

        try
        {
            EmailSendOperation emailSendOperation = await _emailClient.SendAsync(WaitUntil.Started, emailMessage, cancellationToken);

            return new ComposedEmailSendResult
            {
                OperationId = emailSendOperation.Id,
                EncodedAttachmentsSize = encodedAttachmentsSize
            };
        }
        catch (RequestFailedException e)
        {
            _logger.LogError(e, "// EmailServiceClient // SendComposedEmail // Failed to send composed email, NotificationId {NotificationId}", email.NotificationId);

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
                _logger.LogWarning("A request failed because the recipient is on the suppression list of Azure Communication Services, OperationId {OperationId}", operationId);
            }
            else
            {
                _logger.LogError(e, "// EmailServiceClient // GetOperationUpdate // Exception thrown when getting status, OperationId {OperationId}", operationId);
            }

            return GetEmailSendResult(e);
        }

        return Core.Status.EmailSendResult.Sending;
    }

    /// <summary>
    /// Determines the appropriate email send result based on the request failed exception from Azure Communication Services.
    /// </summary>
    /// <param name="e">The request failed exception thrown by Azure Communication Services.</param>
    /// <returns>The email send result indicating the type of failure.</returns>
    internal static Core.Status.EmailSendResult GetEmailSendResult(RequestFailedException e)
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
        else if (e.Status == ErrorTypes.PayloadTooLargeStatusCode)
        {
            emailSendResult = Core.Status.EmailSendResult.Failed_PayloadTooLarge;
        }
        else if ((e.Status >= 500 && e.Status < 600) || e.Status == 0)
        {
            // Handle all 5xx errors and status 0 (network/no response) as transient errors
            emailSendResult = Core.Status.EmailSendResult.Failed_TransientError;
        }
        else
        {
            emailSendResult = Core.Status.EmailSendResult.Failed;
        }

        return emailSendResult;
    }

    /// <summary>
    /// Gets the configured delay in seconds for handling unknown/intermittent errors from Azure Communication Services.
    /// This is used as a fallback when Azure Communication Services returns 5xx errors or other errors
    /// where a retry-after value cannot be parsed from the error message.
    /// </summary>
    /// <returns>The configured delay in seconds for intermittent errors.</returns>
    internal int GetUnknownErrorDelay()
    {
        return _emailServiceAdminSettings.IntermittentErrorDelay;
    }

    /// <summary>
    /// Gets the int proceeding the word seconds in the string.
    /// Falls back to the configured intermittent error delay if no delay is found in the message.
    /// </summary>
    /// <param name="message">The message to find delay within</param>
    /// <returns></returns>
    internal int GetDelayFromString(string message)
    {
        var secondsString = Regex.Match(
                message,
                @"(\d+)[^,.\d\n]+?(?=seconds)|(?<=seconds)[^,.\d\n]+?(\d+)",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(10))
                .Value;

        return string.IsNullOrEmpty(secondsString) ? GetUnknownErrorDelay() : int.Parse(secondsString);
    }

    /// <summary>
    /// Downloads a single attachment from a SAS URL with concurrency throttling.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the notification the attachment belongs to.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to perform the download request.</param>
    /// <param name="semaphore">A <see cref="SemaphoreSlim"/> used to limit the number of concurrent downloads.</param>
    /// <param name="attachment">The <see cref="SasFileAttachment"/> containing the SAS URL and file metadata.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A tuple containing the filename, MIME type, and raw byte data of the downloaded attachment.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a network error occurs during the download.</exception>
    /// <exception cref="InvalidSasUrlException">Thrown when the SAS URL returns a non-success HTTP status code.</exception>
    private async Task<(string Filename, string MimeType, byte[] Data)> DownloadAttachmentAsync(Guid notificationId, HttpClient httpClient, SemaphoreSlim semaphore, SasFileAttachment attachment, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            HttpResponseMessage response;

            try
            {
                response = await httpClient.GetAsync(attachment.SasUrl, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new InvalidOperationException(
                    $"Network error downloading attachment '{attachment.Filename}' for notification {notificationId}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "EmailServiceClient received HTTP {StatusCode} for attachment '{Filename}' on NotificationId {NotificationId}. SAS URL may be expired or invalid.",
                    (int)response.StatusCode,
                    attachment.Filename,
                    notificationId);

                throw new InvalidSasUrlException(attachment.Filename, (int)response.StatusCode);
            }

            byte[] data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (attachment.Filename, attachment.MimeType, data);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
