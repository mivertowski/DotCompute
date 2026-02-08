// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Attributes;

/// <summary>
/// Instructs the source generator to inject telemetry collection code into a Ring Kernel.
/// </summary>
/// <remarks>
/// When applied to a Ring Kernel class, the source generator will automatically inject
/// performance telemetry collection including:
/// - Execution start/end timestamps
/// - Message processing counts
/// - Memory allocation tracking
/// - Latency measurements
/// - Throughput calculations
///
/// The injected telemetry code has minimal overhead (typically &lt;1% performance impact)
/// and provides detailed insights into kernel performance characteristics.
///
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// [RingKernel(Backend = "CUDA", ExecutionMode = RingKernelExecutionMode.Persistent)]
/// [EnableTelemetry]
/// public class MyActorKernel : IRingKernel
/// {
///     public void Execute()
///     {
///         // Kernel logic - telemetry automatically injected by source generator
///     }
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EnableTelemetryAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a value indicating whether to collect detailed per-message metrics.
    /// </summary>
    /// <remarks>
    /// When true, collects individual message sizes and processing times.
    /// When false, only aggregated metrics are collected.
    /// Default is false for minimal overhead.
    /// </remarks>
    public bool CollectDetailedMetrics { get; set; }

    /// <summary>
    /// Gets or sets the sampling rate for telemetry collection (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// Sampling rate determines what fraction of executions are measured.
    /// - 1.0 = measure every execution (highest accuracy, highest overhead)
    /// - 0.1 = measure 10% of executions (lower overhead, statistically valid)
    /// - 0.01 = measure 1% of executions (minimal overhead, approximate metrics)
    /// Default is 1.0 (100% sampling).
    /// </remarks>
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets a value indicating whether to track memory allocations.
    /// </summary>
    /// <remarks>
    /// When true, monitors memory allocated/deallocated by the kernel.
    /// When false, memory metrics are not collected.
    /// Default is true.
    /// </remarks>
    public bool TrackMemory { get; set; } = true;

    /// <summary>
    /// Gets or sets the telemetry provider type name (optional).
    /// </summary>
    /// <remarks>
    /// Allows specifying a custom telemetry provider implementation.
    /// If null, uses the default IKernelTelemetryProvider from dependency injection.
    /// Must be a fully qualified type name implementing IKernelTelemetryProvider.
    /// </remarks>
    public string? CustomProviderType { get; set; }
}
