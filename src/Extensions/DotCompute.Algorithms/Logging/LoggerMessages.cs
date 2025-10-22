
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#nullable disable

using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Algorithms.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    // Algorithm Execution Messages
    [LoggerMessage(
        EventId = 70000,
        Level = LogLevel.Information,
        Message = "Executing algorithm {AlgorithmName} with input size {InputSize}")]
    public static partial void AlgorithmExecuting(this ILogger logger, string algorithmName, long inputSize);

    [LoggerMessage(
        EventId = 70001,
        Level = LogLevel.Information,
        Message = "Algorithm {AlgorithmName} completed in {ElapsedMs}ms")]
    public static partial void AlgorithmCompleted(this ILogger logger, string algorithmName, double elapsedMs);

    [LoggerMessage(
        EventId = 70002,
        Level = LogLevel.Debug,
        Message = "Algorithm {AlgorithmName} using backend {Backend}")]
    public static partial void AlgorithmBackendSelected(this ILogger logger, string algorithmName, string backend);

    // Linear Algebra Messages
    [LoggerMessage(
        EventId = 71000,
        Level = LogLevel.Debug,
        Message = "Matrix operation {Operation}: {Rows}x{Cols} matrix")]
    public static partial void MatrixOperation(this ILogger logger, string operation, int rows, int cols);

    [LoggerMessage(
        EventId = 71001,
        Level = LogLevel.Information,
        Message = "BLAS operation {Operation} achieved {GFlops:F2} GFLOPS")]
    public static partial void BlasPerformance(this ILogger logger, string operation, double gFlops);

    // FFT Messages
    [LoggerMessage(
        EventId = 72000,
        Level = LogLevel.Debug,
        Message = "FFT transform: {Size} points, {Direction}")]
    public static partial void FftTransform(this ILogger logger, int size, string direction);

    // Sorting Messages
    [LoggerMessage(
        EventId = 73000,
        Level = LogLevel.Debug,
        Message = "Sorting {ElementCount} elements using {Algorithm}")]
    public static partial void SortingOperation(this ILogger logger, long elementCount, string algorithm);

    // Reduction Messages
    [LoggerMessage(
        EventId = 74000,
        Level = LogLevel.Debug,
        Message = "Reduction operation {Operation} on {ElementCount} elements")]
    public static partial void ReductionOperation(this ILogger logger, string operation, long elementCount);

    // Performance Messages
    [LoggerMessage(
        EventId = 75000,
        Level = LogLevel.Information,
        Message = "Algorithm {AlgorithmName} throughput: {Throughput:F2} GB/s")]
    public static partial void AlgorithmThroughput(this ILogger logger, string algorithmName, double throughput);

    // Error Messages
    [LoggerMessage(
        EventId = 76000,
        Level = LogLevel.Error,
        Message = "Algorithm {AlgorithmName} failed: {ErrorMessage}")]
    public static partial void AlgorithmError(this ILogger logger, string algorithmName, string errorMessage, Exception exception);

    [LoggerMessage(
        EventId = 76001,
        Level = LogLevel.Warning,
        Message = "Algorithm {AlgorithmName} warning: {WarningMessage}")]
    public static partial void AlgorithmWarning(this ILogger logger, string algorithmName, string warningMessage);

    // Generic Messages for common logging patterns
    [LoggerMessage(EventId = 77000, Level = LogLevel.Information, Message = "{Message}")]
    public static partial void LogInfoMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 77001, Level = LogLevel.Debug, Message = "{Message}")]
    public static partial void LogDebugMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 77002, Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void LogWarningMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 77003, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 77004, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, Exception ex, string message);

    [LoggerMessage(EventId = 77005, Level = LogLevel.Trace, Message = "{Message}")]
    public static partial void LogTraceMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 77006, Level = LogLevel.Critical, Message = "{Message}")]
    public static partial void LogCriticalMessage(this ILogger logger, string message);
}