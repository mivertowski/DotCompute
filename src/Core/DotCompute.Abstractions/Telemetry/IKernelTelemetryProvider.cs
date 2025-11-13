// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Telemetry;

/// <summary>
/// Provides telemetry collection and reporting for kernel execution.
/// </summary>
/// <remarks>
/// Telemetry providers collect performance metrics such as throughput, latency,
/// memory usage, and execution counts for Ring Kernels and standard kernels.
/// Implementations must be thread-safe for concurrent metric collection.
/// </remarks>
public interface IKernelTelemetryProvider
{
    /// <summary>
    /// Records the start of a kernel execution.
    /// </summary>
    /// <param name="kernelId">Unique identifier for the kernel instance.</param>
    /// <param name="timestamp">High-precision timestamp when execution started.</param>
    public void RecordExecutionStart(string kernelId, long timestamp);

    /// <summary>
    /// Records the completion of a kernel execution.
    /// </summary>
    /// <param name="kernelId">Unique identifier for the kernel instance.</param>
    /// <param name="timestamp">High-precision timestamp when execution completed.</param>
    /// <param name="success">True if execution succeeded, false if it failed.</param>
    public void RecordExecutionEnd(string kernelId, long timestamp, bool success);

    /// <summary>
    /// Records memory allocation for a kernel.
    /// </summary>
    /// <param name="kernelId">Unique identifier for the kernel instance.</param>
    /// <param name="bytes">Number of bytes allocated.</param>
    public void RecordMemoryAllocation(string kernelId, long bytes);

    /// <summary>
    /// Records memory deallocation for a kernel.
    /// </summary>
    /// <param name="kernelId">Unique identifier for the kernel instance.</param>
    /// <param name="bytes">Number of bytes deallocated.</param>
    public void RecordMemoryDeallocation(string kernelId, long bytes);

    /// <summary>
    /// Records a message processed by a Ring Kernel.
    /// </summary>
    /// <param name="kernelId">Unique identifier for the kernel instance.</param>
    /// <param name="messageSize">Size of the message in bytes.</param>
    public void RecordMessageProcessed(string kernelId, int messageSize);

    /// <summary>
    /// Gets the current metrics for a specific kernel.
    /// </summary>
    /// <param name="kernelId">Unique identifier for the kernel instance.</param>
    /// <returns>Current telemetry metrics for the kernel.</returns>
    public TelemetryMetrics GetMetrics(string kernelId);

    /// <summary>
    /// Gets aggregated metrics for all kernels.
    /// </summary>
    /// <returns>Aggregated telemetry metrics across all kernels.</returns>
    public TelemetryMetrics GetAggregatedMetrics();

    /// <summary>
    /// Resets all telemetry data for a specific kernel.
    /// </summary>
    /// <param name="kernelId">Unique identifier for the kernel instance.</param>
    public void ResetMetrics(string kernelId);

    /// <summary>
    /// Resets all telemetry data for all kernels.
    /// </summary>
    public void ResetAllMetrics();
}
