using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Statistics about errors and recovery attempts.
    /// </summary>
    public class ErrorStatistics
    {
        /// <summary>
        /// Gets or sets the total number of errors encountered.
        /// </summary>
        public int TotalErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of errors successfully recovered.
        /// </summary>
        public int RecoveredErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of unrecoverable errors.
        /// </summary>
        public int UnrecoverableErrors { get; set; }

        /// <summary>
        /// Gets errors grouped by category.
        /// </summary>
        public Dictionary<ErrorCategory, int> ErrorsByCategory { get; init; } = [];

        /// <summary>
        /// Gets recovery success rates by error category.
        /// </summary>
        public Dictionary<ErrorCategory, double> RecoveryRateByCategory { get; init; } = [];

        /// <summary>
        /// Gets or sets the total retry attempts.
        /// </summary>
        public int TotalRetryAttempts { get; set; }

        /// <summary>
        /// Gets or sets the number of successful retries.
        /// </summary>
        public int SuccessfulRetries { get; set; }

        /// <summary>
        /// Gets or sets the number of CPU fallbacks.
        /// </summary>
        public int CpuFallbacks { get; set; }

        /// <summary>
        /// Gets or sets the number of device resets.
        /// </summary>
        public int DeviceResets { get; set; }

        /// <summary>
        /// Gets or sets the total downtime due to errors.
        /// </summary>
        public TimeSpan TotalDowntime { get; set; }

        /// <summary>
        /// Gets or sets when statistics collection started.
        /// </summary>
        public DateTimeOffset CollectionStarted { get; set; }

        /// <summary>
        /// Gets the overall recovery rate.
        /// </summary>
        public double OverallRecoveryRate
            => TotalErrors > 0 ? (double)RecoveredErrors / TotalErrors : 0;

        /// <summary>
        /// Gets the retry success rate.
        /// </summary>
        public double RetrySuccessRate
            => TotalRetryAttempts > 0 ? (double)SuccessfulRetries / TotalRetryAttempts : 0;
    }
}
