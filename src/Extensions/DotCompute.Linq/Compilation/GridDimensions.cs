using System;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Represents GPU grid and block dimensions for kernel launch.
/// </summary>
/// <remarks>
/// GPU kernels execute in a 3D grid of thread blocks:
/// - Grid: Number of blocks in each dimension (X, Y, Z)
/// - Block: Number of threads per block in each dimension (X, Y, Z)
/// - Total threads: GridX * GridY * GridZ * BlockX * BlockY * BlockZ
///
/// Optimal dimensions depend on:
/// - GPU architecture (warp size, max threads per block)
/// - Data size and memory access patterns
/// - Kernel complexity and register usage
/// </remarks>
public sealed class GridDimensions
{
    /// <summary>
    /// Number of blocks in X dimension (horizontal).
    /// </summary>
    public int GridX { get; init; }

    /// <summary>
    /// Number of blocks in Y dimension (vertical).
    /// </summary>
    public int GridY { get; init; }

    /// <summary>
    /// Number of blocks in Z dimension (depth).
    /// </summary>
    public int GridZ { get; init; }

    /// <summary>
    /// Number of threads per block in X dimension.
    /// </summary>
    public int BlockX { get; init; }

    /// <summary>
    /// Number of threads per block in Y dimension.
    /// </summary>
    public int BlockY { get; init; }

    /// <summary>
    /// Number of threads per block in Z dimension.
    /// </summary>
    public int BlockZ { get; init; }

    /// <summary>
    /// Total number of threads launched (grid * block).
    /// </summary>
    public long TotalThreads => (long)GridX * GridY * GridZ * BlockX * BlockY * BlockZ;

    /// <summary>
    /// Total number of blocks launched.
    /// </summary>
    public int TotalBlocks => GridX * GridY * GridZ;

    /// <summary>
    /// Threads per block.
    /// </summary>
    public int ThreadsPerBlock => BlockX * BlockY * BlockZ;

    /// <summary>
    /// Creates grid dimensions for 1D data (most common case).
    /// </summary>
    /// <param name="dataSize">Number of elements to process.</param>
    /// <param name="threadsPerBlock">Threads per block (typically 256 or 512).</param>
    /// <returns>Optimized 1D grid dimensions.</returns>
    public static GridDimensions Create1D(int dataSize, int threadsPerBlock = 256)
    {
        if (dataSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(dataSize), "Data size must be positive");
        if (threadsPerBlock <= 0 || threadsPerBlock > 1024)
            throw new ArgumentOutOfRangeException(nameof(threadsPerBlock), "Threads per block must be 1-1024");

        // Round up to ensure we have enough threads
        int numBlocks = (dataSize + threadsPerBlock - 1) / threadsPerBlock;

        return new GridDimensions
        {
            GridX = numBlocks,
            GridY = 1,
            GridZ = 1,
            BlockX = threadsPerBlock,
            BlockY = 1,
            BlockZ = 1
        };
    }

    /// <summary>
    /// Creates grid dimensions for 2D data (e.g., images, matrices).
    /// </summary>
    /// <param name="width">Width of 2D data.</param>
    /// <param name="height">Height of 2D data.</param>
    /// <param name="blockSize">Block dimensions (typically 16x16 or 32x32).</param>
    /// <returns>Optimized 2D grid dimensions.</returns>
    public static GridDimensions Create2D(int width, int height, int blockSize = 16)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");
        if (blockSize <= 0 || blockSize > 32)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be 1-32");

        int gridX = (width + blockSize - 1) / blockSize;
        int gridY = (height + blockSize - 1) / blockSize;

        return new GridDimensions
        {
            GridX = gridX,
            GridY = gridY,
            GridZ = 1,
            BlockX = blockSize,
            BlockY = blockSize,
            BlockZ = 1
        };
    }

    /// <summary>
    /// Creates grid dimensions with automatic optimization for the target backend.
    /// </summary>
    /// <param name="dataSize">Number of elements to process.</param>
    /// <param name="maxThreadsPerBlock">Maximum threads per block (GPU-specific).</param>
    /// <param name="warpSize">Warp/wavefront size (32 for NVIDIA, 64 for AMD).</param>
    /// <returns>Optimized grid dimensions based on hardware characteristics.</returns>
    public static GridDimensions CreateOptimized(int dataSize, int maxThreadsPerBlock = 1024, int warpSize = 32)
    {
        if (dataSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(dataSize), "Data size must be positive");

        // Use multiple of warp size for efficient execution
        int threadsPerBlock = Math.Min(256, maxThreadsPerBlock);
        threadsPerBlock = (threadsPerBlock / warpSize) * warpSize; // Round down to warp multiple

        return Create1D(dataSize, threadsPerBlock);
    }

    /// <summary>
    /// Returns a string representation of the grid dimensions.
    /// </summary>
    public override string ToString() => $"Grid({GridX}, {GridY}, {GridZ}) Block({BlockX}, {BlockY}, {BlockZ}) = {TotalThreads:N0} threads";
}
