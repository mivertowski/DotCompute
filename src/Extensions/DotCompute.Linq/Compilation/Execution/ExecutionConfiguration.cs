// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Compilation.Execution;
/// <summary>
/// Represents execution configuration for a compute stage.
/// </summary>
/// <remarks>
/// This class encapsulates the parameters needed to launch a kernel on the GPU,
/// including grid and block dimensions, shared memory requirements, and additional
/// configuration parameters.
/// </remarks>
public class ExecutionConfiguration
{
    /// <summary>
    /// Gets or sets the grid dimensions.
    /// </summary>
    /// <value>
    /// A tuple specifying the grid dimensions (X, Y, Z) for kernel launch.
    /// Default is (1, 1, 1).
    /// </value>
    /// <remarks>
    /// Grid dimensions determine how many blocks will be launched.
    /// The total number of blocks is X * Y * Z.
    /// </remarks>
    public (int X, int Y, int Z) GridDimensions { get; set; } = (1, 1, 1);
    /// Gets or sets the block dimensions.
    /// A tuple specifying the block dimensions (X, Y, Z) for kernel launch.
    /// Block dimensions determine how many threads are in each block.
    /// The total number of threads per block is X * Y * Z.
    public (int X, int Y, int Z) BlockDimensions { get; set; } = (1, 1, 1);
    /// Gets or sets the shared memory size in bytes.
    /// The amount of shared memory to allocate per block, in bytes.
    /// Default is 0.
    /// Shared memory is fast on-chip memory that can be used for communication
    /// and data sharing between threads within the same block.
    public int SharedMemorySize { get; set; }
    /// Gets or sets additional configuration parameters.
    /// A dictionary containing additional parameters that may be specific
    /// to certain kernel types or execution environments.
    /// This dictionary can contain platform-specific settings, optimization hints,
    /// or other configuration data needed by the kernel launcher.
    public Dictionary<string, object> Parameters { get; set; } = [];
    /// Gets the total number of threads that will be launched.
    /// The total number of threads across all blocks and grids.
    public long TotalThreads
        => (long)GridDimensions.X * GridDimensions.Y * GridDimensions.Z *
        BlockDimensions.X * BlockDimensions.Y * BlockDimensions.Z;
    /// Gets the total number of blocks that will be launched.
    /// The total number of blocks in the grid.
    public long TotalBlocks
        => (long)GridDimensions.X * GridDimensions.Y * GridDimensions.Z;
}
