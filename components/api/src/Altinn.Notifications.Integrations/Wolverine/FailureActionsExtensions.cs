using System.Text.Json;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Services.Interfaces;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine.ErrorHandling;
using Wolverine.Runtime;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Extension methods for IFailureActions to support custom failure handling scenarios.
/// </summary>
public static class FailureActionsExtensions
{
    /// <summary>
    /// Configures the failure policy to save a dead delivery report when all retries are exhausted.
    /// </summary>
    public static IAdditionalActions SaveDeadDeliveryReport(this IFailureActions failureActions, string reason)
    {
        return failureActions.CustomAction(
            async (runtime, envelope, exception) =>
            {
                var logger = runtime.LoggerFactory.CreateLogger(typeof(EmailDeliveryReportHandler));
                
                try
                {
                    var eventGridEvent = EventGridEvent.Parse(BinaryData.FromBytes(envelope.Envelope!.Data!));
                    
                    if (eventGridEvent.TryGetSystemEventData(out object systemEvent))
                    {
                        var deliveryReport = (AcsEmailDeliveryReportReceivedEventData)systemEvent;
                        var deadDeliveryReportService = runtime.Services.GetRequiredService<IDeadDeliveryReportService>();
                        
                        var deadDeliveryReport = new DeadDeliveryReport
                        {
                            Channel = Core.Enums.DeliveryReportChannel.AzureCommunicationServices,
                            FirstSeen = envelope.Envelope.SentAt.UtcDateTime,
                            LastAttempt = DateTime.UtcNow,
                            AttemptCount = envelope.Envelope.Attempts,
                            Resolved = false,
                            DeliveryReport = JsonSerializer.Serialize(deliveryReport),
                            Reason = reason,
                            Message = exception.Message
                        };

                        await deadDeliveryReportService.InsertAsync(deadDeliveryReport);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to save dead delivery report");
                }
            },
            "Save Dead Delivery Report");
    }
}
