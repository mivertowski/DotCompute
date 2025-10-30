// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Advanced
{
    public sealed partial class CudaTensorCoreManager
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 27031,
            Level = LogLevel.Debug,
            Message = "Tensor Core Manager initialized for {Architecture}, Generation {Generation}, supported precisions: {Precisions}")]
        private static partial void LogManagerInitialized(ILogger logger, string architecture, int generation, string precisions);

        [LoggerMessage(
            EventId = 27032,
            Level = LogLevel.Error,
            Message = "Error optimizing kernel for Tensor Cores")]
        private static partial void LogOptimizationError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 27033,
            Level = LogLevel.Error,
            Message = "Error executing Tensor Core operation")]
        private static partial void LogExecutionError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 27034,
            Level = LogLevel.Debug,
            Message = "Cleaned up {Count} unused Tensor Core kernels")]
        private static partial void LogMaintenanceCleanup(ILogger logger, int count);

        [LoggerMessage(
            EventId = 27035,
            Level = LogLevel.Warning,
            Message = "Error during Tensor Core maintenance")]
        private static partial void LogMaintenanceError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 27036,
            Level = LogLevel.Warning,
            Message = "Error updating Tensor Core performance metrics")]
        private static partial void LogMetricsUpdateError(ILogger logger, Exception ex);

        #endregion
    }
}
