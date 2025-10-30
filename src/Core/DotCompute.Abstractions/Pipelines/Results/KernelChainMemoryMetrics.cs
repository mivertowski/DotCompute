// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;

namespace DotCompute.Abstractions.Pipelines.Results
{
    /// <summary>
    /// Comprehensive memory usage metrics for kernel chain execution.
    /// Provides detailed insights into memory allocation, usage patterns, and optimization opportunities.
    /// </summary>
    public sealed class KernelChainMemoryMetrics
    {
        /// <summary>
        /// Gets the peak memory usage during the entire chain execution in bytes.
        /// </summary>
        public required long PeakMemoryUsage { get; init; }

        /// <summary>
        /// Gets the total memory allocated during execution in bytes.
        /// </summary>
        public required long TotalMemoryAllocated { get; init; }

        /// <summary>
        /// Gets the total memory freed during execution in bytes.
        /// </summary>
        public required long TotalMemoryFreed { get; init; }

        /// <summary>
        /// Gets the number of garbage collections triggered during execution.
        /// </summary>
        public required int GarbageCollections { get; init; }

        /// <summary>
        /// Gets whether memory pooling was used to optimize allocations.
        /// </summary>
        public required bool MemoryPoolingUsed { get; init; }

        /// <summary>
        /// Gets the initial memory usage before chain execution started in bytes.
        /// </summary>
        public long? InitialMemoryUsage { get; init; }

        /// <summary>
        /// Gets the final memory usage after chain execution completed in bytes.
        /// </summary>
        public long? FinalMemoryUsage { get; init; }

        /// <summary>
        /// Gets the number of memory allocations made during execution.
        /// </summary>
        public int? AllocationCount { get; init; }

        /// <summary>
        /// Gets the number of memory deallocations made during execution.
        /// </summary>
        public int? DeallocationCount { get; init; }

        /// <summary>
        /// Gets the average allocation size in bytes.
        /// </summary>
        public double? AverageAllocationSize { get; init; }

        /// <summary>
        /// Gets the largest single allocation size in bytes.
        /// </summary>
        public long? LargestAllocation { get; init; }

        /// <summary>
        /// Gets the number of cache hits for memory allocations (when pooling is used).
        /// </summary>
        public int? PoolCacheHits { get; init; }

        /// <summary>
        /// Gets the number of cache misses for memory allocations (when pooling is used).
        /// </summary>
        public int? PoolCacheMisses { get; init; }

        /// <summary>
        /// Gets memory fragmentation percentage (estimated).
        /// </summary>
        public double? FragmentationPercentage { get; init; }

        /// <summary>
        /// Gets the time spent on memory management operations.
        /// </summary>
        public TimeSpan? MemoryManagementTime { get; init; }

        /// <summary>
        /// Gets device-specific memory metrics (for GPU/accelerator kernels).
        /// </summary>
        public IReadOnlyDictionary<string, DeviceMemoryMetrics>? DeviceMemoryMetrics { get; init; }

        /// <summary>
        /// Gets memory usage by step in the chain.
        /// </summary>
        public IReadOnlyList<StepMemoryUsage>? StepMemoryUsages { get; init; }

        /// <summary>
        /// Gets memory optimization recommendations.
        /// </summary>
        public IReadOnlyList<string>? OptimizationRecommendations { get; init; }

        /// <summary>
        /// Gets additional metadata for memory analysis.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Metadata { get; init; }

        /// <summary>
        /// Gets the net memory change (final - initial) in bytes.
        /// Positive values indicate memory growth, negative values indicate memory reduction.
        /// </summary>
        public long? NetMemoryChange
            => (FinalMemoryUsage.HasValue && InitialMemoryUsage.HasValue)
                ? FinalMemoryUsage.Value - InitialMemoryUsage.Value
                : null;

        /// <summary>
        /// Gets the memory efficiency ratio (useful operations per byte allocated).
        /// </summary>
        public double? MemoryEfficiency
            => TotalMemoryAllocated > 0 ? (double)(TotalMemoryFreed) / TotalMemoryAllocated : null;

        /// <summary>
        /// Gets the pool cache hit ratio (when pooling is used).
        /// </summary>
        public double? PoolCacheHitRatio
            => (PoolCacheHits.HasValue && PoolCacheMisses.HasValue && (PoolCacheHits.Value + PoolCacheMisses.Value) > 0)
                ? (double)PoolCacheHits.Value / (PoolCacheHits.Value + PoolCacheMisses.Value)
                : null;

        /// <summary>
        /// Creates memory metrics for a successful execution.
        /// </summary>
        /// <param name="peakMemoryUsage">Peak memory usage in bytes</param>
        /// <param name="totalMemoryAllocated">Total memory allocated in bytes</param>
        /// <param name="totalMemoryFreed">Total memory freed in bytes</param>
        /// <param name="garbageCollections">Number of garbage collections</param>
        /// <param name="memoryPoolingUsed">Whether memory pooling was used</param>
        /// <param name="initialMemoryUsage">Optional initial memory usage</param>
        /// <param name="finalMemoryUsage">Optional final memory usage</param>
        /// <param name="allocationCount">Optional allocation count</param>
        /// <param name="optimizationRecommendations">Optional optimization recommendations</param>
        /// <returns>Memory metrics instance</returns>
        public static KernelChainMemoryMetrics Create(
            long peakMemoryUsage,
            long totalMemoryAllocated,
            long totalMemoryFreed,
            int garbageCollections,
            bool memoryPoolingUsed,
            long? initialMemoryUsage = null,
            long? finalMemoryUsage = null,
            int? allocationCount = null,
            IReadOnlyList<string>? optimizationRecommendations = null)
            => new()
            {
                PeakMemoryUsage = peakMemoryUsage,
                TotalMemoryAllocated = totalMemoryAllocated,
                TotalMemoryFreed = totalMemoryFreed,
                GarbageCollections = garbageCollections,
                MemoryPoolingUsed = memoryPoolingUsed,
                InitialMemoryUsage = initialMemoryUsage,
                FinalMemoryUsage = finalMemoryUsage,
                AllocationCount = allocationCount,
                AverageAllocationSize = allocationCount > 0 ? (double)totalMemoryAllocated / allocationCount : null,
                OptimizationRecommendations = optimizationRecommendations
            };

        /// <summary>
        /// Creates memory metrics with detailed device information.
        /// </summary>
        /// <param name="peakMemoryUsage">Peak memory usage in bytes</param>
        /// <param name="totalMemoryAllocated">Total memory allocated in bytes</param>
        /// <param name="totalMemoryFreed">Total memory freed in bytes</param>
        /// <param name="garbageCollections">Number of garbage collections</param>
        /// <param name="memoryPoolingUsed">Whether memory pooling was used</param>
        /// <param name="deviceMetrics">Device-specific memory metrics</param>
        /// <param name="stepMemoryUsages">Memory usage by step</param>
        /// <param name="optimizationRecommendations">Optimization recommendations</param>
        /// <returns>Detailed memory metrics instance</returns>
        public static KernelChainMemoryMetrics CreateDetailed(
            long peakMemoryUsage,
            long totalMemoryAllocated,
            long totalMemoryFreed,
            int garbageCollections,
            bool memoryPoolingUsed,
            IReadOnlyDictionary<string, DeviceMemoryMetrics>? deviceMetrics = null,
            IReadOnlyList<StepMemoryUsage>? stepMemoryUsages = null,
            IReadOnlyList<string>? optimizationRecommendations = null)
            => new()
            {
                PeakMemoryUsage = peakMemoryUsage,
                TotalMemoryAllocated = totalMemoryAllocated,
                TotalMemoryFreed = totalMemoryFreed,
                GarbageCollections = garbageCollections,
                MemoryPoolingUsed = memoryPoolingUsed,
                DeviceMemoryMetrics = deviceMetrics,
                StepMemoryUsages = stepMemoryUsages,
                OptimizationRecommendations = optimizationRecommendations
            };

        /// <summary>
        /// Gets a summary report of memory usage.
        /// </summary>
        /// <returns>Human-readable memory usage summary</returns>
        public string GetSummaryReport()
        {
            var report = new System.Text.StringBuilder();
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"Peak Memory Usage: {FormatBytes(PeakMemoryUsage)}");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"Total Allocated: {FormatBytes(TotalMemoryAllocated)}");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"Total Freed: {FormatBytes(TotalMemoryFreed)}");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"Garbage Collections: {GarbageCollections}");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"Memory Pooling: {(MemoryPoolingUsed ? "Enabled" : "Disabled")}");

            if (NetMemoryChange.HasValue)
            {
                var change = NetMemoryChange.Value;
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"Net Memory Change: {(change >= 0 ? "+" : "")}{FormatBytes(Math.Abs(change))}");
            }

            if (MemoryEfficiency.HasValue)
            {
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"Memory Efficiency: {MemoryEfficiency.Value:P2}");
            }

            if (PoolCacheHitRatio.HasValue)
            {
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"Pool Cache Hit Ratio: {PoolCacheHitRatio.Value:P2}");
            }

            return report.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
            var counter = 0;
            double number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:N1} {suffixes[counter]}";
        }
    }

    /// <summary>
    /// Device-specific memory metrics for GPU/accelerator execution.
    /// </summary>
    public sealed class DeviceMemoryMetrics
    {
        /// <summary>
        /// Gets the device name or identifier.
        /// </summary>
        public required string DeviceName { get; init; }

        /// <summary>
        /// Gets the total device memory in bytes.
        /// </summary>
        public required long TotalDeviceMemory { get; init; }

        /// <summary>
        /// Gets the used device memory in bytes.
        /// </summary>
        public required long UsedDeviceMemory { get; init; }

        /// <summary>
        /// Gets the free device memory in bytes.
        /// </summary>
        public long FreeDeviceMemory => TotalDeviceMemory - UsedDeviceMemory;

        /// <summary>
        /// Gets the peak device memory usage during execution in bytes.
        /// </summary>
        public required long PeakDeviceMemoryUsage { get; init; }

        /// <summary>
        /// Gets the number of host-to-device memory transfers.
        /// </summary>
        public int? HostToDeviceTransfers { get; init; }

        /// <summary>
        /// Gets the number of device-to-host memory transfers.
        /// </summary>
        public int? DeviceToHostTransfers { get; init; }

        /// <summary>
        /// Gets the total time spent on memory transfers.
        /// </summary>
        public TimeSpan? MemoryTransferTime { get; init; }

        /// <summary>
        /// Gets the memory bandwidth utilization percentage.
        /// </summary>
        public double? BandwidthUtilization { get; init; }
    }

    /// <summary>
    /// Memory usage information for individual steps in a kernel chain.
    /// </summary>
    public sealed class StepMemoryUsage
    {
        /// <summary>
        /// Gets the step index.
        /// </summary>
        public required int StepIndex { get; init; }

        /// <summary>
        /// Gets the kernel name.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets the memory used before step execution in bytes.
        /// </summary>
        public required long MemoryBefore { get; init; }

        /// <summary>
        /// Gets the memory used after step execution in bytes.
        /// </summary>
        public required long MemoryAfter { get; init; }

        /// <summary>
        /// Gets the peak memory usage during step execution in bytes.
        /// </summary>
        public required long PeakMemoryDuringStep { get; init; }

        /// <summary>
        /// Gets the memory allocated by this step in bytes.
        /// </summary>
        public long MemoryAllocated => Math.Max(0, MemoryAfter - MemoryBefore);

        /// <summary>
        /// Gets the step identifier if specified.
        /// </summary>
        public string? StepId { get; init; }
    }
}
