using System.Diagnostics.Tracing;

namespace Altinn.Notifications
{
    /// <summary>
    /// Event listener for Azure Monitor exporter events from OpenTelemetry.
    /// </summary>
    public sealed class AzureMonitorExporterListener : EventListener
    {
        private readonly ILogger<AzureMonitorExporterListener> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureMonitorExporterListener"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public AzureMonitorExporterListener(
            ILogger<AzureMonitorExporterListener> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Enables verbose event monitoring for the OpenTelemetry Azure Monitor exporter event source.
        /// </summary>
        /// <param name="eventSource">The event source being created.</param>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "OpenTelemetry-AzureMonitor-Exporter")
            {
                EnableEvents(
                    eventSource,
                    EventLevel.Verbose,
                    EventKeywords.All);
            }
        }

        /// <summary>
        /// Logs information about events written by the Azure Monitor exporter.
        /// </summary>
        /// <param name="eventData">The event data that was written.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            _logger.LogInformation(
                "[AzureMonitorExporter] {Event}: {Payload}",
                eventData.EventName,
                string.Join(", ", eventData.Payload ?? []));
        }
    }
}
