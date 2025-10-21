// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CPU.Kernels.Simd;

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// Memory access pattern optimizer for SIMD operations
/// </summary>
public sealed class SimdMemoryOptimizer(ExecutorConfiguration config, ILogger logger) : IDisposable
{
    private readonly ExecutorConfiguration _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private volatile bool _disposed;

    /// <summary>
    /// Optimizes memory layout for vectorized operations
    /// </summary>
    public void OptimizeMemoryLayout<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output) where T : unmanaged
    {
        if (!_config.EnablePrefetching)
        {
            return;
        }

        // Prefetch initial data blocks

        PrefetchDataBlocks(input1, input2, output);
    }

    /// <summary>
    /// Prefetches data blocks to improve cache performance
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void PrefetchDataBlocks<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output) where T : unmanaged
    {
        const int prefetchDistance = 512; // Cache line prefetch distance

        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            // Prefetch first few cache lines
            var elementSize = Unsafe.SizeOf<T>();
            for (var i = 0; i < Math.Min(prefetchDistance, input1.Length); i += 64 / elementSize)
            {
                if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
                {
                    System.Runtime.Intrinsics.X86.Sse.Prefetch0(ptr1 + i);
                    System.Runtime.Intrinsics.X86.Sse.Prefetch0(ptr2 + i);
                    System.Runtime.Intrinsics.X86.Sse.PrefetchNonTemporal(ptrOut + i);
                }
            }
        }
    }

    /// <summary>
    /// Optimizes data alignment for SIMD operations
    /// </summary>
    public bool IsDataAligned<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        unsafe
        {
            fixed (T* ptr = data)
            {
                var address = (nint)ptr;
                var elementSize = Unsafe.SizeOf<T>();
                var alignment = elementSize >= 32 ? 32 : 16; // AVX requires 32-byte alignment
                return (address & (alignment - 1)) == 0;
            }
        }
    }

    /// <summary>
    /// Calculates optimal block size for cache-friendly processing
    /// </summary>
    public static int CalculateOptimalBlockSize<T>(int dataSize) where T : unmanaged
    {
        const int l1CacheSize = 32 * 1024; // 32KB L1 cache (typical)
        const int l2CacheSize = 256 * 1024; // 256KB L2 cache (typical)

        var elementSize = Unsafe.SizeOf<T>();
        var elementsInL1 = l1CacheSize / elementSize;
        var elementsInL2 = l2CacheSize / elementSize;

        // Choose block size based on data size
        if (dataSize <= elementsInL1)
        {
            return dataSize; // Fits in L1
        }
        else if (dataSize <= elementsInL2)
        {
            return elementsInL1 / 2; // Use half of L1 for better cache utilization
        }
        else
        {
            return elementsInL2 / 4; // Use quarter of L2 for large datasets
        }
    }

    /// <summary>
    /// Provides memory access pattern guidance
    /// </summary>
    public MemoryAccessPattern GetOptimalAccessPattern<T>(
        int dataSize,
        SimdExecutionStrategy strategy) where T : unmanaged
    {
        var blockSize = CalculateOptimalBlockSize<T>(dataSize);
        var vectorSize = GetVectorSize<T>(strategy);

        return new MemoryAccessPattern
        {
            BlockSize = blockSize,
            VectorSize = vectorSize,
            UseSequentialAccess = dataSize > 100_000,
            UsePrefetching = _config.EnablePrefetching && dataSize > 1000,
            OptimalStride = vectorSize * _config.UnrollFactor
        };
    }

    /// <summary>
    /// Gets vector size for the given strategy and type
    /// </summary>
    private static int GetVectorSize<T>(SimdExecutionStrategy strategy) where T : unmanaged
    {
        var elementSize = Unsafe.SizeOf<T>();
        return strategy switch
        {
            SimdExecutionStrategy.Avx512 => 512 / (elementSize * 8),
            SimdExecutionStrategy.Avx2 => 256 / (elementSize * 8),
            SimdExecutionStrategy.Sse => 128 / (elementSize * 8),
            SimdExecutionStrategy.Neon => 128 / (elementSize * 8),
            _ => 1
        };
    }

    /// <summary>
    /// Analyzes memory access patterns for optimization opportunities
    /// </summary>
    public MemoryAnalysisResult AnalyzeMemoryPattern<T>(
        ReadOnlySpan<T> data,
        int accessStride) where T : unmanaged
    {
        var elementSize = Unsafe.SizeOf<T>();
        var result = new MemoryAnalysisResult
        {
            DataSize = data.Length,
            AccessStride = accessStride,
            IsAligned = IsDataAligned(data),
            ElementSize = elementSize
        };

        // Analyze cache efficiency
        result.CacheEfficiency = CalculateCacheEfficiency(data.Length, accessStride, elementSize);

        // Suggest optimizations
        if (!result.IsAligned)
        {
            result.Recommendations.Add("Consider using aligned memory allocation");
        }

        if (result.CacheEfficiency < 0.7)
        {
            result.Recommendations.Add("Consider blocking to improve cache locality");
        }

        if (accessStride > 1)
        {
            result.Recommendations.Add("Non-unit stride detected - consider data reorganization");
        }

        return result;
    }

    /// <summary>
    /// Calculates estimated cache efficiency for the given access pattern
    /// </summary>
    private static double CalculateCacheEfficiency(int dataSize, int stride, int elementSize)
    {
        const int cacheLineSize = 64; // bytes
        var elementsPerCacheLine = cacheLineSize / elementSize;

        if (stride == 1)
        {
            return 1.0; // Perfect sequential access
        }

        if (stride <= elementsPerCacheLine)
        {
            return 1.0 / stride; // Partial cache line utilization
        }

        return Math.Max(0.1, 1.0 / (stride / elementsPerCacheLine)); // Poor cache utilization
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogDebug("SIMD Memory Optimizer disposed");
        }
    }
}

/// <summary>
/// Memory access pattern configuration
/// </summary>
public sealed class MemoryAccessPattern
{
    /// <summary>
    /// Gets or sets the block size.
    /// </summary>
    /// <value>The block size.</value>
    public int BlockSize { get; init; }
    /// <summary>
    /// Gets or sets the vector size.
    /// </summary>
    /// <value>The vector size.</value>
    public int VectorSize { get; init; }
    /// <summary>
    /// Gets or sets the use sequential access.
    /// </summary>
    /// <value>The use sequential access.</value>
    public bool UseSequentialAccess { get; init; }
    /// <summary>
    /// Gets or sets the use prefetching.
    /// </summary>
    /// <value>The use prefetching.</value>
    public bool UsePrefetching { get; init; }
    /// <summary>
    /// Gets or sets the optimal stride.
    /// </summary>
    /// <value>The optimal stride.</value>
    public int OptimalStride { get; init; }
}

/// <summary>
/// Result of memory pattern analysis
/// </summary>
public sealed class MemoryAnalysisResult
{
    /// <summary>
    /// Gets or sets the data size.
    /// </summary>
    /// <value>The data size.</value>
    public int DataSize { get; init; }
    /// <summary>
    /// Gets or sets the access stride.
    /// </summary>
    /// <value>The access stride.</value>
    public int AccessStride { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether aligned.
    /// </summary>
    /// <value>The is aligned.</value>
    public bool IsAligned { get; init; }
    /// <summary>
    /// Gets or sets the element size.
    /// </summary>
    /// <value>The element size.</value>
    public int ElementSize { get; init; }
    /// <summary>
    /// Gets or sets the cache efficiency.
    /// </summary>
    /// <value>The cache efficiency.</value>
    public double CacheEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the recommendations.
    /// </summary>
    /// <value>The recommendations.</value>
    public List<string> Recommendations { get; init; } = [];
}