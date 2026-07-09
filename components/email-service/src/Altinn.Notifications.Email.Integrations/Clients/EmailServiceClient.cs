using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Exceptions;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Clients.AzureCommunicationServices;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailServiceAdminSettings _emailServiceAdminSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailServiceClient"/> class.
    /// </summary>
    public EmailServiceClient(
        CommunicationServicesSettings communicationServicesSettings,
        EmailServiceAdminSettings emailServiceAdminSettings,
        IHttpClientFactory httpClientFactory,
        ILogger<EmailServiceClient> logger)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _emailServiceAdminSettings = emailServiceAdminSettings;

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
        EmailMessage emailMessage = BuildEmailMessage(email);

        long encodedAttachmentsSize = await DownloadAttachmentsAsync(email, emailMessage, cancellationToken);

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

            EmailClientErrorResponse errorResponse = new()
            {
                SendResult = GetEmailSendResult(e),
                EncodedAttachmentsSize = encodedAttachmentsSize > 0 ? encodedAttachmentsSize : null
            };

            if (errorResponse.SendResult == Core.Status.EmailSendResult.Failed_TransientError)
            {
                errorResponse.IntermittentErrorDelay = GetDelayFromString(e.Message);
            }

            return errorResponse;
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
    /// Builds an <see cref="EmailMessage"/> from the given <see cref="ComposedEmail"/>, populating subject, body, and recipients.
    /// </summary>
    /// <param name="email">The composed email to build the message from.</param>
    /// <returns>A fully constructed <see cref="EmailMessage"/> ready for attachment and sending.</returns>
    private static EmailMessage BuildEmailMessage(ComposedEmail email)
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

        return new EmailMessage(email.FromAddress, email.ToAddress, emailContent);
    }

    /// <summary>
    /// Downloads all attachments from their SAS URLs concurrently and adds them to the <see cref="EmailMessage"/>.
    /// </summary>
    /// <param name="email">The composed email containing the attachments to download.</param>
    /// <param name="emailMessage">The message to attach the downloaded files to.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The total Base64-encoded size in bytes of all downloaded attachments, or <c>0</c> if there are none.</returns>
    /// <exception cref="InvalidSasUrlException">Thrown when a SAS URL returns a permanent HTTP 4xx response (excluding 429 and 408).</exception>
    /// <exception cref="AttachmentDownloadException">Thrown when a network error or transient HTTP response (5xx, 429, 408) occurs during the download.</exception>
    private async Task<long> DownloadAttachmentsAsync(ComposedEmail email, EmailMessage emailMessage, CancellationToken cancellationToken)
    {
        if (email.Attachments.Count == 0)
        {
            return 0;
        }

        Exception? downloadException = null;

        using var httpClient = _httpClientFactory.CreateClient(nameof(EmailServiceClient));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var semaphore = new SemaphoreSlim(_emailServiceAdminSettings.BlobDownloadConcurrency);

        var downloadTasks = email.Attachments
            .Select(async attachment =>
            {
                try
                {
                    return await DownloadAttachmentAsync(email.NotificationId, httpClient, semaphore, attachment, linkedCts.Token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.CompareExchange(ref downloadException, ex, null);

                    await linkedCts.CancelAsync();

                    throw;
                }
                catch (OperationCanceledException)
                {
                    await linkedCts.CancelAsync();

                    throw;
                }
            });

        (SasFileAttachment Metadata, byte[] Data)[] downloaded;

        try
        {
            downloaded = await Task.WhenAll(downloadTasks);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            if (downloadException != null)
            {
                ExceptionDispatchInfo.Capture(downloadException).Throw();
            }

            throw;
        }

        long encodedAttachmentsSize = 0;

        foreach (var (metadata, data) in downloaded)
        {
            encodedAttachmentsSize += ((long)data.Length + 2) / 3 * 4;
            emailMessage.Attachments.Add(new EmailAttachment(metadata.Filename, metadata.MimeType, BinaryData.FromBytes(data)));
        }

        return encodedAttachmentsSize;
    }

    /// <summary>
    /// Downloads a single attachment from a SAS URL with concurrency throttling.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the notification the attachment belongs to.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to perform the download request.</param>
    /// <param name="semaphore">A <see cref="SemaphoreSlim"/> used to limit the number of concurrent downloads per email.</param>
    /// <param name="attachment">The <see cref="SasFileAttachment"/> containing the SAS URL and file metadata.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The original <see cref="SasFileAttachment"/> paired with its downloaded byte data.</returns>
    /// <exception cref="AttachmentDownloadException">Thrown when a network error or transient HTTP response (5xx, 429, 408) occurs during the download.</exception>
    /// <exception cref="InvalidSasUrlException">Thrown when the SAS URL returns a permanent HTTP 4xx response (excluding 429 and 408).</exception>
    private static async Task<(SasFileAttachment Metadata, byte[] Data)> DownloadAttachmentAsync(Guid notificationId, HttpClient httpClient, SemaphoreSlim semaphore, SasFileAttachment attachment, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(attachment.SasUrl, cancellationToken);

                response.EnsureSuccessStatusCode();

                byte[] data = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                return (attachment, data);
            }
            catch (HttpRequestException ex) when ((int?)ex.StatusCode >= 500 || ex.StatusCode == HttpStatusCode.TooManyRequests || ex.StatusCode == HttpStatusCode.RequestTimeout)
            {
                throw new AttachmentDownloadException(attachment.Filename, notificationId, (int)ex.StatusCode.Value, ex);
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                throw new InvalidSasUrlException(attachment.Filename, (int)ex.StatusCode.Value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new AttachmentDownloadException(attachment.Filename, notificationId, ex);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
}
