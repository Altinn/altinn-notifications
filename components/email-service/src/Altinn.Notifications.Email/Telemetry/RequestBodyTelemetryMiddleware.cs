using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Altinn.Notifications.Email.Configuration;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Mappers;

using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Email.Telemetry;

/// <summary>
/// Middleware that extracts send operation results from EventGrid email delivery report events
/// in POST request bodies and adds them as tags to OpenTelemetry Activity for Application Insights tracking.
/// </summary>
/// <param name="next">The next middleware delegate in the request pipeline.</param>
/// <param name="emailDeliveryReportSettings">Configuration settings for email delivery reports.</param>
public class RequestBodyTelemetryMiddleware(
    RequestDelegate next,
    IOptions<EmailDeliveryReportSettings> emailDeliveryReportSettings)
{
    private readonly RequestDelegate _next = next;
    private readonly EmailDeliveryReportSettings _settings = emailDeliveryReportSettings.Value;

    /// <summary>
    /// Invokes the middleware to extract send operation results from EventGrid email delivery report events.
    /// The extracted data is added as tags to the current OpenTelemetry Activity, which will appear in 
    /// Application Insights customDimensions.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Check if it's a POST request
        if (context.Request.Method != HttpMethods.Post)
        {
            await _next(context);
            return;
        }

        // Allow the body to be read multiple times (rewindable)
        context.Request.EnableBuffering();
        string body;

        // Leave the body stream open after reading
        using (var reader = new StreamReader(
            context.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
        }

        // Reset the stream's position to 0 so the next middleware/controller can read it
        context.Request.Body.Position = 0;

        var activity = Activity.Current;
        if (activity == null)
        {
            await _next(context);
            return;
        }

        if (string.Equals(_settings.ParseObject, "deliveryreport", StringComparison.OrdinalIgnoreCase))
        {
            var deliveryReports = ExtractDeliveryReports(body);
            ApplyDeliveryReportsToCustomDimensions(deliveryReports, activity);
        }
        else if (string.Equals(_settings.ParseObject, "sendoperationresults", StringComparison.OrdinalIgnoreCase))
        {
            var sendOperationResults = ExtractSendOperationResults(body);
            ApplyOperationResultsToCustomDimensions(sendOperationResults, activity);
        }

        // Continue to the next middleware in the pipeline
        await _next(context);
    }

    /// <summary>
    /// Masks an email address by keeping only the first two characters of the local part
    /// and the entire domain, replacing the rest with asterisks.
    /// </summary>
    /// <param name="email">The email address to mask.</param>
    /// <returns>The masked email address, or the original string if it's not a valid email format.</returns>
    private static string MaskEmailAddress(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1)
        {
            return email; // Not a valid email format, return as-is
        }

        var localPart = email[..atIndex];
        var domain = email[(atIndex + 1)..];

        if (localPart.Length <= 2)
        {
            return $"***@{domain}";
        }

        var maskedLocalPart = localPart[..2] + "***";
        return $"{maskedLocalPart}@{domain}";
    }

    private static void ApplyDeliveryReportsToCustomDimensions(List<AcsEmailDeliveryReportReceivedEventData> deliveryReports, Activity activity)
    {
        if (deliveryReports.Count == 0)
        {
            return;
        }

        // Create masked versions for telemetry without mutating original objects
        var maskedReports = deliveryReports.Select(report => new
        {
            report.MessageId,
            report.InternetMessageId,
            Recipient = !string.IsNullOrWhiteSpace(report.Recipient) ? MaskEmailAddress(report.Recipient) : report.Recipient,
            Sender = !string.IsNullOrWhiteSpace(report.Sender) ? MaskEmailAddress(report.Sender) : report.Sender,
            Status = report.Status?.ToString(),
            DetailsStatusMessage = report.DeliveryStatusDetails?.StatusMessage,
            DetailsRecipientMailServerHostName = report.DeliveryStatusDetails?.RecipientMailServerHostName,
            report.DeliveryAttemptTimestamp
        }).ToList();

        activity.SetTag("DeliveryReports", JsonSerializer.Serialize(maskedReports));
    }

    private static void ApplyOperationResultsToCustomDimensions(List<SendOperationResult> sendOperationResults, Activity activity)
    {
        if (sendOperationResults.Count == 0)
        {
            return;
        }

        // Add send operation results as a custom tag - will appear in Application Insights customDimensions
        activity.SetTag("SendOperationResults", JsonSerializer.Serialize(sendOperationResults));
    }

    private static List<AcsEmailDeliveryReportReceivedEventData> ExtractDeliveryReports(string body)
    {
        var deliveryReports = new List<AcsEmailDeliveryReportReceivedEventData>();
        
        if (string.IsNullOrWhiteSpace(body))
        {
            return deliveryReports;
        }

        try
        {
            // Use EventGridEvent.ParseMany to properly deserialize with BinaryData support
            var eventList = EventGridEvent.ParseMany(BinaryData.FromString(body));
            if (eventList == null)
            {
                return deliveryReports;
            }

            foreach (EventGridEvent eventGridEvent in eventList)
            {
                // If the event is a system event, TryGetSystemEventData will return the deserialized system event
                if (eventGridEvent.TryGetSystemEventData(out object systemEvent) 
                    && systemEvent is AcsEmailDeliveryReportReceivedEventData deliveryReport)
                {
                    deliveryReports.Add(deliveryReport);
                }
            }
        }
        catch (Exception)
        {
            // Not a valid EventGrid event array or parsing failed, return empty list
        }

        return deliveryReports;
    }

    /// <summary>
    /// Extracts send operation results from EventGrid events containing AcsEmailDeliveryReportReceivedEventData.
    /// Each result contains the operation ID (message ID) and the parsed email send result (delivery status).
    /// </summary>
    /// <param name="body">The request body as a string containing EventGrid events in JSON format.</param>
    /// <returns>A list of <see cref="SendOperationResult"/> objects extracted from email delivery report events.</returns>
    private static List<SendOperationResult> ExtractSendOperationResults(string body)
    {
        var sendOperationResults = new List<SendOperationResult>();

        if (string.IsNullOrWhiteSpace(body))
        {
            return sendOperationResults;
        }

        try
        {
            // Use EventGridEvent.ParseMany to properly deserialize with BinaryData support
            var eventList = EventGridEvent.ParseMany(BinaryData.FromString(body));
            if (eventList == null)
            {
                return sendOperationResults;
            }

            foreach (EventGridEvent eventGridEvent in eventList)
            {
                // If the event is a system event, TryGetSystemEventData will return the deserialized system event
                if (eventGridEvent.TryGetSystemEventData(out object systemEvent) 
                    && systemEvent is AcsEmailDeliveryReportReceivedEventData deliveryReport)
                {
                    try
                    {
                        sendOperationResults.Add(new SendOperationResult 
                        {
                            OperationId = deliveryReport.MessageId,
                            SendResult = EmailSendResultMapper.ParseDeliveryStatus(deliveryReport.Status?.ToString())
                        });
                    }
                    catch (ArgumentException)
                    {
                        // skip unknown delivery status values
                    }
                }
            }
        }
        catch (Exception)
        {
            // Not a valid EventGrid event array or parsing failed, return empty list
        }

        return sendOperationResults;
    }
}
