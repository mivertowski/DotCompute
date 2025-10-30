// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Advanced
{
    public sealed partial class CudaCooperativeGroupsManager
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 27001,
            Level = LogLevel.Debug,
            Message = "Cooperative Groups Manager initialized")]
        private static partial void LogManagerInitialized(ILogger logger);

        [LoggerMessage(
            EventId = 27002,
            Level = LogLevel.Error,
            Message = "Error optimizing kernel for cooperative groups")]
        private static partial void LogOptimizationError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 27003,
            Level = LogLevel.Error,
            Message = "Error launching cooperative kernel")]
        private static partial void LogLaunchError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 27004,
            Level = LogLevel.Debug,
            Message = "Cleaned up {Count} unused cooperative kernels")]
        private static partial void LogMaintenanceCleanup(ILogger logger, int count);

        [LoggerMessage(
            EventId = 27005,
            Level = LogLevel.Warning,
            Message = "Error during cooperative groups maintenance")]
        private static partial void LogMaintenanceError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 27006,
            Level = LogLevel.Trace,
            Message = "Updated cooperative groups metrics: Efficiency={EfficiencyScore:F3}, Overhead={SynchronizationOverhead:F2}ms")]
        private static partial void LogMetricsUpdate(ILogger logger, double efficiencyScore, double synchronizationOverhead);

        [LoggerMessage(
            EventId = 27007,
            Level = LogLevel.Warning,
            Message = "Error updating cooperative groups metrics")]
        private static partial void LogMetricsUpdateError(ILogger logger, Exception ex);

        #endregion
    }
}
