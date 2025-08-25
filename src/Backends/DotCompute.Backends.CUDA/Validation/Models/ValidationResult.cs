using System;
using System.Collections.Generic;
using DotCompute.Backends.CUDA.Validation.Types;

namespace DotCompute.Backends.CUDA.Validation.Models
{
    /// <summary>
    /// Result of a single validation test.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets or initializes the test category.
        /// </summary>
        public required string Category { get; init; }

        /// <summary>
        /// Gets or initializes the test name.
        /// </summary>
        public required string TestName { get; init; }

        /// <summary>
        /// Gets or sets the validation status.
        /// </summary>
        public ValidationStatus Status { get; set; } = ValidationStatus.NotSet;

        /// <summary>
        /// Gets or sets the error message if validation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets the list of detailed information about the test.
        /// </summary>
        public List<string> Details { get; } = new();

        /// <summary>
        /// Gets or sets the test execution duration.
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the test was executed.
        /// </summary>
        public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets any exception that occurred during validation.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets performance metrics if applicable.
        /// </summary>
        public Dictionary<string, double>? PerformanceMetrics { get; set; }
    }
}