using System.Diagnostics;

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
        private readonly NpgsqlCommand _cmd;
        private bool _tracked = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryTracker"/> class.
        /// </summary>
        /// <param name="cmd">The npgsql cmd</param>
        public TelemetryTracker(NpgsqlCommand cmd)
        {
            _cmd = cmd;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Track the PostgreSQL invocation
        /// <paramref name="success">Outcome of invocation</paramref>
        /// </summary>
        public void Track(bool success = true)
        {
            _timer.Stop();
            _tracked = true;
        }

        /// <summary>
        /// Method to satisfy the dispose pattern
        /// </summary>
        /// <param name="disposing">Has disposed already been called?</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_tracked && disposing)
            {
                Track(false);
                _tracked = true;
            }
        }
    }
}
