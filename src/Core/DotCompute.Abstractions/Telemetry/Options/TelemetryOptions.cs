// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Telemetry.Options;

/// <summary>
/// Core telemetry configuration options.
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>
    /// Gets or sets whether telemetry is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the telemetry export interval.
    /// </summary>
    public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum batch size for telemetry exports.
    /// </summary>
    public int MaxBatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the export endpoints.
    /// </summary>
    public IList<string> ExportEndpoints { get; } = [];
}

/// <summary>
/// Distributed tracing configuration options.
/// </summary>
public sealed class DistributedTracingOptions
{
    /// <summary>
    /// Gets or sets whether distributed tracing is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the sampling rate (0.0 to 1.0).
    /// </summary>
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the maximum trace duration.
    /// </summary>
    public TimeSpan MaxTraceDuration { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Performance profiler configuration options.
/// </summary>
public sealed class PerformanceProfilerOptions
{
    /// <summary>
    /// Gets or sets whether performance profiling is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the profiling frequency.
    /// </summary>
    public TimeSpan ProfilingInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets whether to enable detailed profiling.
    /// </summary>
    public bool DetailedProfiling { get; set; }

}

/// <summary>
/// Structured logging configuration options.
/// </summary>
public sealed class StructuredLoggingOptions
{
    /// <summary>
    /// Gets or sets whether structured logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether to include stack traces.
    /// </summary>
    public bool IncludeStackTrace { get; set; }

}

/// <summary>
/// Log buffer configuration options.
/// </summary>
public sealed class LogBufferOptions
{
    /// <summary>
    /// Gets or sets the buffer size.
    /// </summary>
    public int BufferSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the flush interval.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets whether to auto-flush on critical events.
    /// </summary>
    public bool AutoFlushOnCritical { get; set; } = true;
}

/// <summary>
/// Log levels for structured logging.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Trace level logging.
    /// </summary>
    Trace,

    /// <summary>
    /// Debug level logging.
    /// </summary>
    Debug,

    /// <summary>
    /// Information level logging.
    /// </summary>
    Information,

    /// <summary>
    /// Warning level logging.
    /// </summary>
    Warning,

    /// <summary>
    /// Error level logging.
    /// </summary>
    Error,

    /// <summary>
    /// Critical level logging.
    /// </summary>
    Critical
}