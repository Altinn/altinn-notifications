using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Shared.Commands;

using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.ErrorHandling;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Extension methods for IFailureActions to support custom failure handling scenarios.
/// </summary>
public static class FailureActionsExtensions
{
    /// <summary>
    /// Configures the failure policy to save a dead delivery report when all retries are exhausted.
    /// </summary>
    /// <param name="failureActions">The failure actions to extend.</param>
    /// <param name="reason">Reason code stored on the dead delivery report.</param>
    /// <param name="channel">The delivery channel; determines how the report payload is extracted.</param>
    public static IAdditionalActions SaveDeadDeliveryReport(
        this IFailureActions failureActions,
        string reason,
        DeliveryReportChannel channel)
    {
        return failureActions.CustomAction(
            async (runtime, envelope, exception) =>
            {
                string? payload = channel switch
                {
                    DeliveryReportChannel.AzureCommunicationServices => ExtractEmailPayload(envelope.Envelope!.Message!),
                    DeliveryReportChannel.LinkMobility => ExtractSmsPayload(envelope.Envelope!.Message!),
                    _ => throw new InvalidOperationException($"No payload extractor registered for channel {channel}")
                };

                if (payload is null)
                {
                    return;
                }

                var deadDeliveryReportService = runtime.Services.GetRequiredService<IDeadDeliveryReportService>();

                var deadDeliveryReport = new DeadDeliveryReport
                {
                    Channel = channel,
                    FirstSeen = envelope.Envelope.SentAt.UtcDateTime,
                    LastAttempt = DateTime.UtcNow,
                    AttemptCount = Math.Max(1, envelope.Envelope.Attempts),
                    Resolved = false,
                    DeliveryReport = payload,
                    Reason = reason,
                    Message = exception.Message
                };

                await deadDeliveryReportService.InsertAsync(deadDeliveryReport);
            },
            "Save Dead Delivery Report");
    }

    private static string? ExtractEmailPayload(object message)
    {
        var command = (EmailDeliveryReportCommand)message;
        var eventGridEvent = EventGridEvent.Parse(command.Message.Body);

        if (eventGridEvent.TryGetSystemEventData(out object systemEvent)
            && systemEvent is AcsEmailDeliveryReportReceivedEventData deliveryReport)
        {
            return JsonSerializer.Serialize(deliveryReport);
        }

        return null;
    }

    private static string ExtractSmsPayload(object message)
        => JsonSerializer.Serialize((SmsDeliveryReportCommand)message);
}
