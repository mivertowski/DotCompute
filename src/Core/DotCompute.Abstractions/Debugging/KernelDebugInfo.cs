// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Performance;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Contains detailed debug information for a kernel execution.
/// </summary>
public sealed class KernelDebugInfo
{
    /// <summary>
    /// Gets the name of the kernel that was debugged.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the accelerator type used for execution.
    /// </summary>
    public AcceleratorType AcceleratorType { get; init; }

    /// <summary>
    /// Gets the time when debugging started.
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the time when debugging ended.
    /// </summary>
    public DateTime EndTime { get; init; }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the execution error if execution failed.
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// Gets the kernel output result.
    /// </summary>
    public object? Output { get; init; }

    /// <summary>
    /// Gets the input validation result.
    /// </summary>
    public InputValidationResult? InputValidation { get; init; }

    /// <summary>
    /// Gets memory usage before execution.
    /// </summary>
    public long MemoryUsageBefore { get; init; }

    /// <summary>
    /// Gets memory usage after execution.
    /// </summary>
    public long MemoryUsageAfter { get; init; }

    /// <summary>
    /// Gets the amount of memory allocated during execution.
    /// </summary>
    public long MemoryAllocated { get; init; }

    /// <summary>
    /// Gets performance metrics for this execution.
    /// </summary>
    public PerformanceMetrics? PerformanceMetrics { get; init; }

    /// <summary>
    /// Gets resource usage information.
    /// </summary>
    public ResourceUsage? ResourceUsage { get; init; }

    /// <summary>
    /// Gets error analysis information if execution failed.
    /// </summary>
    public ErrorAnalysis? ErrorAnalysis { get; init; }

    /// <summary>
    /// Gets additional debug metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}