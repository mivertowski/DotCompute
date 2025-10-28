// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry.Analysis;
using DotCompute.Core.Telemetry.Metrics;
using DotCompute.Core.Telemetry.Options;
using DotCompute.Core.Telemetry.Profiles;
using DotCompute.Core.Telemetry.System;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Interface for performance profiler enabling detailed kernel analysis, bottleneck identification, and optimization recommendations.
/// </summary>
public interface IPerformanceProfiler : IDisposable
{
    /// <summary>
    /// Creates a new performance profile with the given correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID for tracking this profile.</param>
    /// <param name="profileOptions">Optional profile configuration options.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous operation with the created performance profile.</returns>
    public Task<PerformanceProfile> CreateProfileAsync(string correlationId, ProfileOptions? profileOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records kernel execution metrics for the given profile.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the profile.</param>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="executionMetrics">Detailed execution metrics.</param>
    public void RecordKernelExecution(string correlationId, string kernelName, string deviceId, KernelExecutionMetrics executionMetrics);

    /// <summary>
    /// Records memory operation metrics for the given profile.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the profile.</param>
    /// <param name="operationType">Type of memory operation (e.g., "Transfer", "Allocation").</param>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="memoryMetrics">Detailed memory operation metrics.</param>
    public void RecordMemoryOperation(string correlationId, string operationType, string deviceId, MemoryOperationMetrics memoryMetrics);

    /// <summary>
    /// Finishes profiling and returns the completed profile with analysis.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the profile to finish.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous operation with the completed performance profile.</returns>
    public Task<PerformanceProfile> FinishProfilingAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes kernel performance based on historical data.
    /// </summary>
    /// <param name="kernelName">The kernel name to analyze.</param>
    /// <param name="timeWindow">Optional time window for the analysis.</param>
    /// <returns>Analysis results for the kernel.</returns>
    public KernelAnalysisResult AnalyzeKernelPerformance(string kernelName, TimeSpan? timeWindow = null);

    /// <summary>
    /// Analyzes memory access patterns from profiling data.
    /// </summary>
    /// <param name="timeWindow">Optional time window for the analysis.</param>
    /// <returns>Memory access analysis results.</returns>
    public MemoryAccessAnalysisResult AnalyzeMemoryAccessPatterns(TimeSpan? timeWindow = null);

    /// <summary>
    /// Gets current system performance snapshot.
    /// </summary>
    /// <returns>System performance snapshot with current metrics.</returns>
    public SystemPerformanceSnapshot GetSystemPerformanceSnapshot();
}
