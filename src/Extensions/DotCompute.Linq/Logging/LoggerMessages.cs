// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
namespace DotCompute.Linq.Logging;
/// <summary>
/// High-performance logger message delegates for DotCompute.Linq.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    // LINQ Query Messages
    [LoggerMessage(
        EventId = 80000,
        Level = LogLevel.Debug,
        Message = "LINQ query compiled for {Backend} backend")]
    public static partial void LinqQueryCompiled(this ILogger logger, string backend);
        EventId = 80001,
        Level = LogLevel.Information,
        Message = "LINQ query executed: {ElementCount} elements processed in {ElapsedMs}ms")]
    public static partial void LinqQueryExecuted(this ILogger logger, long elementCount, double elapsedMs);
        EventId = 80002,
        Message = "LINQ operator {Operator} accelerated on {Backend}")]
    public static partial void LinqOperatorAccelerated(this ILogger logger, string @operator, string backend);
    // Query Optimization Messages
        EventId = 81000,
        Message = "Query optimization applied: {OptimizationType}")]
    public static partial void QueryOptimizationApplied(this ILogger logger, string optimizationType);
        EventId = 81001,
        Message = "Query plan rewritten: {OriginalOps} operations → {OptimizedOps} operations")]
    public static partial void QueryPlanOptimized(this ILogger logger, int originalOps, int optimizedOps);
    // Kernel Fusion Messages
        EventId = 82000,
        Message = "Kernel fusion: {KernelCount} kernels fused into 1")]
    public static partial void KernelsFused(this ILogger logger, int kernelCount);
    // Fallback Messages
        EventId = 83000,
        Level = LogLevel.Warning,
        Message = "LINQ operator {Operator} falling back to CPU implementation")]
    public static partial void LinqFallbackToCpu(this ILogger logger, string @operator);
        EventId = 83001,
        Message = "GPU acceleration unavailable for {Reason}")]
    public static partial void GpuAccelerationUnavailable(this ILogger logger, string reason);
    // Performance Messages
        EventId = 84000,
        Message = "LINQ GPU acceleration achieved {Speedup:F2}x speedup")]
    public static partial void LinqSpeedup(this ILogger logger, double speedup);
    // Error Messages
        EventId = 85000,
        Level = LogLevel.Error,
        Message = "LINQ provider error: {ErrorMessage}")]
    public static partial void LinqError(this ILogger logger, string errorMessage, Exception? exception = null);
        EventId = 85001,
        Message = "LINQ provider warning: {WarningMessage}")]
    public static partial void LinqWarning(this ILogger logger, string warningMessage);
    // Generic Messages for common logging patterns
    [LoggerMessage(EventId = 86000, Level = LogLevel.Information, Message = "{Message}")]
    public static partial void LogInfoMessage(this ILogger logger, string message);
    [LoggerMessage(EventId = 86001, Level = LogLevel.Debug, Message = "{Message}")]
    public static partial void LogDebugMessage(this ILogger logger, string message);
    [LoggerMessage(EventId = 86002, Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void LogWarningMessage(this ILogger logger, string message);
    [LoggerMessage(EventId = 86003, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, string message);
    [LoggerMessage(EventId = 86004, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, Exception ex, string message);
    [LoggerMessage(EventId = 86005, Level = LogLevel.Trace, Message = "{Message}")]
    public static partial void LogTraceMessage(this ILogger logger, string message);
    [LoggerMessage(EventId = 86006, Level = LogLevel.Critical, Message = "{Message}")]
    public static partial void LogCriticalMessage(this ILogger logger, string message);
}
