using DotCompute.Backends.CUDA.ErrorHandling.Types;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Context information for error handling and recovery.
    /// </summary>
    public class ErrorContext
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; } = "";

        /// <summary>
        /// Gets or sets the error category.
        /// </summary>
        public ErrorCategory Category { get; set; }

        /// <summary>
        /// Gets or sets the operation that failed.
        /// </summary>
        public string FailedOperation { get; set; } = "";

        /// <summary>
        /// Gets or sets the device ID where the error occurred.
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// Gets or sets when the error occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the number of retry attempts made.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets whether the error is recoverable.
        /// </summary>
        public bool IsRecoverable { get; set; }

        /// <summary>
        /// Gets or sets whether CPU fallback is available.
        /// </summary>
        public bool CanFallbackToCpu { get; set; }

        /// <summary>
        /// Gets additional context data.
        /// </summary>
        public Dictionary<string, object> AdditionalData { get; init; } = [];

        /// <summary>
        /// Gets or sets the stack trace at the time of error.
        /// </summary>
        public string? StackTrace { get; set; }
    }
}