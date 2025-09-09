// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

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

    [LoggerMessage(
        EventId = 80001,
        Level = LogLevel.Information,
        Message = "LINQ query executed: {ElementCount} elements processed in {ElapsedMs}ms")]
    public static partial void LinqQueryExecuted(this ILogger logger, long elementCount, double elapsedMs);

    [LoggerMessage(
        EventId = 80002,
        Level = LogLevel.Debug,
        Message = "LINQ operator {Operator} accelerated on {Backend}")]
    public static partial void LinqOperatorAccelerated(this ILogger logger, string @operator, string backend);

    // Query Optimization Messages
    [LoggerMessage(
        EventId = 81000,
        Level = LogLevel.Debug,
        Message = "Query optimization applied: {OptimizationType}")]
    public static partial void QueryOptimizationApplied(this ILogger logger, string optimizationType);

    [LoggerMessage(
        EventId = 81001,
        Level = LogLevel.Information,
        Message = "Query plan rewritten: {OriginalOps} operations → {OptimizedOps} operations")]
    public static partial void QueryPlanOptimized(this ILogger logger, int originalOps, int optimizedOps);

    // Kernel Fusion Messages
    [LoggerMessage(
        EventId = 82000,
        Level = LogLevel.Debug,
        Message = "Kernel fusion: {KernelCount} kernels fused into 1")]
    public static partial void KernelsFused(this ILogger logger, int kernelCount);

    // Fallback Messages
    [LoggerMessage(
        EventId = 83000,
        Level = LogLevel.Warning,
        Message = "LINQ operator {Operator} falling back to CPU implementation")]
    public static partial void LinqFallbackToCpu(this ILogger logger, string @operator);

    [LoggerMessage(
        EventId = 83001,
        Level = LogLevel.Debug,
        Message = "GPU acceleration unavailable for {Reason}")]
    public static partial void GpuAccelerationUnavailable(this ILogger logger, string reason);

    // Performance Messages
    [LoggerMessage(
        EventId = 84000,
        Level = LogLevel.Information,
        Message = "LINQ GPU acceleration achieved {Speedup:F2}x speedup")]
    public static partial void LinqSpeedup(this ILogger logger, double speedup);

    // Error Messages
    [LoggerMessage(
        EventId = 85000,
        Level = LogLevel.Error,
        Message = "LINQ provider error: {ErrorMessage}")]
    public static partial void LinqError(this ILogger logger, string errorMessage, Exception? exception = null);

    [LoggerMessage(
        EventId = 85001,
        Level = LogLevel.Warning,
        Message = "LINQ provider warning: {WarningMessage}")]
    public static partial void LinqWarning(this ILogger logger, string warningMessage);
}