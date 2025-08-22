// <copyright file="MemoryStatisticsImpl.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Results;

namespace DotCompute.Memory.Internal;

/// <summary>
/// Internal implementation of memory statistics.
/// </summary>
internal sealed class MemoryStatisticsImpl : IMemoryStatistics
{
    private readonly MemoryManagerStats _stats;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryStatisticsImpl"/> class.
    /// </summary>
    /// <param name="stats">The memory manager statistics.</param>
    internal MemoryStatisticsImpl(MemoryManagerStats stats)
    {
        _stats = stats;
    }

    /// <inheritdoc/>
    public long TotalAllocated => _stats.TotalAllocated;

    /// <inheritdoc/>
    public long TotalFreed => _stats.TotalFreed;

    /// <inheritdoc/>
    public long CurrentUsage => _stats.CurrentUsage;

    /// <inheritdoc/>
    public long PeakUsage => _stats.PeakUsage;

    /// <inheritdoc/>
    public int AllocationCount => _stats.AllocationCount;

    /// <inheritdoc/>
    public int DeallocationCount => _stats.DeallocationCount;

    /// <inheritdoc/>
    public int ActiveBuffers => _stats.ActiveBuffers;

    /// <inheritdoc/>
    public double FragmentationRatio => _stats.FragmentationRatio;
}

/// <summary>
/// Internal memory manager statistics tracking.
/// </summary>
internal sealed class MemoryManagerStats
{
    private long _totalAllocated;
    private long _totalFreed;
    private long _currentUsage;
    private long _peakUsage;
    private int _allocationCount;
    private int _deallocationCount;
    private int _activeBuffers;

    /// <summary>
    /// Gets the total memory allocated.
    /// </summary>
    public long TotalAllocated => _totalAllocated;

    /// <summary>
    /// Gets the total memory freed.
    /// </summary>
    public long TotalFreed => _totalFreed;

    /// <summary>
    /// Gets the current memory usage.
    /// </summary>
    public long CurrentUsage => _currentUsage;

    /// <summary>
    /// Gets the peak memory usage.
    /// </summary>
    public long PeakUsage => _peakUsage;

    /// <summary>
    /// Gets the number of allocations.
    /// </summary>
    public int AllocationCount => _allocationCount;

    /// <summary>
    /// Gets the number of deallocations.
    /// </summary>
    public int DeallocationCount => _deallocationCount;

    /// <summary>
    /// Gets the number of active buffers.
    /// </summary>
    public int ActiveBuffers => _activeBuffers;

    /// <summary>
    /// Gets the fragmentation ratio.
    /// </summary>
    public double FragmentationRatio
    {
        get
        {
            if (_currentUsage == 0)
            {
                return 0;
            }

            // Simple fragmentation estimate based on allocation patterns
            var avgAllocationSize = _allocationCount > 0 ? _totalAllocated / _allocationCount : 0;
            var avgDeallocationSize = _deallocationCount > 0 ? _totalFreed / _deallocationCount : 0;
            
            if (avgAllocationSize == 0)
            {
                return 0;
            }

            return 1.0 - (double)avgDeallocationSize / avgAllocationSize;
        }
    }

    /// <summary>
    /// Records a memory allocation.
    /// </summary>
    /// <param name="bytes">The number of bytes allocated.</param>
    internal void RecordAllocation(long bytes)
    {
        _totalAllocated += bytes;
        _currentUsage += bytes;
        _allocationCount++;
        _activeBuffers++;

        if (_currentUsage > _peakUsage)
        {
            _peakUsage = _currentUsage;
        }
    }

    /// <summary>
    /// Records a memory deallocation.
    /// </summary>
    /// <param name="bytes">The number of bytes freed.</param>
    internal void RecordDeallocation(long bytes)
    {
        _totalFreed += bytes;
        _currentUsage -= bytes;
        _deallocationCount++;
        _activeBuffers--;

        if (_currentUsage < 0)
        {
            _currentUsage = 0; // Prevent negative values due to tracking errors
        }

        if (_activeBuffers < 0)
        {
            _activeBuffers = 0;
        }
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    internal void Reset()
    {
        _totalAllocated = 0;
        _totalFreed = 0;
        _currentUsage = 0;
        _peakUsage = 0;
        _allocationCount = 0;
        _deallocationCount = 0;
        _activeBuffers = 0;
    }
}