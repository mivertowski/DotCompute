// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Telemetry.Types;

namespace DotCompute.Abstractions.Telemetry.Providers;

/// <summary>
/// Abstract base class for telemetry providers.
/// </summary>
public abstract class TelemetryProvider : IDisposable
{
    /// <summary>
    /// Records a kernel execution event.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="executionTime">The execution time.</param>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="success">Whether the execution was successful.</param>
    /// <param name="metadata">Additional metadata.</param>
    public abstract void RecordKernelExecution(string kernelName, TimeSpan executionTime, string deviceId, bool success, Dictionary<string, object> metadata);

    /// <summary>
    /// Records a memory operation event.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <param name="bytes">The number of bytes.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="success">Whether the operation was successful.</param>
    public abstract void RecordMemoryOperation(string operationType, long bytes, TimeSpan duration, string deviceId, bool success);

    /// <summary>
    /// Gets system health metrics.
    /// </summary>
    /// <returns>The system health metrics.</returns>
    public abstract SystemHealthMetrics GetSystemHealth();

    /// <summary>
    /// Exports telemetry data.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public abstract Task ExportTelemetryAsync(TelemetryExportFormat format, CancellationToken cancellationToken);

    /// <summary>
    /// Disposes the telemetry provider.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by the telemetry provider.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        // Default implementation - derived classes can override
    }
}

/// <summary>
/// Abstract base class for distributed tracers.
/// </summary>
public abstract class DistributedTracer : IDisposable
{
    /// <summary>
    /// Starts a new trace.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="parentContext">The parent context.</param>
    /// <param name="tags">Additional tags.</param>
    /// <returns>The trace context.</returns>
    public abstract Context.TraceContext StartTrace(string operationName, string? correlationId, object? parentContext, Dictionary<string, object?>? tags);

    /// <summary>
    /// Finishes a trace.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="status">The trace status.</param>
    /// <returns>The trace data.</returns>
    public abstract Task<Traces.TraceData?> FinishTraceAsync(string correlationId, TraceStatus status);

    /// <summary>
    /// Disposes the distributed tracer.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by the distributed tracer.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        // Default implementation - derived classes can override
    }
}

/// <summary>
/// Abstract base class for performance profilers.
/// </summary>
public abstract class PerformanceProfiler : IDisposable
{
    /// <summary>
    /// Creates a performance profile.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="options">The profile options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The performance profile.</returns>
    public abstract Task<Profiles.PerformanceProfile> CreateProfileAsync(string correlationId, ProfileOptions? options, CancellationToken cancellationToken);

    /// <summary>
    /// Disposes the performance profiler.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by the performance profiler.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        // Default implementation - derived classes can override
    }
}

/// <summary>
/// Abstract base class for structured loggers.
/// </summary>
public abstract class StructuredLogger : IDisposable
{
    /// <summary>
    /// Logs a kernel execution event.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="executionTime">The execution time.</param>
    /// <param name="metrics">The performance metrics.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="exception">The exception, if any.</param>
    public abstract void LogKernelExecution(string kernelName, string deviceId, TimeSpan executionTime, Logging.KernelPerformanceMetrics metrics, string? correlationId, Exception? exception);

    /// <summary>
    /// Logs a memory operation event.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="bytes">The number of bytes.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="metrics">The memory metrics.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="exception">The exception, if any.</param>
    public abstract void LogMemoryOperation(string operationType, string deviceId, long bytes, TimeSpan duration, Logging.MemoryAccessMetrics metrics, string? correlationId, Exception? exception);

    /// <summary>
    /// Disposes the structured logger.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by the structured logger.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        // Default implementation - derived classes can override
    }
}
