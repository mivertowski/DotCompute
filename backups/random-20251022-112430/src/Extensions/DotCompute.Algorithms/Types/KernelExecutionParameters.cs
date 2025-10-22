#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types;


/// <summary>
/// Configuration parameters for kernel execution optimization.
/// </summary>
public sealed record KernelExecutionParameters
{
    /// <summary>
    /// Gets or sets the block size for kernel execution.
    /// </summary>
    public int BlockSize { get; init; } = 16;

    /// <summary>
    /// Gets or sets the work group size.
    /// </summary>
    public int WorkGroupSize { get; init; } = 256;

    /// <summary>
    /// Gets or sets the number of threads per block.
    /// </summary>
    public int ThreadsPerBlock { get; init; } = 256;

    /// <summary>
    /// Gets or sets whether to use shared memory.
    /// </summary>
    public bool UseSharedMemory { get; init; } = true;

    /// <summary>
    /// Gets or sets the memory alignment requirement.
    /// </summary>
    public int MemoryAlignment { get; init; } = 16;

    /// <summary>
    /// Gets or sets the optimization level (0-3).
    /// </summary>
    public int OptimizationLevel { get; init; } = 2;

    /// <summary>
    /// Gets or sets additional compilation flags.
    /// </summary>
    public IReadOnlyList<string>? CompilerFlags { get; init; }

    /// <summary>
    /// Creates default parameters optimized for the given operation and hardware.
    /// </summary>
    /// <param name="operation">The linear algebra operation.</param>
    /// <param name="hardwareInfo">Hardware information.</param>
    /// <returns>Optimized kernel execution parameters.</returns>
    public static KernelExecutionParameters CreateOptimized(LinearAlgebraOperation operation, HardwareInfo hardwareInfo)
    {
        return operation switch
        {
            LinearAlgebraOperation.MatrixMultiply => new KernelExecutionParameters
            {
                BlockSize = Math.Min(32, hardwareInfo.MaxWorkGroupSize / 8),
                WorkGroupSize = Math.Min(256, hardwareInfo.MaxWorkGroupSize),
                UseSharedMemory = true,
                OptimizationLevel = 3
            },
            LinearAlgebraOperation.QRDecomposition => new KernelExecutionParameters
            {
                BlockSize = 16,
                WorkGroupSize = Math.Min(128, hardwareInfo.MaxWorkGroupSize),
                OptimizationLevel = 2
            },
            LinearAlgebraOperation.SVD => new KernelExecutionParameters
            {
                BlockSize = 8,
                WorkGroupSize = Math.Min(64, hardwareInfo.MaxWorkGroupSize),
                OptimizationLevel = 2
            },
            _ => new KernelExecutionParameters()
        };
    }
}
