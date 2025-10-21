// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{
    /// <summary>
    /// LoggerMessage delegates for CudaKernelExecutor
    /// </summary>
    public sealed partial class CudaKernelExecutor
    {
        [LoggerMessage(EventId = 22100, Level = LogLevel.Information,
            Message = "CUDA Kernel Executor initialized for device {DeviceId} ({DeviceName})")]
        private static partial void LogExecutorInitialized(ILogger logger, int deviceId, string deviceName);

        [LoggerMessage(EventId = 22101, Level = LogLevel.Error,
            Message = "Kernel execution failed")]
        private static partial void LogKernelExecutionFailed(ILogger logger);

        [LoggerMessage(EventId = 22102, Level = LogLevel.Error,
            Message = "Asynchronous kernel execution failed")]
        private static partial void LogAsyncExecutionFailed(ILogger logger);

        [LoggerMessage(EventId = 22103, Level = LogLevel.Debug,
            Message = "Optimal block size calculated for RTX 2000 Ada architecture")]
        private static partial void LogOptimalBlockSizeCalculated(ILogger logger);

        [LoggerMessage(EventId = 22104, Level = LogLevel.Warning,
            Message = "Some executions did not complete within timeout during disposal")]
        private static partial void LogDisposalTimeoutWarning(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 22105, Level = LogLevel.Warning,
            Message = "Failed to clean up execution events")]
        private static partial void LogEventCleanupError(ILogger logger, Exception ex);
    }
}
