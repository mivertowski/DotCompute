// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// High-performance logger message delegates for CpuCompiledKernel.
/// </summary>
internal static partial class CpuCompiledKernelLoggerMessages
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Executing kernel '{KernelName}' with global work size: [{WorkSize}], vectorization: {Vectorization}")]
    public static partial void LogExecutingKernel(ILogger logger, string kernelName, string workSize, string vectorization);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Debug,
        Message = "Kernel '{KernelName}' execution completed in {ElapsedMs:F2}ms")]
    public static partial void LogKernelExecutionCompleted(ILogger logger, string kernelName, double elapsedMs);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Kernel '{KernelName}' performance: {ExecutionCount} executions, avg time: {AvgTime:F2}ms")]
    public static partial void LogKernelPerformance(ILogger logger, string kernelName, long executionCount, double avgTime);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "Failed to execute compiled delegate, falling back to default implementation")]
    public static partial void LogFailedCompiledDelegate(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Debug,
        Message = "Disposed kernel: {KernelName}")]
    public static partial void LogKernelDisposed(ILogger logger, string kernelName);
}
