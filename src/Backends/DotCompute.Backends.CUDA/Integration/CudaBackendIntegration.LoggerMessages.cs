// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration
{
    /// <summary>
    /// LoggerMessage delegates for CudaBackendIntegration.
    /// </summary>
    public sealed partial class CudaBackendIntegration
    {
        [LoggerMessage(
            EventId = 20000,
            Level = LogLevel.Information,
            Message = "CUDA Backend Integration initialized for device {DeviceId}")]
        private static partial void LogBackendInitialized(ILogger logger, int deviceId);

        [LoggerMessage(
            EventId = 20001,
            Level = LogLevel.Information,
            Message = "Backend optimization completed: {OptimizationCount} optimizations applied")]
        private static partial void LogOptimizationCompleted(ILogger logger, int optimizationCount);

        [LoggerMessage(
            EventId = 20002,
            Level = LogLevel.Warning,
            Message = "CUDA backend health degraded: {OverallHealth:F2}")]
        private static partial void LogBackendHealthDegraded(ILogger logger, double overallHealth);

        [LoggerMessage(
            EventId = 20003,
            Level = LogLevel.Debug,
            Message = "CUDA backend health: {OverallHealth:F2}")]
        private static partial void LogBackendHealth(ILogger logger, double overallHealth);

        [LoggerMessage(
            EventId = 20004,
            Level = LogLevel.Warning,
            Message = "Error during health check")]
        private static partial void LogHealthCheckError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 20005,
            Level = LogLevel.Information,
            Message = "CUDA backend maintenance completed")]
        private static partial void LogMaintenanceCompleted(ILogger logger);

        [LoggerMessage(
            EventId = 20006,
            Level = LogLevel.Warning,
            Message = "Error during memory cleanup maintenance")]
        private static partial void LogMemoryCleanupError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 20007,
            Level = LogLevel.Information,
            Message = "CUDA Backend Integration disposed")]
        private static partial void LogBackendDisposed(ILogger logger);

        [LoggerMessage(
            EventId = 20008,
            Level = LogLevel.Warning,
            Message = "Error updating performance metrics")]
        private static partial void LogErrorUpdatingPerformanceMetrics(ILogger logger, Exception ex);
    }
}
