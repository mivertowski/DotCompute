// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// LoggerMessage delegates for CpuKernelExecutor.
/// </summary>
internal sealed partial class CpuKernelExecutor
{
    // Event ID Range: 7680-7699

    [LoggerMessage(
        EventId = 7680,
        Level = LogLevel.Debug,
        Message = "Executing kernel '{KernelName}' with {TotalWorkItems} items using {Strategy}")]
    private static partial void LogExecutingKernel(ILogger logger, string kernelName, long totalWorkItems, ExecutionStrategy strategy);

    [LoggerMessage(
        EventId = 7681,
        Level = LogLevel.Debug,
        Message = "Kernel '{KernelName}' executed in {ElapsedMs}ms")]
    private static partial void LogKernelExecuted(ILogger logger, string kernelName, long elapsedMs);

    [LoggerMessage(
        EventId = 7682,
        Level = LogLevel.Error,
        Message = "Failed to execute kernel '{KernelName}'")]
    private static partial void LogExecutionError(ILogger logger, Exception ex, string kernelName);
}
