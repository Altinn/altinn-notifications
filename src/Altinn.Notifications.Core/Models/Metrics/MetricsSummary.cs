using System;
using System.Collections.Generic;
using System.Text;

namespace Altinn.Notifications.Core.Models.Metrics
{
    /// <summary>
    /// Summary information for a generated metrics file.
    /// </summary>
    public class MetricsSummary
    {
        /// <summary>
        /// Gets or sets the stream containing the metrics file content.
        /// The caller is responsible for the lifecycle of the stream.
        /// </summary>
        public Stream FileStream { get; set; } = null!;

        /// <summary>
        /// Gets or sets the file name of the metrics file.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hash of the file content (e.g. MD5) used for integrity checks.
        /// </summary>
        public string FileHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the total number of file transfers represented in the file.
        /// </summary>
        public int TotalFileTransferCount { get; set; }

        /// <summary>
        /// Gets or sets the time when the metrics file was generated.
        /// </summary>
        public DateTimeOffset GeneratedAt { get; set; }

        /// <summary>
        /// Gets or sets the environment name where the metrics were generated.
        /// </summary>
        public string Environment { get; set; } = string.Empty;
    }
}
