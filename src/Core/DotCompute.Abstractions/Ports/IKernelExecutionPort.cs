// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;

namespace DotCompute.Abstractions.Ports;

/// <summary>
/// Port interface for kernel execution operations.
/// Part of hexagonal architecture - defines the contract that backend adapters must implement.
/// </summary>
public interface IKernelExecutionPort
{
    /// <summary>
    /// Executes a compiled kernel.
    /// </summary>
    /// <param name="kernel">The compiled kernel to execute.</param>
    /// <param name="configuration">Execution configuration.</param>
    /// <param name="arguments">Kernel arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result with timing information.</returns>
    public ValueTask<KernelExecutionResult> ExecuteAsync(
        ICompiledKernel kernel,
        ExecutionConfiguration configuration,
        KernelArguments arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the optimal execution configuration for a kernel.
    /// </summary>
    /// <param name="kernel">The kernel to analyze.</param>
    /// <param name="dataSize">The size of the data to process.</param>
    /// <returns>Recommended execution configuration.</returns>
    public ExecutionConfiguration GetOptimalConfiguration(
        ICompiledKernel kernel,
        int dataSize);

    /// <summary>
    /// Synchronizes all pending operations.
    /// </summary>
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets execution capabilities.
    /// </summary>
    public ExecutionCapabilities Capabilities { get; }
}

/// <summary>
/// Configuration for kernel execution.
/// </summary>
public sealed record ExecutionConfiguration
{
    /// <summary>Grid dimensions (number of blocks).</summary>
    public required Dim3 GridDim { get; init; }

    /// <summary>Block dimensions (threads per block).</summary>
    public required Dim3 BlockDim { get; init; }

    /// <summary>Shared memory size in bytes.</summary>
    public int SharedMemoryBytes { get; init; }

    /// <summary>Stream/queue index for async execution.</summary>
    public int StreamIndex { get; init; }

    /// <summary>Creates a 1D configuration.</summary>
    public static ExecutionConfiguration Create1D(int gridSize, int blockSize) =>
        new()
        {
            GridDim = new Dim3(gridSize, 1, 1),
            BlockDim = new Dim3(blockSize, 1, 1)
        };

    /// <summary>Creates a 2D configuration.</summary>
    public static ExecutionConfiguration Create2D(int gridX, int gridY, int blockX, int blockY) =>
        new()
        {
            GridDim = new Dim3(gridX, gridY, 1),
            BlockDim = new Dim3(blockX, blockY, 1)
        };
}

/// <summary>
/// 3D dimension specification.
/// </summary>
/// <param name="X">X dimension.</param>
/// <param name="Y">Y dimension.</param>
/// <param name="Z">Z dimension.</param>
public readonly record struct Dim3(int X, int Y, int Z)
{
    /// <summary>Total number of elements.</summary>
    public int Total => X * Y * Z;

    /// <summary>Creates a 1D dimension.</summary>
    public static Dim3 Create1D(int x) => new(x, 1, 1);
}

/// <summary>
/// Result of kernel execution.
/// </summary>
public sealed record KernelExecutionResult
{
    /// <summary>Whether execution succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Execution duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>GPU-side duration if available.</summary>
    public TimeSpan? GpuDuration { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static KernelExecutionResult Succeeded(TimeSpan duration, TimeSpan? gpuDuration = null) =>
        new() { Success = true, Duration = duration, GpuDuration = gpuDuration };

    /// <summary>Creates a failed result.</summary>
    public static KernelExecutionResult Failed(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Execution capabilities of a backend.
/// </summary>
public sealed record ExecutionCapabilities
{
    /// <summary>Maximum threads per block.</summary>
    public int MaxThreadsPerBlock { get; init; }

    /// <summary>Maximum grid dimensions.</summary>
    public Dim3 MaxGridDim { get; init; }

    /// <summary>Maximum shared memory per block.</summary>
    public int MaxSharedMemoryPerBlock { get; init; }

    /// <summary>Number of concurrent streams/queues.</summary>
    public int MaxConcurrentStreams { get; init; }

    /// <summary>Supports cooperative kernel launch.</summary>
    public bool SupportsCooperativeLaunch { get; init; }

    /// <summary>Supports dynamic parallelism.</summary>
    public bool SupportsDynamicParallelism { get; init; }
}
