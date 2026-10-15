using System.Diagnostics;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Shared;

using Wolverine;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
/// Represents an implementation of <see cref="ISmsClient"/> that sends text messages using LinkMobility's SMS gateway.
/// </summary>
public class MockSmsClient(HttpClient client /*, SmsDeliveryReportSettings smsDeliveryReportSettings */) : ISmsClient
{
    private const int SmsSendExecutionTimeMs = 70;
    private const int SmsDeliveryReportDelayTimeMs = 2000;

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
        client.BaseAddress = new Uri("https://localhost/notifications/sms/api/v1/reports");
        await Task.Delay(SmsSendExecutionTimeMs);
        string gatewayReference = Guid.NewGuid().ToString();
        string? traceParent = Activity.Current?.Id;
        string? traceState = Activity.Current?.TraceStateString;
        _ = Task.Run(async () =>
        {
            await Task.Delay(SmsDeliveryReportDelayTimeMs);
            await client.PostAsync(client.BaseAddress, new StringContent($"{{\"gatewayReference\":\"{gatewayReference}\",\"notificationId\":\"{sms.NotificationId}\"}}"));
        });

        return gatewayReference;
    }
}

/// <summary>
/// Mock of SMS delivery report publisher
/// </summary>
public class MockSmsDeliveryReportPublisher(IServiceProvider serviceProvider) : Shared.Publishers.WolverinePublisher(serviceProvider)
{
    /// <summary>
    /// Dispatches a mock SMS delivery report event to the message bus.
    /// </summary>
    /// <returns></returns>
    public async Task DispatchAsync(string gatewayReference, Guid notificationId, string? traceParent = null, string? traceState = null)
    {
        var command = new SmsDeliveryReportCommand
        {
            NotificationId = notificationId,
            GatewayReference = gatewayReference,
            SendResult = "Delivered",
            DeliveryReport = "Mock delivery report"
        };

        if (string.IsNullOrWhiteSpace(traceParent) && string.IsNullOrWhiteSpace(traceState))
        {
            await PublishCommandAsync(command);
            return;
        }

        var deliveryOptions = new DeliveryOptions();

        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            deliveryOptions.Headers["Diagnostic-Id"] = traceParent;
            deliveryOptions.Headers["traceparent"] = traceParent;
        }

        if (!string.IsNullOrWhiteSpace(traceState))
        {
            deliveryOptions.Headers["tracestate"] = traceState;
        }

        await PublishCommandAsync(command, deliveryOptions);
    }
}
