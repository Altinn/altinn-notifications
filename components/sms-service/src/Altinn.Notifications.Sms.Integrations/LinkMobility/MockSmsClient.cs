using System.Net.Http.Headers;
using System.Text;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
/// Represents an implementation of <see cref="ISmsClient"/> that sends text messages using LinkMobility's SMS gateway.
/// </summary>
public class MockSmsClient(HttpClient client, IConfiguration configuration, ILogger<MockSmsClient> logger) : ISmsClient
{
    private const int SmsSendExecutionTimeMs = 70;
    private const int SmsDeliveryReportDelayTimeMs = 2000;
    private readonly string _bodyTemplate = """<?xml version="1.0"?><!DOCTYPE MSGLST SYSTEM "pswincom_report_request.dtd"><MSGLST><MSG><ID>1</ID><REF>gwref</REF><RCV>recipient</RCV><STATE>DELIVRD</STATE><DELIVERYTIME>timestamp</DELIVERYTIME></MSG></MSGLST>""";

    /// <inheritdoc />
    public async Task<Result<string, SmsClientErrorResponse>> SendAsync(Core.Sending.Sms sms)
    {
        return await SendToLinkMobilityAsync(sms);
    }

    /// <inheritdoc />
    public async Task<Result<string, SmsClientErrorResponse>> SendAsync(Core.Sending.Sms sms, int timeToLiveInSeconds)
    {
        return await SendToLinkMobilityAsync(sms);
    }

    /// <summary>
    /// Sends the SMS message using the LinkMobility gateway and handles the result.
    /// </summary>
    /// <returns>
    /// A <see cref="Result{T, TError}"/> that represents the outcome of the send operation:
    /// <list type="table">
    ///   <item>On success: a unique string identifier for tracking the message.</item>
    ///   <item>On failure: a <see cref="SmsClientErrorResponse"/> providing details about the error.</item>
    /// </list>
    /// </returns>
    private async Task<Result<string, SmsClientErrorResponse>> SendToLinkMobilityAsync(Core.Sending.Sms sms)
    {
        string gatewayReference = Guid.NewGuid().ToString();
        string url = configuration["SmsGatewaySettings:MockDeliveryEndpointBaseUrl"] + "/notifications/sms/api/v1/reports";
        var bodyTemplate = _bodyTemplate
            .Replace("gwref", gatewayReference)
            .Replace("timestamp", DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"))
            .Replace("recipient", sms.Recipient);
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyTemplate, Encoding.UTF8, "application/xml")
        };

        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{configuration["SmsGatewaySettings:Username"]}:{configuration["SmsGatewaySettings:Password"]}")));

        await Task.Delay(SmsSendExecutionTimeMs);

        _ = Task.Run(async () =>
        {
            await Task.Delay(SmsDeliveryReportDelayTimeMs);
            try
            {
                var result = await client.SendAsync(httpRequestMessage);
                if (!result.IsSuccessStatusCode)
                {
                    logger.LogError("Failed to send delivery report. Status code: {StatusCode}, Url: {Url}", result.StatusCode, url);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending delivery report to url: {Url}", url);
            }
        });

        return gatewayReference;
    }
}
