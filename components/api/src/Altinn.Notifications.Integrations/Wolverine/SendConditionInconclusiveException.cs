namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Thrown by <see cref="Handlers.ProcessPastDueOrderHandler"/> when a send condition check is inconclusive
/// on the first processing attempt, signalling Wolverine to schedule a retry.
/// On the retry attempt <see cref="Core.Services.OrderProcessingService.ProcessOrderRetry"/> is called,
/// which proceeds regardless of the condition result.
/// </summary>
internal sealed class SendConditionInconclusiveException(string message) : Exception(message);
