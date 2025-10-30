// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Extensions;

/// <summary>
/// Extension methods for IAccelerator interface to provide additional functionality.
/// </summary>
public static class AcceleratorExtensions
{
    /// <summary>
    /// Gets the accelerator type (legacy property name for backward compatibility).
    /// </summary>
    /// <param name="accelerator">The accelerator instance.</param>
    /// <returns>The accelerator type.</returns>
    public static AcceleratorType AcceleratorType(this IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        return accelerator.Type;
    }

    /// <summary>
    /// Creates a graph for batching operations (if supported by the backend).
    /// </summary>
    /// <param name="accelerator">The accelerator instance.</param>
    /// <returns>A graph object if supported, otherwise null.</returns>
    /// <remarks>
    /// This is a compatibility method for CUDA graph functionality.
    /// Not all backends support graph creation.
    /// </remarks>
    public static object? CreateGraph(this IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);

        // For now, return null for unsupported backends
        // CUDA backend would override this with actual implementation

        return null;
    }

    /// <summary>
    /// Gets memory statistics for the accelerator (if supported).
    /// </summary>
    /// <param name="accelerator">The accelerator instance.</param>
    /// <returns>Memory statistics if supported, otherwise default values.</returns>
    public static MemoryStatistics GetMemoryStatistics(this IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);

        // Try to get statistics from the memory manager

        if (accelerator.Memory is IUnifiedMemoryManager memoryManager)
        {
            try
            {
                var stats = memoryManager.Statistics;
                return stats;
            }
            catch
            {
                // Fall back to default if not implemented
            }
        }
        else
        {
        }

        // Return default statistics

        return new MemoryStatistics
        {
            TotalMemoryBytes = accelerator.Info!.TotalMemory,
            UsedMemoryBytes = 0,
            AvailableMemoryBytes = accelerator.Info!.AvailableMemory,
            AllocationCount = 0
        };
    }
}
