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
        ArgumentNullException.ThrowIfNull(report);

        if (string.IsNullOrWhiteSpace(report.DeliveryReport))
        {
            throw new ArgumentException(
                "DeliveryReport cannot be null or empty",
                nameof(report));
        }

        if (report.AttemptCount <= 0)
        {
            throw new ArgumentException(
                "AttemptCount must be greater than zero",
                nameof(report));
        }

        if (report.LastAttempt < report.FirstSeen)
        {
            throw new ArgumentException(
                "LastAttempt must be greater than or equal to FirstSeen",
                nameof(report));
        }

        return _reportRepository.InsertAsync(report, cancellationToken);
    }
}
