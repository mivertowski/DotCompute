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
        #region LoggerMessage Delegates

        [LoggerMessage(EventId = 24031, Level = LogLevel.Information,
            Message = "CUDA Kernel Executor initialized for device {DeviceId} ({DeviceName})")]
        private static partial void LogExecutorInitialized(ILogger logger, int deviceId, string deviceName);

        [LoggerMessage(EventId = 24032, Level = LogLevel.Error,
            Message = "Kernel execution failed")]
        private static partial void LogKernelExecutionFailed(ILogger logger);

        [LoggerMessage(EventId = 24033, Level = LogLevel.Error,
            Message = "Failed to execute kernel asynchronously")]
        private static partial void LogAsyncExecutionFailed(ILogger logger);

        [LoggerMessage(EventId = 24034, Level = LogLevel.Debug,
            Message = "Calculated optimal block size for kernel")]
        private static partial void LogOptimalBlockSizeCalculated(ILogger logger);

        #endregion
    }
}
