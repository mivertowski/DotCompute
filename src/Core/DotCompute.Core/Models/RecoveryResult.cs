namespace DotCompute.Core.Models
{
    /// <summary>
    /// Result of an error recovery attempt.
    /// </summary>
    public class RecoveryResult
    {
        /// <summary>
        /// Gets or sets whether recovery was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the recovery method used.
        /// </summary>
        public RecoveryMethod Method { get; set; }

        /// <summary>
        /// Gets or sets the time taken for recovery.
        /// </summary>
        public TimeSpan RecoveryTime { get; set; }

        /// <summary>
        /// Gets or sets any data loss that occurred during recovery.
        /// </summary>
        public bool DataLossOccurred { get; set; }

        /// <summary>
        /// Gets or sets the description of the recovery action taken.
        /// </summary>
        public string RecoveryAction { get; set; } = "";

        /// <summary>
        /// Gets or sets any warnings from the recovery process.
        /// </summary>
        public string? Warning { get; set; }

        /// <summary>
        /// Gets or sets the error message if recovery failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Methods used for error recovery.
    /// </summary>
    public enum RecoveryMethod
    {
        /// <summary>
        /// No recovery attempted.
        /// </summary>
        None,

        /// <summary>
        /// Simple retry of the operation.
        /// </summary>
        Retry,

        /// <summary>
        /// Device reset and retry.
        /// </summary>
        DeviceReset,

        /// <summary>
        /// Memory cleanup and reallocation.
        /// </summary>
        MemoryCleanup,

        /// <summary>
        /// Fallback to CPU execution.
        /// </summary>
        CpuFallback,

        /// <summary>
        /// Migration to different device.
        /// </summary>
        DeviceMigration,

        /// <summary>
        /// Context recreation.
        /// </summary>
        ContextRecreation
    }
}
