namespace Altinn.Notifications.Core.Models.Metrics
{
    /// <summary>
    /// Summary information for a generated metrics file.
    /// </summary>
    public record MetricsSummary
    {
        /// <summary>
        /// Gets or sets the stream containing the metrics file content.
        /// The caller is responsible for the lifecycle of the stream.
        /// </summary>
        public Stream FileStream { get; init; } = null!;

        /// <summary>
        /// Gets or sets the file name of the metrics file.
        /// </summary>
        public string FileName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the hash of the file content (e.g. MD5) used for integrity checks.
        /// Note: Although MD5 is no longer considered safe for encryption, 
        /// it is still widely used for file integrity verification due to its speed and simplicity. 
        /// The hash can be used to verify that the file content has not been altered or corrupted during transfer or storage.
        /// </summary>
        public string FileHash { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSizeBytes { get; init; }

        /// <summary>
        /// Gets or sets the total number of file transfers represented in the file.
        /// </summary>
        public int TotalFileTransferCount { get; init; }

        /// <summary>
        /// Gets or sets the time when the metrics file was generated.
        /// </summary>
        public DateTimeOffset GeneratedAt { get; init; }

        /// <summary>
        /// Gets or sets the environment name where the metrics were generated.
        /// </summary>
        public string Environment { get; init; } = string.Empty;
    }
}
