// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Utilities;

/// <summary>
/// Unified buffer allocation utilities that consolidate common allocation patterns
/// across all backend implementations. Eliminates duplicate allocation logic
/// and provides consistent error handling and performance optimization.
/// </summary>
public static partial class BufferAllocationUtilities
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 5001, Level = MsLogLevel.Warning, Message = "Memory allocation failed on attempt {Attempt}/{MaxRetries}, retrying after {DelayMs}ms")]
    private static partial void LogAllocationRetry(ILogger logger, int attempt, int maxRetries, double delayMs);

    #endregion

    private static readonly ConcurrentDictionary<Type, int> TypeSizeCache = new();

    /// <summary>
    /// Validates allocation parameters with comprehensive error checking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateAllocationParameters<T>(int count, long maxAllocationSize) where T : unmanaged
    {
        if (count <= 0)
        {

            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive");
        }


        var elementSize = GetElementSize<T>();
        var totalSize = (long)count * elementSize;

        if (totalSize > maxAllocationSize)
        {

            throw new ArgumentOutOfRangeException(nameof(count),
                $"Requested allocation of {totalSize} bytes exceeds maximum {maxAllocationSize} bytes");
        }


        if (totalSize > int.MaxValue && !Environment.Is64BitProcess)
        {

            throw new ArgumentOutOfRangeException(nameof(count),
                "Allocation size exceeds maximum for 32-bit process");
        }
    }

    /// <summary>
    /// Validates raw allocation parameters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateRawAllocationParameters(long sizeInBytes, long maxAllocationSize)
    {
        if (sizeInBytes <= 0)
        {

            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be positive");
        }


        if (sizeInBytes > maxAllocationSize)
        {

            throw new ArgumentOutOfRangeException(nameof(sizeInBytes),
                $"Requested allocation of {sizeInBytes} bytes exceeds maximum {maxAllocationSize} bytes");
        }


        if (sizeInBytes > int.MaxValue && !Environment.Is64BitProcess)
        {

            throw new ArgumentOutOfRangeException(nameof(sizeInBytes),
                "Allocation size exceeds maximum for 32-bit process");
        }
    }

    /// <summary>
    /// Gets the size of an unmanaged type with caching for performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetElementSize<T>() where T : unmanaged => TypeSizeCache.GetOrAdd(typeof(T), _ => Unsafe.SizeOf<T>());

    /// <summary>
    /// Calculates allocation size in bytes for a given count and type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long CalculateAllocationSize<T>(int count) where T : unmanaged => (long)count * GetElementSize<T>();

    /// <summary>
    /// Validates copy operation parameters with comprehensive bounds checking.
    /// </summary>
    public static void ValidateCopyParameters<T>(
        IUnifiedMemoryBuffer<T> source, int sourceOffset,
        IUnifiedMemoryBuffer<T> destination, int destinationOffset,
        int count) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);


        if (sourceOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOffset));
        }


        if (destinationOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationOffset));
        }


        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }


        if (sourceOffset + count > source.Length)
        {

            throw new ArgumentException($"Source range [{sourceOffset}, {sourceOffset + count}) exceeds buffer bounds [0, {source.Length})");
        }


        if (destinationOffset + count > destination.Length)
        {

            throw new ArgumentException($"Destination range [{destinationOffset}, {destinationOffset + count}) exceeds buffer bounds [0, {destination.Length})");
        }


        if (source.IsDisposed)
        {

            throw new ObjectDisposedException(nameof(source), "Source buffer has been disposed");
        }


        if (destination.IsDisposed)
        {

            throw new ObjectDisposedException(nameof(destination), "Destination buffer has been disposed");
        }
    }

    /// <summary>
    /// Validates view creation parameters.
    /// </summary>
    public static void ValidateViewParameters<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int count) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }


        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }


        if (offset + count > buffer.Length)
        {

            throw new ArgumentException($"View range [{offset}, {offset + count}) exceeds buffer bounds [0, {buffer.Length})");
        }


        if (buffer.IsDisposed)
        {

            throw new ObjectDisposedException(nameof(buffer), "Buffer has been disposed");
        }
    }

    /// <summary>
    /// Performs allocation with retry and exponential backoff for memory pressure scenarios.
    /// </summary>
    public static async Task<T> AllocateWithRetryAsync<T>(
        Func<T> allocateFunc,
        ILogger logger,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var delay = baseDelay ?? TimeSpan.FromMilliseconds(100);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return allocateFunc();
            }
            catch (OutOfMemoryException) when (attempt < maxRetries)
            {
                LogAllocationRetry(logger, attempt, maxRetries, delay.TotalMilliseconds);

                // Force garbage collection to free up memory
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);

                // Exponential backoff with jitter
                var actualDelay = TimeSpan.FromTicks((long)(delay.Ticks * Math.Pow(2, attempt - 1) * (0.8 + Random.Shared.NextDouble() * 0.4)));
                await Task.Delay(actualDelay, cancellationToken);
            }
        }

        // Final attempt
        return allocateFunc();
    }

    /// <summary>
    /// Determines optimal memory allocation strategy based on size and system state.
    /// </summary>
    public static AllocationStrategy DetermineOptimalStrategy(long sizeInBytes, MemoryOptions options)
    {
        // Small allocations (< 1MB) - prefer fast allocation
        if (sizeInBytes < 1024 * 1024)
        {
            return AllocationStrategy.Fast;
        }

        // Large allocations (> 100MB) - prefer pooled allocation to reduce fragmentation
        if (sizeInBytes > 100 * 1024 * 1024)
        {
            return AllocationStrategy.Pooled;
        }

        // Medium allocations - use default strategy based on options
        if ((options & MemoryOptions.Pooled) != 0)
        {
            return AllocationStrategy.Pooled;
        }

        if ((options & MemoryOptions.Pinned) != 0)
        {
            return AllocationStrategy.Pinned;
        }

        return AllocationStrategy.Standard;
    }

    /// <summary>
    /// Calculates optimal buffer alignment based on backend requirements.
    /// </summary>
    public static int CalculateOptimalAlignment<T>(string backendType) where T : unmanaged
    {
        var elementSize = GetElementSize<T>();

        return backendType.ToUpper(CultureInfo.InvariantCulture) switch
        {
            "cuda" => Math.Max(elementSize, 32), // CUDA prefers 32-byte alignment
            "metal" => Math.Max(elementSize, 16), // Metal prefers 16-byte alignment
            "opencl" => Math.Max(elementSize, 16), // OpenCL prefers 16-byte alignment
            "cpu" => Math.Max(elementSize, 64), // CPU SIMD prefers cache line alignment
            _ => Math.Max(elementSize, 16) // Default alignment
        };
    }

    /// <summary>
    /// Creates a memory allocation descriptor with all necessary information.
    /// </summary>
    public static AllocationDescriptor CreateAllocationDescriptor<T>(
        int count,
        MemoryOptions options,
        string backendType,
        long maxAllocationSize) where T : unmanaged
    {
        ValidateAllocationParameters<T>(count, maxAllocationSize);

        var elementSize = GetElementSize<T>();
        var totalSize = CalculateAllocationSize<T>(count);
        var alignment = CalculateOptimalAlignment<T>(backendType);
        var strategy = DetermineOptimalStrategy(totalSize, options);

        return new AllocationDescriptor
        {
            ElementType = typeof(T),
            ElementSize = elementSize,
            Count = count,
            TotalSizeInBytes = totalSize,
            Options = options,
            BackendType = backendType,
            Alignment = alignment,
            Strategy = strategy
        };
    }

    /// <summary>
    /// Estimates memory usage for an allocation including overhead.
    /// </summary>
    public static MemoryUsageEstimate EstimateMemoryUsage<T>(
        int count,
        MemoryOptions options,
        string backendType) where T : unmanaged
    {
        var baseSize = CalculateAllocationSize<T>(count);
        var alignment = CalculateOptimalAlignment<T>(backendType);

        // Calculate overhead based on backend and options
        var overhead = backendType.ToUpper(CultureInfo.InvariantCulture) switch
        {
            "cuda" => baseSize * 0.05, // 5% overhead for CUDA metadata
            "metal" => baseSize * 0.03, // 3% overhead for Metal metadata
            "opencl" => baseSize * 0.04, // 4% overhead for OpenCL metadata
            "cpu" => baseSize * 0.01, // 1% overhead for CPU allocations
            _ => baseSize * 0.02 // 2% default overhead
        };

        // Add alignment padding
        var alignmentPadding = alignment > 1 ? alignment : 0;

        // Add options-specific overhead
        if ((options & MemoryOptions.Pinned) != 0)
        {
            overhead += baseSize * 0.02; // 2% extra for pinned memory
        }


        if ((options & MemoryOptions.Unified) != 0)
        {

            overhead += baseSize * 0.03; // 3% extra for unified memory
        }


        var totalEstimatedSize = (long)(baseSize + overhead + alignmentPadding);

        return new MemoryUsageEstimate
        {
            BaseSize = baseSize,
            Overhead = (long)overhead,
            AlignmentPadding = alignmentPadding,
            TotalEstimatedSize = totalEstimatedSize,
            EfficiencyRatio = baseSize / (double)totalEstimatedSize
        };
    }
}
/// <summary>
/// An allocation strategy enumeration.
/// </summary>

/// <summary>
/// Allocation strategy enumeration.
/// </summary>
public enum AllocationStrategy
{
    /// <summary>
    /// Optimize for speed, prioritizing allocation performance.
    /// </summary>
    Fast,

    /// <summary>
    /// Balanced approach between speed and memory efficiency.
    /// </summary>
    Standard,

    /// <summary>
    /// Use memory pools for reduced allocation overhead.
    /// </summary>
    Pooled,

    /// <summary>
    /// Use pinned memory for optimal GPU transfer performance.
    /// </summary>
    Pinned,

    /// <summary>
    /// Use unified memory accessible from both GPU and CPU.
    /// </summary>
    Unified
}

/// <summary>
/// Comprehensive allocation descriptor.
/// </summary>
public sealed class AllocationDescriptor
{
    /// <summary>
    /// Gets or sets the element type.
    /// </summary>
    /// <value>The element type.</value>
    public required Type ElementType { get; init; }
    /// <summary>
    /// Gets or sets the element size.
    /// </summary>
    /// <value>The element size.</value>
    public required int ElementSize { get; init; }
    /// <summary>
    /// Gets or sets the count.
    /// </summary>
    /// <value>The count.</value>
    public required int Count { get; init; }
    /// <summary>
    /// Gets or sets the total size in bytes.
    /// </summary>
    /// <value>The total size in bytes.</value>
    public required long TotalSizeInBytes { get; init; }
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public required MemoryOptions Options { get; init; }
    /// <summary>
    /// Gets or sets the backend type.
    /// </summary>
    /// <value>The backend type.</value>
    public required string BackendType { get; init; }
    /// <summary>
    /// Gets or sets the alignment.
    /// </summary>
    /// <value>The alignment.</value>
    public required int Alignment { get; init; }
    /// <summary>
    /// Gets or sets the strategy.
    /// </summary>
    /// <value>The strategy.</value>
    public required AllocationStrategy Strategy { get; init; }
}

/// <summary>
/// Memory usage estimation result.
/// </summary>
public sealed class MemoryUsageEstimate
{
    /// <summary>
    /// Gets or sets the base size.
    /// </summary>
    /// <value>The base size.</value>
    public required long BaseSize { get; init; }
    /// <summary>
    /// Gets or sets the overhead.
    /// </summary>
    /// <value>The overhead.</value>
    public required long Overhead { get; init; }
    /// <summary>
    /// Gets or sets the alignment padding.
    /// </summary>
    /// <value>The alignment padding.</value>
    public required int AlignmentPadding { get; init; }
    /// <summary>
    /// Gets or sets the total estimated size.
    /// </summary>
    /// <value>The total estimated size.</value>
    public required long TotalEstimatedSize { get; init; }
    /// <summary>
    /// Gets or sets the efficiency ratio.
    /// </summary>
    /// <value>The efficiency ratio.</value>
    public required double EfficiencyRatio { get; init; }
}