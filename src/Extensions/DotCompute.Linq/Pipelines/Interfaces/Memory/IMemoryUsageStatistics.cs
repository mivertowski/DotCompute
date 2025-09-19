// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;

namespace DotCompute.Linq.Pipelines.Interfaces.Memory
{
    /// <summary>
    /// Memory usage statistics interface.
    /// </summary>
    public interface IMemoryUsageStatistics
    {
        /// <summary>Gets the peak memory usage in bytes.</summary>
        long PeakMemoryUsage { get; }

        /// <summary>Gets the average memory usage in bytes.</summary>
        long AverageMemoryUsage { get; }

        /// <summary>Gets the number of allocations.</summary>
        int AllocationCount { get; }

        /// <summary>Gets the total allocated memory in bytes.</summary>
        long TotalAllocatedMemory { get; }

        /// <summary>Gets memory usage by stage.</summary>
        IReadOnlyDictionary<string, long> MemoryByStage { get; }
    }
}