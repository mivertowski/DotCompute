// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Compilation.Plans;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Linq.Execution;


/// <summary>
/// Defines the interface for executing compiled compute plans.
/// </summary>
public interface IQueryExecutor
{
    /// <summary>
    /// Executes a compute plan and returns the result.
    /// </summary>
    /// <param name="context">The execution context containing the plan and accelerator.</param>
    /// <returns>The result of the execution.</returns>
    public object? Execute(ExecutionContext context);

    /// <summary>
    /// Executes a compute plan asynchronously and returns the result.
    /// </summary>
    /// <param name="context">The execution context containing the plan and accelerator.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation with the result.</returns>
    public Task<object?> ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a compute plan can be executed.
    /// </summary>
    /// <param name="plan">The compute plan to validate.</param>
    /// <param name="accelerator">The target accelerator.</param>
    /// <returns>A validation result.</returns>
    public DotCompute.Abstractions.Validation.UnifiedValidationResult Validate(IComputePlan plan, IAccelerator accelerator);
}

/// <summary>
/// Represents the context for executing a compute plan.
/// </summary>
public class ExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionContext"/> class.
    /// </summary>
    /// <param name="accelerator">The accelerator to use for execution.</param>
    /// <param name="plan">The compute plan to execute.</param>
    public ExecutionContext(IAccelerator accelerator, IComputePlan plan)
    {
        Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Parameters = [];
        BufferPool = new BufferPool();
    }

    /// <summary>
    /// Gets the accelerator for execution.
    /// </summary>
    public IAccelerator Accelerator { get; }

    /// <summary>
    /// Gets the compute plan to execute.
    /// </summary>
    public IComputePlan Plan { get; }

    /// <summary>
    /// Gets the execution parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; }

    /// <summary>
    /// Gets the buffer pool for memory management.
    /// </summary>
    public BufferPool BufferPool { get; }

    /// <summary>
    /// Gets or sets the execution options.
    /// </summary>
    public ExecutionOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets GPU information for optimization.
    /// </summary>
    public GpuInfo? GpuInfo { get; set; }

    /// <summary>
    /// Gets the target backend type.
    /// </summary>
    public DotCompute.Linq.Types.BackendType TargetBackend => Accelerator switch
    {
        _ when Accelerator.Name.Contains("CUDA") => DotCompute.Linq.Types.BackendType.CUDA,
        _ when Accelerator.Name.Contains("GPU") => DotCompute.Linq.Types.BackendType.CUDA,
        _ => DotCompute.Linq.Types.BackendType.CPU
    };

    /// <summary>
    /// Gets the cache size for the target backend.
    /// </summary>
    public long CacheSize => TargetBackend == DotCompute.Linq.Types.BackendType.CUDA
        ? (GpuInfo?.SharedMemoryPerBlock ?? 49152)
        : Environment.ProcessorCount * 32 * 1024; // Assume 32KB L1 per core

    /// <summary>
    /// Gets a value indicating whether AVX2 is supported.
    /// </summary>
    public bool HasAvx2 => System.Runtime.Intrinsics.X86.Avx2.IsSupported;

    /// <summary>
    /// Gets a value indicating whether AVX512F is supported.
    /// </summary>
    public bool HasAvx512 => System.Runtime.Intrinsics.X86.Avx512F.IsSupported;
}

/// <summary>
/// Represents execution options for query execution.
/// </summary>
public class ExecutionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable profiling.
    /// </summary>
    public bool EnableProfiling { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable memory pooling.
    /// </summary>
    public bool EnableMemoryPooling { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum memory allocation size in bytes.
    /// </summary>
    public long MaxMemoryAllocation { get; set; } = long.MaxValue;

    /// <summary>
    /// Gets or sets the execution timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable automatic fallback to CPU.
    /// </summary>
    public bool EnableCpuFallback { get; set; } = true;
}

/// <summary>
/// Manages a pool of reusable memory buffers.
/// </summary>
public class BufferPool
{
    private readonly Dictionary<string, IUnifiedMemoryBuffer> _buffers = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Gets or creates a buffer with the specified key.
    /// </summary>
    /// <param name="key">The buffer key.</param>
    /// <param name="size">The required size in bytes.</param>
    /// <param name="manager">The memory manager to use for allocation.</param>
    /// <returns>The memory buffer.</returns>
    public async Task<IUnifiedMemoryBuffer> GetOrCreateAsync(string key, long size, IUnifiedMemoryManager manager)
    {
        lock (_lock)
        {
            if (_buffers.TryGetValue(key, out var existingBuffer) && existingBuffer.SizeInBytes >= size)
            {
                return existingBuffer;
            }
        }

        var buffer = await manager.AllocateAsync<byte>((int)size, DotCompute.Abstractions.Memory.MemoryOptions.None);

        lock (_lock)
        {
            // For now, don't dispose the buffer - the memory manager will handle cleanup
            _buffers[key] = buffer;
        }

        return buffer!;
    }

    /// <summary>
    /// Releases a buffer back to the pool.
    /// </summary>
    /// <param name="key">The buffer key.</param>
    public void Release(string key)
    {
        lock (_lock)
        {
            // Buffer remains in pool for reuse

            if (_buffers.TryGetValue(key, out _))
            {
                // Buffer remains available for reuse
            }
        }
    }

    /// <summary>
    /// Disposes all buffers in the pool.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            // Clear buffers - memory manager will handle cleanup
            _buffers.Clear();
        }
    }
}

/// <summary>
/// Interface for caching compiled query plans.
/// </summary>
public interface IQueryCache
{
    /// <summary>
    /// Generates a cache key for an expression.
    /// </summary>
    /// <param name="expression">The expression to generate a key for.</param>
    /// <returns>The cache key.</returns>
    public string GenerateKey(System.Linq.Expressions.Expression expression);

    /// <summary>
    /// Tries to get a cached result.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="result">The cached result if found.</param>
    /// <returns>True if the result was found in cache; otherwise, false.</returns>
    public bool TryGet(string key, out object? result);

    /// <summary>
    /// Sets a result in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="result">The result to cache.</param>
    public void Set(string key, object? result);

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void Clear();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Cache statistics.</returns>
    public CacheStatistics GetStatistics();
}

/// <summary>
/// Represents cache statistics.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Gets or sets the total number of cache hits.
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// Gets or sets the total number of cache misses.
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// Gets or sets the current number of entries in the cache.
    /// </summary>
    public int EntryCount { get; set; }

    /// <summary>
    /// Gets or sets the total size of cached data in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    public double HitRatio => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;
}

/// <summary>
/// Contains GPU information for optimization purposes.
/// </summary>
public class GpuInfo
{
    /// <summary>
    /// Gets or sets the GPU name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compute capability version.
    /// </summary>
    public Version ComputeCapability { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of streaming multiprocessors.
    /// </summary>
    public int StreamingMultiprocessors { get; set; }

    /// <summary>
    /// Gets or sets the maximum threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the maximum threads per multiprocessor.
    /// </summary>
    public int MaxThreadsPerMultiprocessor { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the global memory size in bytes.
    /// </summary>
    public long GlobalMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the shared memory per block in bytes.
    /// </summary>
    public int SharedMemoryPerBlock { get; set; } = 49152;

    /// <summary>
    /// Gets or sets the number of registers per block.
    /// </summary>
    public int RegistersPerBlock { get; set; } = 65536;

    /// <summary>
    /// Gets or sets the memory bandwidth in bytes per second.
    /// </summary>
    public long MemoryBandwidth { get; set; }
}
