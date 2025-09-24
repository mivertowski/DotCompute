// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Configuration options for Metal production logging
/// </summary>
public sealed class MetalLoggingOptions
{
    /// <summary>
    /// Gets or sets whether to enable buffering
    /// </summary>
    public bool EnableBuffering { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable correlation tracking
    /// </summary>
    public bool EnableCorrelationTracking { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable performance logging
    /// </summary>
    public bool EnablePerformanceLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable stack trace logging
    /// </summary>
    public bool EnableStackTraceLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to use JSON format
    /// </summary>
    public bool UseJsonFormat { get; set; } = false;

    /// <summary>
    /// Gets or sets the buffer flush interval
    /// </summary>
    public TimeSpan BufferFlushInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the maximum buffer size
    /// </summary>
    public int MaxBufferSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the slow operation threshold in milliseconds
    /// </summary>
    public double SlowOperationThresholdMs { get; set; } = 100.0;

    /// <summary>
    /// Gets or sets the external log endpoints
    /// </summary>
    public List<string>? ExternalLogEndpoints { get; set; }
}