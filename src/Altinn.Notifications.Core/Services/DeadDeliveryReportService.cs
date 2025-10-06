using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <inheritdoc/>
public class DeadDeliveryReportService(IDeadDeliveryReportRepository reportRepository) : IDeadDeliveryReportService
{
    private readonly IDeadDeliveryReportRepository _reportRepository = reportRepository;

    /// <inheritdoc/>
    public Task<long> InsertAsync(DeadDeliveryReport report, CancellationToken cancellationToken = default)
    {
        if (report is null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (string.IsNullOrWhiteSpace(report.DeliveryReport))
        {
            throw new ArgumentException(
                "DeliveryReport cannot be null or empty",
                nameof(DeadDeliveryReport.DeliveryReport));
        }

        return _reportRepository.InsertAsync(report, cancellationToken);
    }
}
