// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{
    public sealed partial class CudaGraphSupport
    {
        [LoggerMessage(EventId = 26100, Level = LogLevel.Warning,
            Message = "Error cleaning up failed graph capture")]
        private static partial void LogGraphCaptureCleanupError(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 26101, Level = LogLevel.Warning,
            Message = "Error cleaning up graph node during build failure")]
        private static partial void LogGraphNodeCleanupError(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 26102, Level = LogLevel.Warning,
            Message = "Error optimizing graph {GraphId}")]
        private static partial void LogGraphOptimizationError(ILogger logger, string graphId, Exception ex);

        [LoggerMessage(EventId = 26103, Level = LogLevel.Warning,
            Message = "Error during periodic graph optimization")]
        private static partial void LogPeriodicOptimizationError(ILogger logger, Exception ex);
    }
}
