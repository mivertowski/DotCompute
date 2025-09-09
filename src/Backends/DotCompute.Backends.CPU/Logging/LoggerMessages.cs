// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Backends.CPU.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    // CPU Initialization Messages
    [LoggerMessage(
        EventId = 40000,
        Level = LogLevel.Information,
        Message = "CPU backend initialized: {ProcessorCount} cores, {Architecture} architecture")]
    public static partial void CpuInitialized(this ILogger logger, int processorCount, string architecture);

    [LoggerMessage(
        EventId = 40001,
        Level = LogLevel.Information,
        Message = "SIMD support detected: {SimdLevel} with {VectorSize}-bit vectors")]
    public static partial void SimdSupported(this ILogger logger, string simdLevel, int vectorSize);

    // CPU Execution Messages
    [LoggerMessage(
        EventId = 41000,
        Level = LogLevel.Debug,
        Message = "Executing CPU kernel {KernelName} with {ThreadCount} threads")]
    public static partial void CpuKernelExecuting(this ILogger logger, string kernelName, int threadCount);

    [LoggerMessage(
        EventId = 41001,
        Level = LogLevel.Debug,
        Message = "CPU kernel {KernelName} completed in {ElapsedMs}ms")]
    public static partial void CpuKernelCompleted(this ILogger logger, string kernelName, double elapsedMs);

    [LoggerMessage(
        EventId = 41002,
        Level = LogLevel.Information,
        Message = "Vectorized operation {Operation} achieved {Speedup:F2}x speedup")]
    public static partial void VectorizedSpeedup(this ILogger logger, string operation, double speedup);

    // Thread Pool Messages
    [LoggerMessage(
        EventId = 42000,
        Level = LogLevel.Debug,
        Message = "Thread pool configured: Min={MinThreads}, Max={MaxThreads}")]
    public static partial void ThreadPoolConfigured(this ILogger logger, int minThreads, int maxThreads);

    [LoggerMessage(
        EventId = 42001,
        Level = LogLevel.Warning,
        Message = "Thread pool exhaustion detected: {PendingTasks} tasks pending")]
    public static partial void ThreadPoolExhaustion(this ILogger logger, int pendingTasks);

    // SIMD Optimization Messages
    [LoggerMessage(
        EventId = 43000,
        Level = LogLevel.Debug,
        Message = "Using {SimdType} intrinsics for {Operation}")]
    public static partial void SimdIntrinsicsUsed(this ILogger logger, string simdType, string operation);

    [LoggerMessage(
        EventId = 43001,
        Level = LogLevel.Information,
        Message = "AVX512 detected and enabled for enhanced performance")]
    public static partial void Avx512Enabled(this ILogger logger);

    [LoggerMessage(
        EventId = 43002,
        Level = LogLevel.Warning,
        Message = "Falling back to scalar implementation for {Operation}: SIMD not supported")]
    public static partial void SimdFallback(this ILogger logger, string operation);

    // Memory Messages
    [LoggerMessage(
        EventId = 44000,
        Level = LogLevel.Debug,
        Message = "CPU memory allocated: {SizeBytes} bytes, alignment: {Alignment}")]
    public static partial void CpuMemoryAllocated(this ILogger logger, long sizeBytes, int alignment);

    [LoggerMessage(
        EventId = 44001,
        Level = LogLevel.Debug,
        Message = "Cache-friendly memory layout applied: {LayoutType}")]
    public static partial void CacheFriendlyLayout(this ILogger logger, string layoutType);

    // Performance Messages
    [LoggerMessage(
        EventId = 45000,
        Level = LogLevel.Information,
        Message = "CPU utilization: {Utilization:F1}%, Cache misses: {CacheMisses}")]
    public static partial void CpuPerformanceMetrics(this ILogger logger, double utilization, long cacheMisses);

    // Error Messages
    [LoggerMessage(
        EventId = 46000,
        Level = LogLevel.Error,
        Message = "CPU backend error in {Component}: {ErrorMessage}")]
    public static partial void CpuError(this ILogger logger, string component, string errorMessage, Exception? exception = null);

    // Generic Messages for common logging patterns
    [LoggerMessage(EventId = 47000, Level = LogLevel.Information, Message = "{Message}")]
    public static partial void LogInfoMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47001, Level = LogLevel.Debug, Message = "{Message}")]
    public static partial void LogDebugMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47002, Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void LogWarningMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47003, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47004, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, Exception ex, string message);

    [LoggerMessage(EventId = 47005, Level = LogLevel.Trace, Message = "{Message}")]
    public static partial void LogTraceMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47006, Level = LogLevel.Critical, Message = "{Message}")]
    public static partial void LogCriticalMessage(this ILogger logger, string message);
}