// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Tuning;

/// <summary>
/// Interface for kernel auto-tuning services.
/// </summary>
/// <remarks>
/// <para>
/// Provides automatic optimization of kernel launch parameters based on
/// workload characteristics, hardware capabilities, and performance history.
/// </para>
/// </remarks>
public interface IKernelAutoTuner : IAsyncDisposable
{
    /// <summary>
    /// Gets the optimal launch configuration for a kernel.
    /// </summary>
    /// <param name="kernelId">Unique kernel identifier.</param>
    /// <param name="workloadSize">Total number of work items.</param>
    /// <param name="device">Target execution device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Optimal launch configuration.</returns>
    ValueTask<KernelLaunchConfig> GetOptimalConfigAsync(
        string kernelId,
        long workloadSize,
        IAccelerator device,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an execution measurement for tuning purposes.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="config">Configuration that was used.</param>
    /// <param name="workloadSize">Workload size.</param>
    /// <param name="executionTime">Measured execution time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask RecordExecutionAsync(
        string kernelId,
        KernelLaunchConfig config,
        long workloadSize,
        TimeSpan executionTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects the optimal backend for a given workload.
    /// </summary>
    /// <param name="workloadSize">Size of the workload.</param>
    /// <param name="availableDevices">Available devices to choose from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recommended device and confidence score.</returns>
    ValueTask<(IAccelerator Device, double Confidence)> SelectBackendAsync(
        long workloadSize,
        IAccelerator[] availableDevices,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached tuning data for a kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier to invalidate.</param>
    void InvalidateCache(string kernelId);
}

/// <summary>
/// Configuration for kernel launch.
/// </summary>
public readonly struct KernelLaunchConfig
{
    /// <summary>Gets the number of threads per block/workgroup.</summary>
    public int ThreadsPerBlock { get; init; }

    /// <summary>Gets the number of blocks/workgroups.</summary>
    public int BlockCount { get; init; }

    /// <summary>Gets the shared memory size in bytes.</summary>
    public int SharedMemoryBytes { get; init; }

    /// <summary>Gets the 3D thread block dimensions.</summary>
    public (int X, int Y, int Z) BlockDimensions { get; init; }

    /// <summary>Gets the 3D grid dimensions.</summary>
    public (int X, int Y, int Z) GridDimensions { get; init; }

    /// <summary>
    /// Creates a 1D configuration.
    /// </summary>
    /// <param name="threadsPerBlock">Threads per block.</param>
    /// <param name="blockCount">Number of blocks.</param>
    /// <returns>A new launch configuration.</returns>
    public static KernelLaunchConfig Create1D(int threadsPerBlock, int blockCount)
        => new()
        {
            ThreadsPerBlock = threadsPerBlock,
            BlockCount = blockCount,
            BlockDimensions = (threadsPerBlock, 1, 1),
            GridDimensions = (blockCount, 1, 1)
        };

    /// <summary>
    /// Creates a 2D configuration.
    /// </summary>
    /// <param name="blockWidth">Block width.</param>
    /// <param name="blockHeight">Block height.</param>
    /// <param name="gridWidth">Grid width.</param>
    /// <param name="gridHeight">Grid height.</param>
    /// <returns>A new launch configuration.</returns>
    public static KernelLaunchConfig Create2D(
        int blockWidth, int blockHeight,
        int gridWidth, int gridHeight)
        => new()
        {
            ThreadsPerBlock = blockWidth * blockHeight,
            BlockCount = gridWidth * gridHeight,
            BlockDimensions = (blockWidth, blockHeight, 1),
            GridDimensions = (gridWidth, gridHeight, 1)
        };

    /// <summary>
    /// Creates a 3D configuration.
    /// </summary>
    /// <param name="blockDim">Block dimensions.</param>
    /// <param name="gridDim">Grid dimensions.</param>
    /// <returns>A new launch configuration.</returns>
    public static KernelLaunchConfig Create3D(
        (int X, int Y, int Z) blockDim,
        (int X, int Y, int Z) gridDim)
        => new()
        {
            ThreadsPerBlock = blockDim.X * blockDim.Y * blockDim.Z,
            BlockCount = gridDim.X * gridDim.Y * gridDim.Z,
            BlockDimensions = blockDim,
            GridDimensions = gridDim
        };
}

/// <summary>
/// Options for auto-tuning behavior.
/// </summary>
public sealed class AutoTunerOptions
{
    /// <summary>Gets or sets whether auto-tuning is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the minimum improvement threshold to update a profile.</summary>
    public double ImprovementThreshold { get; set; } = 0.05; // 5%

    /// <summary>Gets or sets the maximum tuning iterations.</summary>
    public int MaxIterations { get; set; } = 50;

    /// <summary>Gets or sets whether to use machine learning for prediction.</summary>
    public bool UseMLPrediction { get; set; }

    /// <summary>Gets or sets whether to automatically re-tune after hardware changes.</summary>
    public bool AutoRetuneOnHardwareChange { get; set; } = true;

    /// <summary>Gets or sets the profile cache expiration.</summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromDays(30);
}
