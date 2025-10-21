// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration.Components;

/// <summary>
/// LoggerMessage delegates for CudaErrorHandler.
/// </summary>
public sealed partial class CudaErrorHandler
{
    /// <summary>
    /// Event ID range: 29000-29099 (CUDA error handling range).
    /// 29000-29019: Error reporting by severity.
    /// 29020-29039: Recovery attempts.
    /// 29040-29059: Context dumps.
    /// </summary>
    private static partial class Log
    {
        // Error reporting by severity (29000-29019)

        [LoggerMessage(
            EventId = 29000,
            Level = LogLevel.Critical,
            Message = "CUDA error {Error} in operation {Operation}")]
        public static partial void LogCriticalError(
            ILogger logger,
            CudaError error,
            string operation);

        [LoggerMessage(
            EventId = 29001,
            Level = LogLevel.Error,
            Message = "CUDA error {Error} in operation {Operation}")]
        public static partial void LogErrorError(
            ILogger logger,
            CudaError error,
            string operation);

        [LoggerMessage(
            EventId = 29002,
            Level = LogLevel.Warning,
            Message = "CUDA error {Error} in operation {Operation}")]
        public static partial void LogWarningError(
            ILogger logger,
            CudaError error,
            string operation);

        [LoggerMessage(
            EventId = 29003,
            Level = LogLevel.Information,
            Message = "CUDA error {Error} in operation {Operation}")]
        public static partial void LogInformationError(
            ILogger logger,
            CudaError error,
            string operation);

        [LoggerMessage(
            EventId = 29004,
            Level = LogLevel.Debug,
            Message = "CUDA error {Error} in operation {Operation}")]
        public static partial void LogDebugError(
            ILogger logger,
            CudaError error,
            string operation);

        // Recovery attempts (29020-29039)

        [LoggerMessage(
            EventId = 29020,
            Level = LogLevel.Information,
            Message = "Successfully recovered from CUDA error {Error} in operation {Operation}")]
        public static partial void LogRecoverySuccess(
            ILogger logger,
            CudaError error,
            string operation);

        [LoggerMessage(
            EventId = 29021,
            Level = LogLevel.Warning,
            Message = "Failed to recover from CUDA error {Error} in operation {Operation}: {Message}")]
        public static partial void LogRecoveryFailure(
            ILogger logger,
            CudaError error,
            string operation,
            string message);

        [LoggerMessage(
            EventId = 29022,
            Level = LogLevel.Error,
            Message = "Exception during error recovery for {Error} in {Operation}")]
        public static partial void LogRecoveryException(
            ILogger logger,
            Exception ex,
            CudaError error,
            string operation);

        // Diagnostics and health (29040-29059)

        [LoggerMessage(
            EventId = 29040,
            Level = LogLevel.Warning,
            Message = "Failed to query device status")]
        public static partial void LogDeviceStatusQueryFailed(
            ILogger logger,
            Exception ex);

        [LoggerMessage(
            EventId = 29041,
            Level = LogLevel.Debug,
            Message = "Health check completed: {OverallHealth}")]
        public static partial void LogHealthCheckCompleted(
            ILogger logger,
            Integration.Components.Enums.CudaHealthStatus overallHealth);

        [LoggerMessage(
            EventId = 29042,
            Level = LogLevel.Error,
            Message = "Error during health check")]
        public static partial void LogHealthCheckError(
            ILogger logger,
            Exception ex);

        [LoggerMessage(
            EventId = 29043,
            Level = LogLevel.Debug,
            Message = "Error handling policies configured")]
        public static partial void LogPoliciesConfigured(
            ILogger logger);

        [LoggerMessage(
            EventId = 29044,
            Level = LogLevel.Warning,
            Message = "Periodic diagnostics: {CriticalErrors} critical errors detected")]
        public static partial void LogPeriodicDiagnosticsCriticalErrors(
            ILogger logger,
            int criticalErrors);

        [LoggerMessage(
            EventId = 29045,
            Level = LogLevel.Warning,
            Message = "Periodic diagnostics: Low recovery success rate: {Rate:P1}")]
        public static partial void LogPeriodicDiagnosticsLowRecoveryRate(
            ILogger logger,
            double rate);

        [LoggerMessage(
            EventId = 29046,
            Level = LogLevel.Warning,
            Message = "Error during periodic diagnostics")]
        public static partial void LogPeriodicDiagnosticsError(
            ILogger logger,
            Exception ex);

        [LoggerMessage(
            EventId = 29047,
            Level = LogLevel.Debug,
            Message = "CUDA error handler disposed")]
        public static partial void LogErrorHandlerDisposed(
            ILogger logger);

        [LoggerMessage(
            EventId = 29048,
            Level = LogLevel.Debug,
            Message = "CUDA error handler initialized for device {DeviceId}")]
        public static partial void LogErrorHandlerInitialized(
            ILogger logger,
            int deviceId);
    }
}
