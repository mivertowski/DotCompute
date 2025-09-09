// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Core.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    // Kernel Execution Messages
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Executing kernel {KernelName} on accelerator {AcceleratorName}")]
    public static partial void KernelExecutionStarted(this ILogger logger, string kernelName, string acceleratorName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Kernel {KernelName} completed in {ElapsedMilliseconds}ms")]
    public static partial void KernelExecutionCompleted(this ILogger logger, string kernelName, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Kernel {KernelName} execution failed: {ErrorMessage}")]
    public static partial void KernelExecutionFailed(this ILogger logger, string kernelName, string errorMessage, Exception? exception = null);

    // Memory Management Messages
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Allocated {ByteCount} bytes for buffer {BufferId}")]
    public static partial void MemoryAllocated(this ILogger logger, long byteCount, string bufferId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Freed {ByteCount} bytes from buffer {BufferId}")]
    public static partial void MemoryFreed(this ILogger logger, long byteCount, string bufferId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Memory pool pressure detected: {UsedBytes}/{TotalBytes} bytes used ({PercentUsed}%)")]
    public static partial void MemoryPoolPressure(this ILogger logger, long usedBytes, long totalBytes, double percentUsed);

    // Backend Selection Messages
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "Selected backend {BackendName} for kernel {KernelName} (confidence: {Confidence:F2})")]
    public static partial void BackendSelected(this ILogger logger, string backendName, string kernelName, double confidence);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Backend {BackendName} scored {Score:F2} for workload characteristics")]
    public static partial void BackendScored(this ILogger logger, string backendName, double score);

    // Pipeline Messages
    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Debug,
        Message = "Pipeline {PipelineName} stage {StageName} started")]
    public static partial void PipelineStageStarted(this ILogger logger, string pipelineName, string stageName);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Debug,
        Message = "Pipeline {PipelineName} stage {StageName} completed in {ElapsedMilliseconds}ms")]
    public static partial void PipelineStageCompleted(this ILogger logger, string pipelineName, string stageName, double elapsedMilliseconds);

    // Performance Monitoring Messages
    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Information,
        Message = "Performance metric {MetricName}: {Value:F2} {Unit}")]
    public static partial void PerformanceMetricRecorded(this ILogger logger, string metricName, double value, string unit);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message = "Performance degradation detected: {MetricName} decreased by {PercentDecrease:F1}%")]
    public static partial void PerformanceDegradation(this ILogger logger, string metricName, double percentDecrease);

    // Debugging Messages
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Debug,
        Message = "Debug validation for kernel {KernelName}: {ValidationResult}")]
    public static partial void DebugValidation(this ILogger logger, string kernelName, string validationResult);

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Warning,
        Message = "Debug mismatch detected between {Backend1} and {Backend2}: {Difference}")]
    public static partial void DebugMismatch(this ILogger logger, string backend1, string backend2, string difference);

    // Optimization Messages
    [LoggerMessage(
        EventId = 7000,
        Level = LogLevel.Information,
        Message = "Optimization strategy {Strategy} applied to kernel {KernelName}")]
    public static partial void OptimizationApplied(this ILogger logger, string strategy, string kernelName);

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Debug,
        Message = "ML model prediction: {PredictedBackend} with confidence {Confidence:F2}")]
    public static partial void MLPrediction(this ILogger logger, string predictedBackend, double confidence);

    // Accelerator Messages
    [LoggerMessage(
        EventId = 8000,
        Level = LogLevel.Information,
        Message = "Accelerator {AcceleratorName} initialized: {DeviceType} with {MemoryGB:F1}GB memory")]
    public static partial void AcceleratorInitialized(this ILogger logger, string acceleratorName, string deviceType, double memoryGB);

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Warning,
        Message = "Accelerator {AcceleratorName} utilization high: {Utilization}%")]
    public static partial void AcceleratorHighUtilization(this ILogger logger, string acceleratorName, int utilization);

    // Telemetry Messages
    [LoggerMessage(
        EventId = 9000,
        Level = LogLevel.Trace,
        Message = "Telemetry event {EventName} recorded with data: {Data}")]
    public static partial void TelemetryEvent(this ILogger logger, string eventName, string data);

    // General Messages
    [LoggerMessage(
        EventId = 10000,
        Level = LogLevel.Error,
        Message = "Unexpected error in {Component}: {ErrorMessage}")]
    public static partial void UnexpectedError(this ILogger logger, string component, string errorMessage, Exception? exception = null);

    [LoggerMessage(
        EventId = 10001,
        Level = LogLevel.Critical,
        Message = "Critical failure in {Component}: System recovery required")]
    public static partial void CriticalFailure(this ILogger logger, string component, Exception exception);
}