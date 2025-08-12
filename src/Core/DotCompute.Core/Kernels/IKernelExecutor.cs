// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Interface for executing compiled kernels on accelerators.
/// </summary>
public interface IKernelExecutor
{
    /// <summary>
    /// Gets the accelerator this executor targets.
    /// </summary>
    IAccelerator Accelerator { get; }

    /// <summary>
    /// Executes a compiled kernel.
    /// </summary>
    /// <param name="kernel">The compiled kernel to execute.</param>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="executionConfig">The execution configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result.</returns>
    ValueTask<KernelExecutionResult> ExecuteAsync(
        CompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig executionConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a kernel and waits for completion.
    /// </summary>
    /// <param name="kernel">The compiled kernel to execute.</param>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="executionConfig">The execution configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result.</returns>
    ValueTask<KernelExecutionResult> ExecuteAndWaitAsync(
        CompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig executionConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a kernel for execution without waiting.
    /// </summary>
    /// <param name="kernel">The compiled kernel to execute.</param>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="executionConfig">The execution configuration.</param>
    /// <returns>An execution handle to track the operation.</returns>
    KernelExecutionHandle EnqueueExecution(
        CompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig executionConfig);

    /// <summary>
    /// Waits for a kernel execution to complete.
    /// </summary>
    /// <param name="handle">The execution handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result.</returns>
    ValueTask<KernelExecutionResult> WaitForCompletionAsync(
        KernelExecutionHandle handle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the optimal execution configuration for a kernel.
    /// </summary>
    /// <param name="kernel">The compiled kernel.</param>
    /// <param name="problemSize">The problem size.</param>
    /// <returns>Optimal execution configuration.</returns>
    KernelExecutionConfig GetOptimalExecutionConfig(CompiledKernel kernel, int[] problemSize);

    /// <summary>
    /// Profiles kernel execution.
    /// </summary>
    /// <param name="kernel">The compiled kernel to profile.</param>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="executionConfig">The execution configuration.</param>
    /// <param name="iterations">Number of iterations to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Profiling results.</returns>
    ValueTask<KernelProfilingResult> ProfileAsync(
        CompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig executionConfig,
        int iterations = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a kernel argument.
/// </summary>
public sealed class KernelArgument
{
    /// <summary>
    /// Gets or sets the argument name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the argument value.
    /// </summary>
    public required object Value { get; init; }

    /// <summary>
    /// Gets or sets the argument type.
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// Gets or sets whether this is a device memory buffer.
    /// </summary>
    public bool IsDeviceMemory { get; init; }

    /// <summary>
    /// Gets or sets the memory buffer if applicable.
    /// </summary>
    public IMemoryBuffer? MemoryBuffer { get; init; }

    /// <summary>
    /// Gets or sets the size in bytes for raw buffers.
    /// </summary>
    public long SizeInBytes { get; init; }
}

/// <summary>
/// Kernel execution configuration.
/// </summary>
public sealed class KernelExecutionConfig
{
    /// <summary>
    /// Gets or sets the global work size (total threads).
    /// </summary>
    public required int[] GlobalWorkSize { get; init; }

    /// <summary>
    /// Gets or sets the local work size (threads per work group).
    /// </summary>
    public int[]? LocalWorkSize { get; init; }

    /// <summary>
    /// Gets or sets the dynamic shared memory size in bytes.
    /// </summary>
    public int DynamicSharedMemorySize { get; init; }

    /// <summary>
    /// Gets or sets the execution stream/queue.
    /// </summary>
    public object? Stream { get; init; }

    /// <summary>
    /// Gets or sets execution flags.
    /// </summary>
    public KernelExecutionFlags Flags { get; init; } = KernelExecutionFlags.None;

    /// <summary>
    /// Gets or sets events to wait for before execution.
    /// </summary>
    public object[]? WaitEvents { get; init; }

    /// <summary>
    /// Gets or sets whether to capture timing information.
    /// </summary>
    public bool CaptureTimings { get; init; }
}

/// <summary>
/// Kernel execution flags.
/// </summary>
[Flags]
public enum KernelExecutionFlags
{
    /// <summary>
    /// No special flags
    /// </summary>
    None = 0,

    /// <summary>
    /// Prefer shared memory over L1 cache
    /// </summary>
    PreferSharedMemory = 1,

    /// <summary>
    /// Prefer L1 cache over shared memory
    /// </summary>
    PreferL1Cache = 2,

    /// <summary>
    /// Disable caching for global memory reads
    /// </summary>
    DisableCache = 4,

    /// <summary>
    /// Enable cooperative kernel launch
    /// </summary>
    CooperativeKernel = 8,

    /// <summary>
    /// High priority execution
    /// </summary>
    HighPriority = 16,

    /// <summary>
    /// Optimize for throughput over latency
    /// </summary>
    OptimizeForThroughput = 32
}

/// <summary>
/// Handle for tracking kernel execution.
/// </summary>
public sealed class KernelExecutionHandle
{
    /// <summary>
    /// Gets the execution ID.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets the submission timestamp.
    /// </summary>
    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>
    /// Gets the platform-specific event handle.
    /// </summary>
    public object? EventHandle { get; init; }

    /// <summary>
    /// Gets whether the execution has completed.
    /// </summary>
    public bool IsCompleted { get; internal set; }

    /// <summary>
    /// Gets the completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; internal set; }
}

/// <summary>
/// Kernel execution result.
/// </summary>
public sealed class KernelExecutionResult
{
    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the execution handle.
    /// </summary>
    public required KernelExecutionHandle Handle { get; init; }

    /// <summary>
    /// Gets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the execution timings if captured.
    /// </summary>
    public KernelExecutionTimings? Timings { get; init; }

    /// <summary>
    /// Gets performance counters if available.
    /// </summary>
    public Dictionary<string, object>? PerformanceCounters { get; init; }
}

/// <summary>
/// Kernel execution timings.
/// </summary>
public sealed class KernelExecutionTimings
{
    /// <summary>
    /// Gets the kernel execution time in milliseconds.
    /// </summary>
    public required double KernelTimeMs { get; init; }

    /// <summary>
    /// Gets the total time including memory transfers in milliseconds.
    /// </summary>
    public required double TotalTimeMs { get; init; }

    /// <summary>
    /// Gets the memory transfer time in milliseconds.
    /// </summary>
    public double MemoryTransferTimeMs => TotalTimeMs - KernelTimeMs;

    /// <summary>
    /// Gets the queue wait time in milliseconds.
    /// </summary>
    public double QueueWaitTimeMs { get; init; }

    /// <summary>
    /// Gets the effective memory bandwidth in GB/s.
    /// </summary>
    public double EffectiveMemoryBandwidthGBps { get; init; }

    /// <summary>
    /// Gets the effective compute throughput in GFLOPS.
    /// </summary>
    public double EffectiveComputeThroughputGFLOPS { get; init; }
}

/// <summary>
/// Kernel profiling result.
/// </summary>
public sealed class KernelProfilingResult
{
    /// <summary>
    /// Gets the number of iterations profiled.
    /// </summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// Gets the average execution time in milliseconds.
    /// </summary>
    public required double AverageTimeMs { get; init; }

    /// <summary>
    /// Gets the minimum execution time in milliseconds.
    /// </summary>
    public required double MinTimeMs { get; init; }

    /// <summary>
    /// Gets the maximum execution time in milliseconds.
    /// </summary>
    public required double MaxTimeMs { get; init; }

    /// <summary>
    /// Gets the standard deviation in milliseconds.
    /// </summary>
    public required double StdDevMs { get; init; }

    /// <summary>
    /// Gets the median execution time in milliseconds.
    /// </summary>
    public required double MedianTimeMs { get; init; }

    /// <summary>
    /// Gets percentile timings.
    /// </summary>
    public Dictionary<int, double> PercentileTimingsMs { get; init; } = [];

    /// <summary>
    /// Gets the achieved occupancy (0-1).
    /// </summary>
    public double AchievedOccupancy { get; init; }

    /// <summary>
    /// Gets the memory throughput in GB/s.
    /// </summary>
    public double MemoryThroughputGBps { get; init; }

    /// <summary>
    /// Gets the compute throughput in GFLOPS.
    /// </summary>
    public double ComputeThroughputGFLOPS { get; init; }

    /// <summary>
    /// Gets bottleneck analysis.
    /// </summary>
    public BottleneckAnalysis? Bottleneck { get; init; }

    /// <summary>
    /// Gets optimization suggestions.
    /// </summary>
    public List<string> OptimizationSuggestions { get; init; } = [];
}

/// <summary>
/// Bottleneck analysis for kernel execution.
/// </summary>
public sealed class BottleneckAnalysis
{
    /// <summary>
    /// Gets the primary bottleneck type.
    /// </summary>
    public required BottleneckType Type { get; init; }

    /// <summary>
    /// Gets the bottleneck severity (0-1).
    /// </summary>
    public required double Severity { get; init; }

    /// <summary>
    /// Gets detailed analysis.
    /// </summary>
    public required string Details { get; init; }

    /// <summary>
    /// Gets resource utilization percentages.
    /// </summary>
    public Dictionary<string, double> ResourceUtilization { get; init; } = [];
}

/// <summary>
/// Types of performance bottlenecks.
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// Memory bandwidth limited
    /// </summary>
    MemoryBandwidth,

    /// <summary>
    /// Compute limited
    /// </summary>
    Compute,

    /// <summary>
    /// Memory latency limited
    /// </summary>
    MemoryLatency,

    /// <summary>
    /// Instruction issue limited
    /// </summary>
    InstructionIssue,

    /// <summary>
    /// Synchronization overhead
    /// </summary>
    Synchronization,

    /// <summary>
    /// Register pressure
    /// </summary>
    RegisterPressure,

    /// <summary>
    /// Shared memory bank conflicts
    /// </summary>
    SharedMemoryBankConflicts,

    /// <summary>
    /// Warp divergence
    /// </summary>
    WarpDivergence,

    /// <summary>
    /// No significant bottleneck
    /// </summary>
    None
}