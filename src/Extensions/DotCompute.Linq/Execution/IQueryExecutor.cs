// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Compilation.Plans;
using DotCompute.Abstractions.Memory;
using DotCompute.Linq.KernelGeneration;
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
    /// Executes a compute plan asynchronously and returns the result.
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation with the result.</returns>
    public Task<object?> ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default);
    /// Validates whether a compute plan can be executed.
    /// <param name="plan">The compute plan to validate.</param>
    /// <param name="accelerator">The target accelerator.</param>
    /// <returns>A validation result.</returns>
    public DotCompute.Abstractions.Validation.UnifiedValidationResult Validate(IComputePlan plan, IAccelerator accelerator);
}
/// Represents the context for executing a compute plan.
public class ExecutionContext
    /// Initializes a new instance of the <see cref="ExecutionContext"/> class.
    /// <param name="accelerator">The accelerator to use for execution.</param>
    /// <param name="plan">The compute plan to execute.</param>
    public ExecutionContext(IAccelerator accelerator, IComputePlan plan)
    {
        Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Parameters = [];
        BufferPool = new BufferPool();
    }
    /// Gets the accelerator for execution.
    public IAccelerator Accelerator { get; }
    /// Gets the compute plan to execute.
    public IComputePlan Plan { get; }
    /// Gets the execution parameters.
    public Dictionary<string, object> Parameters { get; }
    /// Gets the buffer pool for memory management.
    public BufferPool BufferPool { get; }
    /// Gets or sets the execution options.
    public ExecutionOptions Options { get; set; } = new();
    /// Gets the hardware information for the execution context.
    public HardwareInfo HardwareInfo { get; set; } = new(DotCompute.Linq.Types.BackendType.CPU, "DefaultDevice");
    /// Gets or sets GPU information for optimization.
    public GpuInfo? GpuInfo { get; set; }
    /// Gets the number of available CPU cores.
    public int AvailableCores => Environment.ProcessorCount;
    /// Gets the memory bandwidth in GB/s.
    public double MemoryBandwidth => HardwareInfo.MemoryBandwidthGBps;
    /// Gets a unique hardware signature for caching.
    public string HardwareSignature => $"{HardwareInfo.DeviceType}_{HardwareInfo.DeviceName}_{AvailableCores}";
    /// Gets the target backend type.
    public DotCompute.Linq.Types.BackendType TargetBackend => Accelerator switch
        _ when Accelerator.Info.Name.Contains("CUDA") => DotCompute.Linq.Types.BackendType.CUDA,
        _ when Accelerator.Info.Name.Contains("GPU") => DotCompute.Linq.Types.BackendType.CUDA,
        _ => DotCompute.Linq.Types.BackendType.CPU
    };
    /// Gets the cache size for the target backend.
    public long CacheSize => TargetBackend == DotCompute.Linq.Types.BackendType.CUDA
        ? (GpuInfo?.SharedMemoryPerBlock ?? 49152)
        : Environment.ProcessorCount * 32 * 1024; // Assume 32KB L1 per core
    /// Gets a value indicating whether AVX2 is supported.
    public bool HasAvx2 => System.Runtime.Intrinsics.X86.Avx2.IsSupported;
    /// Gets a value indicating whether AVX512F is supported.
    public bool HasAvx512 => System.Runtime.Intrinsics.X86.Avx512F.IsSupported;
    /// Gets the number of NUMA nodes available on the system.
    public int NumaNodeCount { get; set; } = 1;
    /// Gets the available memory in bytes.
    public long AvailableMemory { get; set; } = GC.GetTotalMemory(false);
    /// Gets the data size for the current operation.
    public long DataSize { get; set; } = 0;
    /// Gets the cache line size in bytes.
    public int CacheLineSize { get; set; } = 64; // Common cache line size
    /// Gets a value indicating whether the operation has complex memory access patterns.
    public bool HasComplexAccessPatterns { get; set; } = false;
    /// Gets the PCIe read bandwidth in bytes per second.
    public long PcieReadBandwidth { get; set; } = 16L * 1024 * 1024 * 1024; // 16 GB/s PCIe 4.0 x16
    /// Gets a value indicating whether the system has NUMA architecture.
    public bool IsNumaSystem { get; set; } = false;
    /// Gets a value indicating whether the system supports hyper-threading.
    public bool HasHyperThreading { get; set; } = Environment.ProcessorCount > Environment.ProcessorCount / 2;
    /// Gets the current system load as a percentage (0.0 to 1.0).
    public double SystemLoad { get; set; } = 0.5; // Default moderate load
    /// Gets the current thermal state of the system.
    public ThermalState ThermalState { get; set; } = ThermalState.Normal;
    /// Gets the current memory pressure level.
    public MemoryPressureLevel MemoryPressure { get; set; } = MemoryPressureLevel.Low;
    /// Creates a clone of this execution context.
    /// <returns>A cloned execution context.</returns>
    public ExecutionContext Clone()
        var clone = new ExecutionContext(Accelerator, Plan)
        {
            Options = Options,
            HardwareInfo = HardwareInfo,
            GpuInfo = GpuInfo,
            NumaNodeCount = NumaNodeCount,
            AvailableMemory = AvailableMemory,
            DataSize = DataSize,
            CacheLineSize = CacheLineSize,
            HasComplexAccessPatterns = HasComplexAccessPatterns,
            PcieReadBandwidth = PcieReadBandwidth,
            IsNumaSystem = IsNumaSystem,
            HasHyperThreading = HasHyperThreading,
            SystemLoad = SystemLoad,
            ThermalState = ThermalState,
            MemoryPressure = MemoryPressure
        };
        // Copy parameters
        foreach (var param in Parameters)
            clone.Parameters[param.Key] = param.Value;
        }
        return clone;
/// Represents execution options for query execution.
public class ExecutionOptions
    /// Gets or sets a value indicating whether to enable profiling.
    public bool EnableProfiling { get; set; }
    /// Gets or sets a value indicating whether to enable memory pooling.
    public bool EnableMemoryPooling { get; set; } = true;
    /// Gets or sets the maximum memory allocation size in bytes.
    public long MaxMemoryAllocation { get; set; } = long.MaxValue;
    /// Gets or sets the execution timeout.
    public TimeSpan? Timeout { get; set; }
    /// Gets or sets a value indicating whether to enable automatic fallback to CPU.
    public bool EnableCpuFallback { get; set; } = true;
/// Manages a pool of reusable memory buffers.
public class BufferPool
    private readonly Dictionary<string, IUnifiedMemoryBuffer> _buffers = [];
    private readonly Lock _lock = new();
    /// Gets or creates a buffer with the specified key.
    /// <param name="key">The buffer key.</param>
    /// <param name="size">The required size in bytes.</param>
    /// <param name="manager">The memory manager to use for allocation.</param>
    /// <returns>The memory buffer.</returns>
    public async Task<IUnifiedMemoryBuffer> GetOrCreateAsync(string key, long size, IUnifiedMemoryManager manager)
        lock (_lock)
            if (_buffers.TryGetValue(key, out var existingBuffer) && existingBuffer.SizeInBytes >= size)
            {
                return existingBuffer;
            }
        var buffer = await manager.AllocateAsync<byte>((int)size, DotCompute.Abstractions.Memory.MemoryOptions.None);
            // For now, don't dispose the buffer - the memory manager will handle cleanup
            _buffers[key] = buffer;
        return buffer!;
    /// Releases a buffer back to the pool.
    public void Release(string key)
            // Buffer remains in pool for reuse
            if (_buffers.TryGetValue(key, out _))
                // Buffer remains available for reuse
    /// Disposes all buffers in the pool.
    public void Dispose()
            // Clear buffers - memory manager will handle cleanup
            _buffers.Clear();
/// Interface for caching compiled query plans.
public interface IQueryCache
    /// Generates a cache key for an expression.
    /// <param name="expression">The expression to generate a key for.</param>
    /// <returns>The cache key.</returns>
    public string GenerateKey(System.Linq.Expressions.Expression expression);
    /// Tries to get a cached result.
    /// <param name="key">The cache key.</param>
    /// <param name="result">The cached result if found.</param>
    /// <returns>True if the result was found in cache; otherwise, false.</returns>
    public bool TryGet(string key, out object? result);
    /// Sets a result in the cache.
    /// <param name="result">The result to cache.</param>
    public void Set(string key, object? result);
    /// Clears the cache.
    public void Clear();
    /// Gets cache statistics.
    /// <returns>Cache statistics.</returns>
    public CacheStatistics GetStatistics();
/// Represents cache statistics.
public class CacheStatistics
    /// Gets or sets the total number of cache hits.
    public long Hits { get; set; }
    /// Gets or sets the total number of cache misses.
    public long Misses { get; set; }
    /// Gets or sets the current number of entries in the cache.
    public int EntryCount { get; set; }
    /// Gets or sets the total size of cached data in bytes.
    public long TotalSizeBytes { get; set; }
    /// Gets the cache hit ratio.
    public double HitRatio => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;
/// Contains GPU information for optimization purposes.
public class GpuInfo
    /// Gets or sets the GPU name.
    public string Name { get; set; } = string.Empty;
    /// Gets or sets the compute capability version.
    public Version ComputeCapability { get; set; } = new();
    /// Gets or sets the number of streaming multiprocessors.
    public int StreamingMultiprocessors { get; set; }
    /// Gets or sets the maximum threads per block.
    public int MaxThreadsPerBlock { get; set; } = 1024;
    /// Gets or sets the maximum threads per multiprocessor.
    public int MaxThreadsPerMultiprocessor { get; set; } = 2048;
    /// Gets or sets the global memory size in bytes.
    public long GlobalMemorySize { get; set; }
    /// Gets or sets the shared memory per block in bytes.
    public int SharedMemoryPerBlock { get; set; } = 49152;
    /// Gets or sets the number of registers per block.
    public int RegistersPerBlock { get; set; } = 65536;
    /// Gets or sets the memory bandwidth in bytes per second.
    public long MemoryBandwidth { get; set; }
    /// Gets or sets the maximum threads per streaming multiprocessor.
    public int MaxThreadsPerSM { get; set; } = 2048;
    /// Gets or sets the maximum blocks per streaming multiprocessor.
    public int MaxBlocksPerSM { get; set; } = 32;
/// Represents the thermal state of the system.
public enum ThermalState
    /// <summary>Normal temperature range.</summary>
    Normal,
    /// <summary>Warm but within safe limits.</summary>
    Warm,
    /// <summary>Hot, performance may be throttled.</summary>
    Hot,
    /// <summary>Critical temperature, aggressive throttling.</summary>
    Critical
/// Represents memory pressure levels.
public enum MemoryPressureLevel
    /// <summary>Low memory pressure.</summary>
    Low,
    /// <summary>Medium memory pressure.</summary>
    Medium,
    /// <summary>High memory pressure.</summary>
    High,
    /// <summary>Critical memory pressure.</summary>
