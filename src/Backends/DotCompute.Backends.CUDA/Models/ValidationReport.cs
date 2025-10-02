using DotCompute.Backends.CUDA.Types;

namespace DotCompute.Backends.CUDA.Models
{
    /// <summary>
    /// Comprehensive validation report for CUDA backend.
    /// </summary>
    public class ValidationReport
    {
        /// <summary>
        /// Gets or sets when validation started.
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Gets or sets when validation ended.
        /// </summary>
        public DateTimeOffset EndTime { get; set; }

        /// <summary>
        /// Gets or sets the total validation duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the validation options used.
        /// </summary>
        public ValidationOptions Options { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of validation results.
        /// </summary>
        public List<UnifiedValidationResult> Results { get; set; } = [];

        /// <summary>
        /// Gets or sets the total number of tests.
        /// </summary>
        public int TotalTests { get; set; }

        /// <summary>
        /// Gets or sets the number of passed tests.
        /// </summary>
        public int PassedTests { get; set; }

        /// <summary>
        /// Gets or sets the number of failed tests.
        /// </summary>
        public int FailedTests { get; set; }

        /// <summary>
        /// Gets or sets the number of tests with warnings.
        /// </summary>
        public int WarningTests { get; set; }

        /// <summary>
        /// Gets or sets the number of skipped tests.
        /// </summary>
        public int SkippedTests { get; set; }

        /// <summary>
        /// Gets or sets the overall validation status.
        /// </summary>
        public ValidationStatus OverallStatus { get; set; }

        /// <summary>
        /// Gets or sets any critical error that stopped validation.
        /// </summary>
        public string? CriticalError { get; set; }

        /// <summary>
        /// Gets the pass rate percentage.
        /// </summary>
        public double PassRate
            => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;

        /// <summary>
        /// Gets whether validation was successful.
        /// </summary>
        public bool IsSuccessful
            => OverallStatus == ValidationStatus.Passed ||

            OverallStatus == ValidationStatus.Warning && FailedTests == 0;
    }
}
