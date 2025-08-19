// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Memory
{

/// <summary>
/// Internal interface for memory pools to support Native AOT by avoiding reflection.
/// This interface provides a non-generic way to interact with generic memory pools.
/// </summary>
internal interface IMemoryPoolInternal
{
    /// <summary>
    /// Gets the performance statistics of the memory pool.
    /// </summary>
    /// <returns>The performance statistics.</returns>
    public MemoryPoolPerformanceStats GetPerformanceStats();

    /// <summary>
    /// Handles memory pressure by releasing unused buffers.
    /// </summary>
    /// <param name="pressure">The memory pressure value between 0.0 and 1.0.</param>
    public void HandleMemoryPressure(double pressure);

    /// <summary>
    /// Compacts the memory pool and releases unused memory.
    /// </summary>
    /// <param name="maxBytesToRelease">The maximum number of bytes to release.</param>
    /// <returns>The number of bytes released.</returns>
    public long Compact(long maxBytesToRelease = long.MaxValue);
}
}
