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
                var innerEnvelope = envelope.Envelope
                    ?? throw new InvalidDataException("Wolverine envelope is null.");

                var message = innerEnvelope.Message
                    ?? throw new InvalidDataException("Envelope message is null.");

                string payload = channel switch
                {
                    DeliveryReportChannel.AzureCommunicationServices => ExtractEmailPayload(message),
                    DeliveryReportChannel.LinkMobility => ExtractSmsPayload(message),
                    _ => throw new NotSupportedException($"No payload extractor registered for channel {channel}")
                };

                var deadDeliveryReportService = runtime.Services.GetRequiredService<IDeadDeliveryReportService>();

                var deadDeliveryReport = new DeadDeliveryReport
                {
                    Channel = channel,
                    FirstSeen = innerEnvelope.SentAt.UtcDateTime,
                    LastAttempt = DateTime.UtcNow,
                    AttemptCount = Math.Max(1, innerEnvelope.Attempts),
                    Resolved = false,
                    DeliveryReport = payload,
                    Reason = reason,
                    Message = exception.Message
                };

                await deadDeliveryReportService.InsertAsync(deadDeliveryReport);
            },
            "Save Dead Delivery Report");
    }

    /// <summary>
    /// Extracts and serializes the email payload from a Wolverine message.
    /// Supports both <see cref="EmailDeliveryReportCommand"/> (Event Grid reports) and
    /// <see cref="EmailSendResultCommand"/> (email service polling results).
    /// </summary>
    /// <param name="message">The raw Wolverine message object.</param>
    /// <returns>A JSON-serialized string of the underlying payload.</returns>
    /// <exception cref="InvalidDataException">Thrown when the message type is unrecognized or the event data cannot be extracted.</exception>
    private static string ExtractEmailPayload(object message)
    {
        if (message is EmailDeliveryReportCommand reportCommand)
        {
            var eventGridEvent = EventGridEvent.Parse(reportCommand.Message.Body);

            if (eventGridEvent.TryGetSystemEventData(out object systemEvent)
                && systemEvent is AcsEmailDeliveryReportReceivedEventData deliveryReport)
            {
                return JsonSerializer.Serialize(deliveryReport);
            }

            throw new InvalidDataException($"Failed to extract email delivery report payload; unrecognized event type '{eventGridEvent.EventType}'.");
        }

        if (message is EmailSendResultCommand sendResultCommand)
        {
            return JsonSerializer.Serialize(sendResultCommand);
        }

        throw new InvalidDataException($"Expected {nameof(EmailDeliveryReportCommand)} or {nameof(EmailSendResultCommand)}, got {message.GetType().Name}.");
    }

    /// <summary>
    /// Serializes the SMS delivery report payload from a Wolverine message.
    /// </summary>
    /// <param name="message">The raw Wolverine message object, expected to be an <see cref="SmsDeliveryReportCommand"/>.</param>
    /// <returns>A JSON-serialized string of the <see cref="SmsDeliveryReportCommand"/>.</returns>
    private static string ExtractSmsPayload(object message)
    {
        if (message is not SmsDeliveryReportCommand command)
        {
            throw new InvalidDataException($"Expected {nameof(SmsDeliveryReportCommand)}, got {message.GetType().Name}.");
        }

        return JsonSerializer.Serialize(command);
    }
}
