using System.Diagnostics.Metrics;

using Altinn.Notifications.Core.Integrations;

namespace Altinn.Notifications.Integrations.Telemetry;

/// <summary>
/// Provides custom OpenTelemetry backlog metrics for notifications and orders.
/// </summary>
public sealed class BacklogMetrics : IBacklogMetrics, IDisposable
{
    /// <summary>
    /// The name of the OpenTelemetry meter. Must be registered via <c>AddMeter</c>
    /// in <c>Program.cs</c> so measurements are exported to Azure Monitor.
    /// </summary>
    public const string MeterName = "Altinn.Notifications.Backlog";

    /// <summary>
    /// The metric name for pending anytime email notifications.
    /// </summary>
    public const string EmailNotificationsAnytimeGaugeName = "altinn.notifications.email.notifications.pending.anytime";

    /// <summary>
    /// The metric name for pending daytime email notifications.
    /// </summary>
    public const string EmailNotificationsDaytimeGaugeName = "altinn.notifications.email.notifications.pending.daytime";

    /// <summary>
    /// The metric name for total pending email notifications.
    /// </summary>
    public const string EmailNotificationsTotalGaugeName = "altinn.notifications.email.notifications.pending.total";

    /// <summary>
    /// The metric name for pending anytime SMS notifications.
    /// </summary>
    public const string SmsNotificationsAnytimeGaugeName = "altinn.notifications.sms.notifications.pending.anytime";

    /// <summary>
    /// The metric name for pending daytime SMS notifications.
    /// </summary>
    public const string SmsNotificationsDaytimeGaugeName = "altinn.notifications.sms.notifications.pending.daytime";

    /// <summary>
    /// The metric name for total pending SMS notifications.
    /// </summary>
    public const string SmsNotificationsTotalGaugeName = "altinn.notifications.sms.notifications.pending.total";

    /// <summary>
    /// The metric name for orders not due.
    /// </summary>
    public const string OrdersNotDueGaugeName = "altinn.notifications.orders.not.due";

    /// <summary>
    /// The metric name for orders past due.
    /// </summary>
    public const string OrdersPastDueGaugeName = "altinn.notifications.orders.past.due";

    /// <summary>
    /// Name of the gauge metric that reports the number of failed notification orders.
    /// </summary>
    public const string OrdersFailedGaugeName = "altinn.notifications.orders.failed";

    /// <summary>
    /// Key for metrics tag
    /// </summary>
    public const string TagKey = "channel";

    private readonly Meter _meter;
    private readonly Gauge<long> _emailNotificationsAnytimeGauge;
    private readonly Gauge<long> _emailNotificationsDaytimeGauge;
    private readonly Gauge<long> _emailNotificationsTotalGauge;
    private readonly Gauge<long> _smsNotificationsAnytimeGauge;
    private readonly Gauge<long> _smsNotificationsDaytimeGauge;
    private readonly Gauge<long> _smsNotificationsTotalGauge;
    private readonly Gauge<long> _ordersNotDueGauge;
    private readonly Gauge<long> _ordersPastDueGauge;
    private readonly Gauge<long> _ordersFailedGauge;

    /// <summary>
    /// Initializes a new instance of the <see cref="BacklogMetrics"/> class.
    /// </summary>
    public BacklogMetrics()
    {
        _meter = new Meter(MeterName);
        _emailNotificationsAnytimeGauge = _meter.CreateGauge<long>(EmailNotificationsAnytimeGaugeName, description: "Current number of pending anytime email notifications.");
        _emailNotificationsDaytimeGauge = _meter.CreateGauge<long>(EmailNotificationsDaytimeGaugeName, description: "Current number of pending daytime email notifications.");
        _emailNotificationsTotalGauge = _meter.CreateGauge<long>(EmailNotificationsTotalGaugeName, description: "Current total number of pending email notifications.");
        _smsNotificationsAnytimeGauge = _meter.CreateGauge<long>(SmsNotificationsAnytimeGaugeName, description: "Current number of pending anytime SMS notifications.");
        _smsNotificationsDaytimeGauge = _meter.CreateGauge<long>(SmsNotificationsDaytimeGaugeName, description: "Current number of pending daytime SMS notifications.");
        _smsNotificationsTotalGauge = _meter.CreateGauge<long>(SmsNotificationsTotalGaugeName, description: "Current total number of pending SMS notifications.");
        _ordersNotDueGauge = _meter.CreateGauge<long>(OrdersNotDueGaugeName, description: "Current number of orders not due for processing.");
        _ordersPastDueGauge = _meter.CreateGauge<long>(OrdersPastDueGaugeName, description: "Current number of orders past due for processing.");
        _ordersFailedGauge = _meter.CreateGauge<long>(OrdersFailedGaugeName, description: "Current number of orders failed for processing.");
    }

    /// <inheritdoc/>
    public void RecordBacklogMetrics(
        long emailNotificationsAnytime,
        long emailNotificationsDaytime,
        long emailNotificationsComposed,
        long smsNotificationsAnytime,
        long smsNotificationsDaytime,
        long ordersNotDue,
        long ordersPastDue,
        long ordersFailed)
    {
        long emailNotificationsTotal = emailNotificationsAnytime + emailNotificationsDaytime + emailNotificationsComposed;
        long smsNotificationsTotal = smsNotificationsAnytime + smsNotificationsDaytime;

        _emailNotificationsAnytimeGauge.Record(emailNotificationsAnytime, new KeyValuePair<string, object?>(TagKey, "email"));
        _emailNotificationsDaytimeGauge.Record(emailNotificationsDaytime, new KeyValuePair<string, object?>(TagKey, "email"));
        _emailNotificationsTotalGauge.Record(emailNotificationsTotal, new KeyValuePair<string, object?>(TagKey, "email"));

        _smsNotificationsAnytimeGauge.Record(smsNotificationsAnytime, new KeyValuePair<string, object?>(TagKey, "sms"));
        _smsNotificationsDaytimeGauge.Record(smsNotificationsDaytime, new KeyValuePair<string, object?>(TagKey, "sms"));
        _smsNotificationsTotalGauge.Record(smsNotificationsTotal, new KeyValuePair<string, object?>(TagKey, "sms"));

        _ordersNotDueGauge.Record(ordersNotDue, new KeyValuePair<string, object?>(TagKey, "order"));
        _ordersPastDueGauge.Record(ordersPastDue, new KeyValuePair<string, object?>(TagKey, "order"));
        _ordersFailedGauge.Record(ordersFailed, new KeyValuePair<string, object?>(TagKey, "order"));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _meter.Dispose();
    }
}
