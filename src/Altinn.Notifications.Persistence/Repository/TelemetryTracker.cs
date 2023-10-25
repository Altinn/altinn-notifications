using System.Diagnostics;

using Microsoft.ApplicationInsights;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository
{
    /// <summary>
    /// Helper to track application insights dependencies for PostgreSQL invocations
    /// </summary>
    public class TelemetryTracker : IDisposable
    {
        private readonly DateTime _startTime = DateTime.Now;
        private readonly Stopwatch _timer = Stopwatch.StartNew();
        private readonly TelemetryClient? _telemetryClient;
        private readonly NpgsqlCommand? _cmd;
        private bool _tracked = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryTracker"/> class.
        /// </summary>
        /// <param name="telemetryClient">Telemetry client from DI</param>
        /// <param name="cmd">The npgsql cmd</param>
        public TelemetryTracker(TelemetryClient telemetryClient, NpgsqlCommand cmd)
        {
            _telemetryClient = telemetryClient;
            _cmd = cmd;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryTracker"/> class.
        /// </summary>
        public TelemetryTracker(TelemetryClient? telemetryClient)
        {
            _telemetryClient = telemetryClient;
            _cmd = null;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_tracked)
            {
                Track("Dispose", false);
                _tracked = true;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Track the PostgreSQL invocation
        /// </summary>
        public void Track(string cmdText, bool success = true)
        {
            _timer.Stop();
            if (_telemetryClient != null)
            {
                _telemetryClient.TrackDependency("Postgres", cmdText, cmdText, _startTime, _timer.Elapsed, success);
            }

            _tracked = true;
        }

        /// <summary>
        /// Track the PostgreSQL invocation
        /// <paramref name="success">Outcome of invocation</paramref>
        /// </summary>
        public void Track(bool success = true)
        {
            _timer.Stop();
            if (_telemetryClient != null)
            {
                _telemetryClient.TrackDependency("Postgres", _cmd.CommandText, _cmd.CommandText, _startTime, _timer.Elapsed, success);
            }

            _tracked = true;
        }
    }
}
