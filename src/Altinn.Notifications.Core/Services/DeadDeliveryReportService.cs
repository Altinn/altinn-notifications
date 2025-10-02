using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <inheritdoc/>
public class DeadDeliveryReportService(IDeadDeliveryReportRepository reportRepository) : IDeadDeliveryReportService
{
    private readonly IDeadDeliveryReportRepository _reportRepository = reportRepository;

    /// <inheritdoc/>
    public Task Add(string report, DeliveryReportChannel channel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(report))
        {
            throw new ArgumentException("Report cannot be null or empty", nameof(report));
        }

        var now = DateTime.UtcNow;
        var deadDeliveryReport = new DeadDeliveryReport
        {
            FirstSeen = now,
            LastAttempt = now,
            DeliveryReport = report,
            Channel = channel,
        };

        return _reportRepository.Add(deadDeliveryReport, cancellationToken);
    }
}
