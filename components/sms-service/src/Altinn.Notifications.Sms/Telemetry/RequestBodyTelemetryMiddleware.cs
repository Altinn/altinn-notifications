using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Altinn.Notifications.Sms.Telemetry;

/// <summary>
/// Middleware that extracts delivery report data from Link Mobility SMS delivery report XML
/// in POST request bodies and adds them as tags to OpenTelemetry Activity for Application Insights tracking.
/// </summary>
/// <param name="next">The next middleware delegate in the request pipeline.</param>
public class RequestBodyTelemetryMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;
    private const string DeliveryReportPath = "/notifications/sms/api/v1/reports";
    private const string ReceiverFieldName = "RCV";

    /// <summary>
    /// Processes the HTTP request to extract and log SMS delivery report telemetry data.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <remarks>
    /// This method reads the request body for POST requests to the delivery report endpoint,
    /// parses the XML to extract the message ID and reference, and adds them as tags to the
    /// current OpenTelemetry Activity. The request body is buffered and reset so downstream
    /// middleware and controllers can still read it.
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == HttpMethods.Post && context.Request.Path.StartsWithSegments(DeliveryReportPath))
        {
            var body = await ReadRequestBodyAsync(context);
            
            if (!string.IsNullOrWhiteSpace(body))
            {
                ProcessDeliveryReportTelemetry(body);
            }
        }

        await _next(context);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpContext context)
    {
        const int MaxBodySize = 1024 * 1024; // 1 MB limit
        
        context.Request.EnableBuffering();

        if (context.Request.ContentLength > MaxBodySize)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(
            context.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        return body;
    }

    private static void ProcessDeliveryReportTelemetry(string body)
    {
        try
        {
            var deliveryData = ParseDeliveryReportXml(body);
            if (deliveryData != null)
            {
                AddTelemetryTag(deliveryData);
            }
        }
        catch (Exception)
        {
            // Silently ignore parsing errors - the controller will handle validation
        }
    }

    private static Dictionary<string, string>? ParseDeliveryReportXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        var msgElement = doc.Root?.Element("MSG");
        
        if (msgElement == null)
        {
            return null;
        }

        var deliveryData = new Dictionary<string, string>();
        foreach (var element in from element in msgElement.Elements()
                                where !string.IsNullOrEmpty(element.Value)
                                select element)
        {
            var value = element.Name.LocalName == ReceiverFieldName 
                ? MaskPhoneNumber(element.Value) 
                : element.Value;
            
            deliveryData[element.Name.LocalName] = value;
        }

        return deliveryData.Count > 0 ? deliveryData : null;
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 6)
        {
            return "******";
        }

        return "******" + phoneNumber[6..];
    }

    private static void AddTelemetryTag(Dictionary<string, string> deliveryData)
    {
        var activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        activity.SetTag("DeliveryReports", JsonSerializer.Serialize(deliveryData));
    }
}
