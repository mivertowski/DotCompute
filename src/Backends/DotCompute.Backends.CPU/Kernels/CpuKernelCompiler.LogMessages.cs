// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// LoggerMessage delegates for CpuKernelCompiler.
/// </summary>
internal static partial class CpuKernelCompiler
{
    // Event ID Range: 7660-7679

    [LoggerMessage(
        EventId = 7660,
        Level = LogLevel.Debug,
        Message = "Starting kernel compilation: {KernelName}")]
    private static partial void LogStartingCompilation(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7661,
        Level = LogLevel.Information,
        Message = "Successfully compiled kernel '{KernelName}' in {ElapsedMs}ms with {OptimizationLevel} optimization")]
    private static partial void LogCompilationSuccess(ILogger logger, string kernelName, long elapsedMs, object optimizationLevel);

    [LoggerMessage(
        EventId = 7662,
        Level = LogLevel.Error,
        Message = "Failed to compile kernel '{KernelName}'")]
    private static partial void LogCompilationError(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 7663,
        Level = LogLevel.Debug,
        Message = "SIMD Instruction Selector disposed")]
    private static partial void LogSelectorDisposed(ILogger logger);
}
