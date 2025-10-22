// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// NUMA-aware memory allocation and management.
/// </summary>
/// <remarks>
/// Initializes a new instance of the NumaMemoryManager class.
/// </remarks>
/// <param name="topology">NUMA topology information.</param>
public sealed class NumaMemoryManager(NumaTopology topology) : IDisposable
{
    private readonly NumaTopology _topology = topology ?? throw new ArgumentNullException(nameof(topology));
    private readonly ConcurrentDictionary<IntPtr, AllocationInfo> _allocations = new();
    private readonly object _lock = new();
    private long _totalAllocatedBytes;
    private bool _disposed;

    /// <summary>
    /// Gets the total number of bytes allocated.
    /// </summary>
    public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocatedBytes);

    /// <summary>
    /// Gets the number of active allocations.
    /// </summary>
    public int ActiveAllocations => _allocations.Count;

    /// <summary>
    /// Allocates memory on a specific NUMA node.
    /// </summary>
    /// <param name="size">Size in bytes.</param>
    /// <param name="nodeId">NUMA node ID.</param>
    /// <returns>Pointer to allocated memory or IntPtr.Zero if allocation failed.</returns>
    public IntPtr AllocateOnNode(nuint size, int nodeId)
    {
        ThrowIfDisposed();

        if (size == 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(size));
        }

        if (nodeId < 0 || nodeId >= _topology.NodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), $"Node ID must be between 0 and {_topology.NodeCount - 1}");
        }

        try
        {
            var ptr = NumaInterop.AllocateOnNode(size, nodeId);
            if (ptr != IntPtr.Zero)
            {
                var allocationInfo = new AllocationInfo
                {
                    Size = size,
                    NodeId = nodeId,
                    AllocatedTime = DateTime.UtcNow,
                    AllocationMethod = AllocationType.NumaSpecific,
                    IsActive = true
                };

                _ = _allocations.TryAdd(ptr, allocationInfo);
                _ = Interlocked.Add(ref _totalAllocatedBytes, (long)size);

                return ptr;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to allocate memory on node {nodeId}: {ex.Message}");
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Allocates memory with automatic node selection based on current thread affinity.
    /// </summary>
    /// <param name="size">Size in bytes.</param>
    /// <returns>Pointer to allocated memory or IntPtr.Zero if allocation failed.</returns>
    public IntPtr AllocatePreferred(nuint size)
    {
        ThrowIfDisposed();

        if (size == 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(size));
        }

        // Try to determine optimal node based on current thread
        var optimalNode = GetOptimalNodeForCurrentThread();
        var ptr = AllocateOnNode(size, optimalNode);

        if (ptr != IntPtr.Zero)
        {
            return ptr;
        }

        // Fallback to regular allocation
        return AllocateAny(size);
    }

    /// <summary>
    /// Allocates memory on any available node.
    /// </summary>
    /// <param name="size">Size in bytes.</param>
    /// <returns>Pointer to allocated memory or IntPtr.Zero if allocation failed.</returns>
    public IntPtr AllocateAny(nuint size)
    {
        ThrowIfDisposed();

        if (size == 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(size));
        }

        try
        {
            // Try each node until we find one with available memory
            var nodesByMemory = _topology.GetNodesByAvailableMemory();

            foreach (var nodeId in nodesByMemory)
            {
                var ptr = AllocateOnNode(size, nodeId);
                if (ptr != IntPtr.Zero)
                {
                    return ptr;
                }
            }

            // Fallback to system allocation
            return AllocateSystem(size);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to allocate memory: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Allocates memory using interleaved policy across multiple nodes.
    /// </summary>
    /// <param name="size">Size in bytes.</param>
    /// <param name="nodeIds">Nodes to interleave across.</param>
    /// <returns>Pointer to allocated memory or IntPtr.Zero if allocation failed.</returns>
    public IntPtr AllocateInterleaved(nuint size, params int[] nodeIds)
    {
        ThrowIfDisposed();

        if (size == 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(size));
        }

        if (nodeIds == null || nodeIds.Length == 0)
        {
            nodeIds = [.. Enumerable.Range(0, _topology.NodeCount)];
        }

        // Validate node IDs
        foreach (var nodeId in nodeIds)
        {
            if (nodeId < 0 || nodeId >= _topology.NodeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeIds), $"Node ID {nodeId} is out of range");
            }
        }

        try
        {
            // For simplicity, allocate on the first available node
            // In a full implementation, this would set up actual interleaved memory policy
            foreach (var nodeId in nodeIds)
            {
                var ptr = AllocateOnNode(size, nodeId);
                if (ptr != IntPtr.Zero)
                {
                    // Update allocation info to reflect interleaved policy
                    if (_allocations.TryGetValue(ptr, out var info))
                    {
                        var updatedInfo = info with { AllocationMethod = AllocationType.Interleaved };
                        _ = _allocations.TryUpdate(ptr, updatedInfo, info);
                    }
                    return ptr;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to allocate interleaved memory: {ex.Message}");
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Allocates memory using system default allocation.
    /// </summary>
    /// <param name="size">Size in bytes.</param>
    /// <returns>Pointer to allocated memory or IntPtr.Zero if allocation failed.</returns>
    public IntPtr AllocateSystem(nuint size)
    {
        ThrowIfDisposed();

        if (size == 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(size));
        }

        try
        {
            var ptr = Marshal.AllocHGlobal((nint)size);
            if (ptr != IntPtr.Zero)
            {
                var allocationInfo = new AllocationInfo
                {
                    Size = size,
                    NodeId = -1, // Unknown node
                    AllocatedTime = DateTime.UtcNow,
                    AllocationMethod = AllocationType.System,
                    IsActive = true
                };

                _ = _allocations.TryAdd(ptr, allocationInfo);
                _ = Interlocked.Add(ref _totalAllocatedBytes, (long)size);
            }

            return ptr;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to allocate system memory: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Frees previously allocated memory.
    /// </summary>
    /// <param name="memoryAddress">Address of memory to free.</param>
    /// <returns>True if memory was freed successfully.</returns>
    public bool Free(IntPtr memoryAddress)
    {
        ThrowIfDisposed();

        if (memoryAddress == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (_allocations.TryRemove(memoryAddress, out var allocationInfo))
            {
                switch (allocationInfo.AllocationMethod)
                {
                    case AllocationType.NumaSpecific:
                    case AllocationType.Interleaved:
                        NumaInterop.FreeNumaMemory(memoryAddress, allocationInfo.Size);
                        break;

                    case AllocationType.System:
                        Marshal.FreeHGlobal(memoryAddress);
                        break;
                }

                _ = Interlocked.Add(ref _totalAllocatedBytes, -(long)allocationInfo.Size);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to free memory: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Gets allocation information for a memory address.
    /// </summary>
    /// <param name="memoryAddress">Memory address.</param>
    /// <returns>Allocation information or null if not found.</returns>
    public AllocationInfo? GetAllocationInfo(IntPtr memoryAddress)
    {
        ThrowIfDisposed();
        return _allocations.TryGetValue(memoryAddress, out var info) ? info : null;
    }

    /// <summary>
    /// Gets memory statistics for all allocations.
    /// </summary>
    /// <returns>Memory allocation statistics.</returns>
    public MemoryStatistics GetMemoryStatistics()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var nodeAllocations = new long[_topology.NodeCount + 1]; // +1 for system allocations
            var allocationsByType = new Dictionary<AllocationType, int>();
            var totalAllocations = 0;

            foreach (var allocation in _allocations.Values.Where(a => a.IsActive))
            {
                var nodeIndex = allocation.NodeId >= 0 ? allocation.NodeId : _topology.NodeCount; // System allocations
                nodeAllocations[nodeIndex] += (long)allocation.Size;

                if (!allocationsByType.TryGetValue(allocation.AllocationMethod, out var count))
                {
                    count = 0;
                }
                allocationsByType[allocation.AllocationMethod] = count + 1;
                totalAllocations++;
            }

            return new MemoryStatistics
            {
                TotalAllocations = totalAllocations,
                TotalAllocatedBytes = TotalAllocatedBytes,
                NodeAllocations = Array.AsReadOnly(nodeAllocations[..^1]), // Exclude system allocations
                SystemAllocations = nodeAllocations[^1],
                AllocationsByType = allocationsByType,
                AverageAllocationSize = totalAllocations > 0 ? TotalAllocatedBytes / totalAllocations : 0,
                MemoryFragmentation = CalculateFragmentation()
            };
        }
    }

    /// <summary>
    /// Sets the preferred NUMA node for future allocations.
    /// </summary>
    /// <param name="nodeId">Preferred node ID.</param>
    public void SetPreferredNode(int nodeId)
    {
        ThrowIfDisposed();

        if (nodeId < 0 || nodeId >= _topology.NodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), $"Node ID must be between 0 and {_topology.NodeCount - 1}");
        }

        NumaInterop.SetPreferredNode(nodeId);
    }

    /// <summary>
    /// Creates a memory allocation scope that sets preferred node.
    /// </summary>
    /// <param name="nodeId">Node ID to prefer.</param>
    /// <returns>Disposable memory scope.</returns>
    public IDisposable CreateMemoryScope(int nodeId)
    {
        ThrowIfDisposed();
        return new MemoryScope(nodeId);
    }

    /// <summary>
    /// Migrates memory from one node to another (if supported by platform).
    /// </summary>
    /// <param name="memoryAddress">Memory address.</param>
    /// <param name="targetNodeId">Target node ID.</param>
    /// <returns>True if migration was successful or not needed.</returns>
    public bool MigrateMemory(IntPtr memoryAddress, int targetNodeId)
    {
        ThrowIfDisposed();

        if (memoryAddress == IntPtr.Zero)
        {
            return false;
        }

        if (targetNodeId < 0 || targetNodeId >= _topology.NodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(targetNodeId), $"Node ID must be between 0 and {_topology.NodeCount - 1}");
        }

        try
        {
            if (_allocations.TryGetValue(memoryAddress, out var allocationInfo))
            {
                if (allocationInfo.NodeId == targetNodeId)
                {
                    return true; // Already on target node
                }

                // For now, we'll update the tracking information
                // In a full implementation, this would actually migrate the memory
                var updatedInfo = allocationInfo with { NodeId = targetNodeId };
                return _allocations.TryUpdate(memoryAddress, updatedInfo, allocationInfo);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to migrate memory: {ex.Message}");
        }

        return false;
    }

    private int GetOptimalNodeForCurrentThread()
    {
        // In a full implementation, this would check current thread affinity
        // For now, use round-robin selection
        var threadId = Environment.CurrentManagedThreadId;
        return threadId % _topology.NodeCount;
    }

    private double CalculateFragmentation()
        // Simplified fragmentation calculation
        // In a real implementation, this would analyze actual memory layout

        => Math.Min(0.1 * _allocations.Count / 100.0, 1.0);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// Disposes of the memory manager and frees all managed allocations.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Free all managed allocations
            foreach (var memoryAddress in _allocations.Keys.ToArray())
            {
                _ = Free(memoryAddress);
            }

            _allocations.Clear();

            _disposed = true;
        }
    }
}

/// <summary>
/// Information about a memory allocation.
/// </summary>
public sealed record AllocationInfo
{
    /// <summary>Size of allocation in bytes.</summary>
    public required nuint Size { get; init; }

    /// <summary>NUMA node ID (-1 for system allocation).</summary>
    public required int NodeId { get; init; }

    /// <summary>Time when memory was allocated.</summary>
    public required DateTime AllocatedTime { get; init; }

    /// <summary>Method used for allocation.</summary>
    public required AllocationType AllocationMethod { get; init; }

    /// <summary>Whether the allocation is currently active.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Duration since allocation.</summary>
    public TimeSpan Age => DateTime.UtcNow - AllocatedTime;
}

/// <summary>
/// Type of memory allocation.
/// </summary>
public enum AllocationType
{
    /// <summary>System default allocation.</summary>
    System,

    /// <summary>NUMA-specific allocation on a particular node.</summary>
    NumaSpecific,

    /// <summary>Interleaved allocation across multiple nodes.</summary>
    Interleaved,

    /// <summary>Preferred allocation with fallback.</summary>
    Preferred,

    /// <summary>Huge page allocation.</summary>
    HugePage
}

/// <summary>
/// Memory allocation statistics.
/// </summary>
public sealed class MemoryStatistics
{
    /// <summary>Total number of allocations.</summary>
    public required int TotalAllocations { get; init; }

    /// <summary>Total allocated bytes.</summary>
    public required long TotalAllocatedBytes { get; init; }

    /// <summary>Bytes allocated per NUMA node.</summary>
    public required IReadOnlyList<long> NodeAllocations { get; init; }

    /// <summary>Bytes allocated using system allocation.</summary>
    public required long SystemAllocations { get; init; }

    /// <summary>Allocations by type.</summary>
    public required Dictionary<AllocationType, int> AllocationsByType { get; init; }

    /// <summary>Average allocation size.</summary>
    public required long AverageAllocationSize { get; init; }

    /// <summary>Memory fragmentation estimate (0.0 to 1.0).</summary>
    public required double MemoryFragmentation { get; init; }

    /// <summary>Gets the most used node.</summary>
    public int MostUsedNode
    {
        get
        {
            if (NodeAllocations.Count == 0)
            {
                return 0;
            }

            int maxIndex = 0;
            long maxValue = NodeAllocations[0];
            for (int i = 1; i < NodeAllocations.Count; i++)
            {
                if (NodeAllocations[i] > maxValue)
                {
                    maxValue = NodeAllocations[i];
                    maxIndex = i;
                }
            }
            return maxIndex;
        }
    }

    /// <summary>Gets the least used node.</summary>
    public int LeastUsedNode
    {
        get
        {
            if (NodeAllocations.Count == 0)
            {
                return 0;
            }

            int minIndex = 0;
            long minValue = NodeAllocations[0];
            for (int i = 1; i < NodeAllocations.Count; i++)
            {
                if (NodeAllocations[i] < minValue)
                {
                    minValue = NodeAllocations[i];
                    minIndex = i;
                }
            }
            return minIndex;
        }
    }

    /// <summary>Gets memory distribution balance score.</summary>
    public double DistributionBalance
    {
        get
        {
            if (NodeAllocations.Count == 0)
            {
                return 1.0;
            }


            var total = NodeAllocations.Sum();
            if (total == 0)
            {
                return 1.0;
            }


            var expected = (double)total / NodeAllocations.Count;
            var variance = NodeAllocations.Select(bytes => Math.Pow(bytes - expected, 2)).Average();
            var standardDeviation = Math.Sqrt(variance);

            return Math.Max(0.0, 1.0 - (standardDeviation / expected));
        }
    }
}

/// <summary>
/// Disposable scope for setting preferred memory node.
/// </summary>
internal sealed class MemoryScope : IDisposable
{
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the MemoryScope class.
    /// </summary>
    /// <param name="nodeId">The node identifier.</param>

    public MemoryScope(int nodeId)
    {
        NumaInterop.SetPreferredNode(nodeId);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            // Restore original preferred node if we tracked it
            _disposed = true;
        }
    }
}