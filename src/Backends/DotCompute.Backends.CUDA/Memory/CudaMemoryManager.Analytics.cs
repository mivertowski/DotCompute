// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Memory.Models;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// High-level analytics and cleanup helpers for <see cref="CudaMemoryManager"/>.
/// These methods were previously exposed by a parallel facade
/// (Integration/Components/CudaMemoryManager) that has since been removed as part of the
/// v1.0 deduplication pass. They are implemented here so the single canonical
/// <c>CudaMemoryManager</c> remains the only public CUDA memory-manager type.
/// </summary>
public sealed partial class CudaMemoryManager
{
    /// <summary>
    /// Returns high-level usage analytics derived from the underlying allocator state.
    /// </summary>
    /// <returns>An immutable <see cref="MemoryUsageAnalytics"/> snapshot.</returns>
    public MemoryUsageAnalytics GetUsageAnalytics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var stats = GetCudaMemoryStatistics();

        return new MemoryUsageAnalytics
        {
            TotalAllocations = stats.AllocationCount,
            TotalDeallocations = stats.DeallocationCount,
            TotalBytesAllocated = stats.TotalAllocated,
            // Transfer bytes are not tracked by the low-level manager; future instrumentation
            // can populate this. For now it mirrors the legacy facade's default of zero.
            TotalBytesTransferred = 0,
            FailedAllocations = 0,
            PoolHitRatio = stats.PoolHitRate,
            AverageAllocationSize = stats.AverageAllocationSize,
            PeakMemoryUsage = stats.PeakUsage,
            LastOptimizationTime = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Performs a comprehensive cleanup of this memory manager: clears the allocator,
    /// optimizes device state, and records a summary of the work performed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="MemoryCleanupSummary"/> describing the cleanup outcome.</returns>
    public async ValueTask<MemoryCleanupSummary> PerformCleanupAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var startTime = DateTimeOffset.UtcNow;
        var beforeMemory = CurrentAllocatedMemory;

        try
        {
            await OptimizeAsync(cancellationToken).ConfigureAwait(false);

            _context.MakeCurrent();

            var afterMemory = CurrentAllocatedMemory;
            var endTime = DateTimeOffset.UtcNow;

            var summary = new MemoryCleanupSummary
            {
                Success = true,
                StartTime = startTime,
                EndTime = endTime,
                MemoryFreed = Math.Max(0, beforeMemory - afterMemory),
                PoolItemsFreed = 0,
                OptimizationsPerformed = ["Memory optimization", "Device synchronization"]
            };

            LogCleanupCompleted(_logger, summary.MemoryFreed, summary.Duration.TotalMilliseconds);
            return summary;
        }
        catch (Exception ex)
        {
            LogCleanupFailed(_logger, ex);

            return new MemoryCleanupSummary
            {
                Success = false,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    [LoggerMessage(
        EventId = 23130,
        Level = LogLevel.Information,
        Message = "CUDA memory cleanup completed: {MemoryFreed} bytes freed in {Duration:F2}ms")]
    private static partial void LogCleanupCompleted(ILogger logger, long memoryFreed, double duration);

    [LoggerMessage(
        EventId = 23131,
        Level = LogLevel.Error,
        Message = "CUDA memory cleanup failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception ex);
}
