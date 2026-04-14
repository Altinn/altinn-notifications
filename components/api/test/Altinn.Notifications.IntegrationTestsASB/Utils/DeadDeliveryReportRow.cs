using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.IntegrationTestsASB.Utils;

/// <summary>
/// Represents a row from the deaddeliveryreports table, used for test assertions.
/// </summary>
public record DeadDeliveryReportRow(
    long Id,
    DeliveryReportChannel Channel,
    string? Reason,
    int AttemptCount,
    bool Resolved);
