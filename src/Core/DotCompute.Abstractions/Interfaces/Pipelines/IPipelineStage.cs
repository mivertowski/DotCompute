// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using DotCompute.Abstractions.Models.Pipelines;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Interfaces.Pipelines.Interfaces;

namespace DotCompute.Abstractions.Interfaces.Pipelines
{




    /// <summary>
    /// Memory usage statistics.
    /// </summary>
    public sealed class MemoryUsageStats
    {
        /// <summary>
        /// Gets the allocated memory in bytes.
        /// </summary>
        public required long AllocatedBytes { get; init; }

        /// <summary>
        /// Gets the peak memory usage in bytes.
        /// </summary>
        public required long PeakBytes { get; init; }

        /// <summary>
        /// Gets the number of allocations.
        /// </summary>
        public required int AllocationCount { get; init; }

        /// <summary>
        /// Gets the number of deallocations.
        /// </summary>
        public required int DeallocationCount { get; init; }

        /// <summary>
        /// Gets memory usage by type.
        /// </summary>
        public IReadOnlyDictionary<string, long>? UsageByType { get; init; }
    }

    // MemoryHint enum moved to DotCompute.Abstractions.Pipelines.Enums.MemoryHint
    // This duplicate definition has been removed to prevent conflicts

}
