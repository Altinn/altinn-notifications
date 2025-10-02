using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <inheritdoc/>
public class DeadDeliveryReportService(IDeadDeliveryReportRepository reportRepository) : IDeadDeliveryReportService
{
    private readonly IDeadDeliveryReportRepository _reportRepository = reportRepository;

    /// <inheritdoc/>
    public Task<long> Insert(DeadDeliveryReport report, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(report.DeliveryReport))
        {
            throw new ArgumentException("Report cannot be null or empty", nameof(report));
        }

        return _reportRepository.Insert(report, cancellationToken);
    }
}
