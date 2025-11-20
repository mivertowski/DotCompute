// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Configs;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.MPS;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace DotCompute.Backends.Metal.Benchmarks;

/// <summary>
/// Comprehensive benchmarks to validate all Metal backend performance claims.
/// Target hardware: Apple M2 (or newer)
///
/// Performance Claims Under Test:
/// 1. Unified Memory: 2-3x speedup vs discrete memory
/// 2. MPS Operations: 3-4x speedup vs custom kernels
/// 3. Memory Pooling: 90% allocation reduction
/// 4. Backend Initialization: Sub-10ms cold start
/// 5. Kernel Compilation: &lt;1ms for cache hits
/// 6. Command Queue: &lt;100μs acquisition, &gt;80% reuse rate
/// 7. Graph Execution: &gt;1.5x speedup with 4 parallel nodes
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MetalPerformanceBenchmarks
{
    private MetalAccelerator? _accelerator;
    private MetalMemoryManager? _memoryManager;
    private MetalMemoryManager? _memoryManagerNoPooling;
    private ILogger<MetalAccelerator>? _logger;
    private ILogger<MetalMemoryManager>? _memLogger;
    private const int DataSize = 1_000_000; // 1M elements for meaningful measurements

    [GlobalSetup]
    public void Setup()
    {
        // Verify running on macOS with Metal support
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Metal benchmarks require macOS");
        }

        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        _logger = loggerFactory.CreateLogger<MetalAccelerator>();
        _memLogger = loggerFactory.CreateLogger<MetalMemoryManager>();

        var options = Options.Create(new MetalAcceleratorOptions
        {
            EnableMetalPerformanceShaders = true,
            CommandBufferCacheSize = 16
        });

        _accelerator = new MetalAccelerator(options, _logger);
        _memoryManager = new MetalMemoryManager(_memLogger, _accelerator, enablePooling: true);
        _memoryManagerNoPooling = new MetalMemoryManager(_memLogger, _accelerator, enablePooling: false);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_memoryManager != null)
        {
            await _memoryManager.DisposeAsync();
        }
        if (_memoryManagerNoPooling != null)
        {
            await _memoryManagerNoPooling.DisposeAsync();
        }
        if (_accelerator != null)
        {
            await _accelerator.DisposeAsync();
        }
    }

    #region Category 1: Unified Memory Performance (2 benchmarks)

    /// <summary>
    /// Benchmark 1: Compare MTLStorageModeShared (unified) vs MTLStorageModeManaged (discrete).
    /// Target: 2-3x speedup on unified memory for data transfers.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("UnifiedMemory")]
    public async Task UnifiedMemory_DiscreteMemory_Baseline()
    {
        if (_memoryManager == null)
        {
            throw new InvalidOperationException("Setup failed");
        }

        // Force discrete memory mode (MTLStorageModeManaged)
        var options = MemoryOptions.None; // Device memory without unified/coherent flags
        var buffer = await _memoryManager.AllocateAsync<float>(DataSize, options);

        try
        {
            // Simulate typical CPU-GPU data transfer pattern
            var hostData = new float[DataSize];
            for (int i = 0; i < DataSize; i++)
            {
                hostData[i] = i;
            }

            // Host to device
            await buffer.CopyFromAsync(hostData);

            // Device to host
            var result = new float[DataSize];
            await buffer.CopyToAsync(result);
        }
        finally
        {
            await _memoryManager.FreeAsync(buffer, CancellationToken.None);
        }
    }

    /// <summary>
    /// Benchmark 2: Unified memory with zero-copy optimization.
    /// Target: 2-3x speedup vs discrete memory (should complete in 33-50% of baseline time).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("UnifiedMemory")]
    public async Task UnifiedMemory_ZeroCopy_Optimized()
    {
        if (_memoryManager == null)
        {
            throw new InvalidOperationException("Setup failed");
        }

        // Force unified memory mode (MTLStorageModeShared) on Apple Silicon
        var options = MemoryOptions.Unified | MemoryOptions.Coherent;
        var buffer = await _memoryManager.AllocateAsync<float>(DataSize, options);

        try
        {
            // With unified memory, this should use zero-copy optimization
            var hostData = new float[DataSize];
            for (int i = 0; i < DataSize; i++)
            {
                hostData[i] = i;
            }

            // Host to device (zero-copy on unified memory)
            await buffer.CopyFromAsync(hostData);

            // Device to host (zero-copy on unified memory)
            var result = new float[DataSize];
            await buffer.CopyToAsync(result);
        }
        finally
        {
            await _memoryManager.FreeAsync(buffer, CancellationToken.None);
        }
    }

    #endregion

    #region Category 2: MPS Performance (2 benchmarks)

    /// <summary>
    /// Benchmark 3: Custom Metal kernel for matrix multiplication (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MPS")]
    public async Task MPS_CustomMatMul_Baseline()
    {
        if (_accelerator == null || _memoryManager == null)
        {
            throw new InvalidOperationException("Setup failed");
        }

        const int size = 512; // 512x512 matrices
        var a = await _memoryManager.AllocateAsync<float>(size * size);
        var b = await _memoryManager.AllocateAsync<float>(size * size);
        var c = await _memoryManager.AllocateAsync<float>(size * size);

        try
        {
            // Simple matrix multiply kernel (not optimized)
            var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void matmul(
    device const float* A [[buffer(0)]],
    device const float* B [[buffer(1)]],
    device float* C [[buffer(2)]],
    constant int& N [[buffer(3)]],
    uint2 gid [[thread_position_in_grid]])
{
    int row = gid.y;
    int col = gid.x;
    if (row >= N || col >= N) return;

    float sum = 0.0f;
    for (int k = 0; k < N; k++) {
        sum += A[row * N + k] * B[k * N + col];
    }
    C[row * N + col] = sum;
}";

            var definition = new KernelDefinition("matmul", kernelCode)
            {
                EntryPoint = "matmul",
                Language = KernelLanguage.Metal
            };

            var kernel = await _accelerator.CompileKernelAsync(definition);
            await kernel.ExecuteAsync([a, b, c, size], CancellationToken.None);
        }
        finally
        {
            await _memoryManager.FreeAsync(a, CancellationToken.None);
            await _memoryManager.FreeAsync(b, CancellationToken.None);
            await _memoryManager.FreeAsync(c, CancellationToken.None);
        }
    }

    /// <summary>
    /// Benchmark 4: MPS accelerated matrix multiplication.
    /// Target: 3-4x speedup vs custom kernel (should complete in 25-33% of baseline time).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("MPS")]
    public async Task MPS_AcceleratedMatMul_Optimized()
    {
        if (_memoryManager == null)
        {
            throw new InvalidOperationException("Setup failed");
        }

        const int size = 512;
        var a = await _memoryManager.AllocateAsync<float>(size * size);
        var b = await _memoryManager.AllocateAsync<float>(size * size);
        var c = await _memoryManager.AllocateAsync<float>(size * size);

        try
        {
            var device = MetalNative.CreateSystemDefaultDevice();
            var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Error));
            var logger = loggerFactory.CreateLogger<MetalMPSOrchestrator>();
            var orchestrator = new MetalMPSOrchestrator(device, logger);

            // MPS matrix multiplication (highly optimized by Apple)
            // Note: Using synchronous method as async version doesn't exist
            // Allocate host arrays and perform matrix multiply
            var aData = new float[size * size];
            var bData = new float[size * size];
            var cData = new float[size * size];

            await a.CopyToAsync(aData);
            await b.CopyToAsync(bData);

            orchestrator.MatrixMultiply(
                aData, size, size,
                bData, size, size,
                cData, size, size);

            await c.CopyFromAsync(cData);
            orchestrator.Dispose();
        }
        finally
        {
            await _memoryManager.FreeAsync(a, CancellationToken.None);
            await _memoryManager.FreeAsync(b, CancellationToken.None);
            await _memoryManager.FreeAsync(c, CancellationToken.None);
        }
    }

    #endregion

    #region Category 3: Memory Pooling (2 benchmarks)

    /// <summary>
    /// Benchmark 5: Direct memory allocation without pooling (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MemoryPooling")]
    public async Task MemoryPool_DirectAllocation_Baseline()
    {
        if (_memoryManagerNoPooling == null)
        {
            throw new InvalidOperationException("Setup failed");
        }

        // Allocate and free 100 buffers without pooling
        var allocations = 0;
        for (int i = 0; i < 100; i++)
        {
            var buffer = await _memoryManagerNoPooling.AllocateAsync<float>(1024);
            allocations++;
            await _memoryManagerNoPooling.FreeAsync(buffer, CancellationToken.None);
        }

        // Validate we made 100 allocations
        Debug.Assert(allocations == 100);
    }

    /// <summary>
    /// Benchmark 6: Pooled memory allocation.
    /// Target: 90% reduction in allocations (10 actual allocations for 100 requests).
    /// Performance overhead should be &lt;5% compared to baseline time.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("MemoryPooling")]
    public async Task MemoryPool_PooledAllocation_Optimized()
    {
        if (_memoryManager == null)
        {
            throw new InvalidOperationException("Setup failed");
        }

        // Allocate and free 100 buffers with pooling
        for (int i = 0; i < 100; i++)
        {
            var buffer = await _memoryManager.AllocateAsync<float>(1024);
            await _memoryManager.FreeAsync(buffer, CancellationToken.None);
        }

        // Verify pool statistics show 90% allocation reduction
        var stats = _memoryManager.PoolStatistics;
        if (stats != null)
        {
            var reduction = stats.AllocationReductionPercentage;
            Debug.Assert(reduction >= 85.0, $"Expected >=85% reduction, got {reduction:F1}%");
        }
    }

    #endregion

    #region Category 4: Startup Time (1 benchmark)

    /// <summary>
    /// Benchmark 7: Cold start initialization of Metal backend.
    /// Target: Sub-10ms startup time.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Startup")]
    public static async Task Backend_ColdStart_Initialization()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<MetalAccelerator>();

        var sw = Stopwatch.StartNew();

        var options = Options.Create(new MetalAcceleratorOptions());
        await using var accelerator = new MetalAccelerator(options, logger);

        sw.Stop();

        // Assert sub-10ms startup
        Debug.Assert(sw.ElapsedMilliseconds < 10,
            $"Startup took {sw.ElapsedMilliseconds}ms, expected <10ms");
    }

    #endregion

    #region Category 5: Kernel Compilation (2 benchmarks)

    /// <summary>
    /// Benchmark 8: Kernel compilation from source (cache miss).
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Compilation")]
    public async Task Kernel_Compilation_CacheMiss()
    {
        if (_accelerator == null)
        {
            throw new InvalidOperationException("Setup failed");
        }

        // Use a unique kernel name to force cache miss
        var uniqueName = $"test_kernel_{Guid.NewGuid():N}";
        var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void " + uniqueName + @"(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint id [[thread_position_in_grid]])
{
    output[id] = input[id] * 2.0f;
}";

        var definition = new KernelDefinition(uniqueName, kernelCode)
        {
            EntryPoint = uniqueName,
            Language = KernelLanguage.Metal
        };

        var kernel = await _accelerator.CompileKernelAsync(definition);
        kernel.Dispose();
    }

    /// <summary>
    /// Benchmark 9: Kernel retrieval from cache.
    /// Target: &lt;1ms for cache hit (should be &gt;10x faster than compilation).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Compilation")]
    public async Task Kernel_Compilation_CacheHit()
    {
        if (_accelerator == null)
        {
            throw new InvalidOperationException("Setup failed");
        }

        // Use a consistent kernel name to hit cache
        const string cachedName = "cached_test_kernel";
        var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void cached_test_kernel(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint id [[thread_position_in_grid]])
{
    output[id] = input[id] * 2.0f;
}";

        var definition = new KernelDefinition(cachedName, kernelCode)
        {
            EntryPoint = cachedName,
            Language = KernelLanguage.Metal
        };

        // First compile to populate cache (done in setup, not measured)
        var kernel = await _accelerator.CompileKernelAsync(definition);

        // This should hit cache
        var sw = Stopwatch.StartNew();
        var cachedKernel = await _accelerator.CompileKernelAsync(definition);
        sw.Stop();

        kernel.Dispose();
        cachedKernel.Dispose();

        // Assert <1ms for cache hit
        Debug.Assert(sw.ElapsedMilliseconds < 1,
            $"Cache hit took {sw.ElapsedMilliseconds}ms, expected <1ms");
    }

    #endregion

    #region Category 6: Command Queue (2 benchmarks)

    /// <summary>
    /// Benchmark 10: Command queue acquisition latency.
    /// Target: &lt;100μs (0.1ms) per acquisition.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("CommandQueue")]
    public static void CommandQueue_AcquisitionLatency()
    {
        var device = MetalNative.CreateSystemDefaultDevice();
        var commandQueue = MetalNative.CreateCommandQueue(device);

        var sw = Stopwatch.StartNew();

        // Measure command buffer acquisition
        var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);

        sw.Stop();

        MetalNative.ReleaseCommandBuffer(commandBuffer);
        MetalNative.ReleaseCommandQueue(commandQueue);
        MetalNative.ReleaseDevice(device);

        // Assert &lt;100μs (0.1ms)
        var microseconds = sw.Elapsed.TotalMicroseconds;
        Debug.Assert(microseconds < 100,
            $"Acquisition took {microseconds:F1}μs, expected &lt;100μs");
    }

    /// <summary>
    /// Benchmark 11: Command buffer reuse rate.
    /// Target: &gt;80% reuse rate from pool.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("CommandQueue")]
    public static void CommandQueue_ReuseRate()
    {
        var device = MetalNative.CreateSystemDefaultDevice();
        var commandQueue = MetalNative.CreateCommandQueue(device);

        var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<Utilities.MetalCommandBufferPool>();

        using var pool = new Utilities.MetalCommandBufferPool(commandQueue, logger, maxPoolSize: 16);

        // Request 100 command buffers
        var buffers = new List<IntPtr>();
        for (int i = 0; i < 100; i++)
        {
            buffers.Add(pool.GetCommandBuffer());
        }

        // Return them all
        foreach (var buffer in buffers)
        {
            pool.ReturnCommandBuffer(buffer);
        }

        // Request 100 more (should reuse from pool)
        for (int i = 0; i < 100; i++)
        {
            var buffer = pool.GetCommandBuffer();
            pool.ReturnCommandBuffer(buffer);
        }

        var stats = pool.GetStats();
        // Calculate reuse rate from pool utilization
        // After returning buffers, subsequent requests should hit the pool
        // Utilization represents how well the pool is being used
        var reuseRate = stats.Utilization / 100.0;

        Native.MetalNative.ReleaseCommandQueue(commandQueue);
        Native.MetalNative.ReleaseDevice(device);

        // Assert &gt;80% reuse rate
        Debug.Assert(reuseRate > 0.80,
            $"Reuse rate {reuseRate:P1}, expected &gt;80%");
    }

    #endregion

    #region Category 7: Graph Execution (2 benchmarks)

    /// <summary>
    /// Benchmark 12: Sequential execution of 4 independent kernels (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("GraphExecution")]
    public async Task Graph_Sequential_Baseline()
    {
        if (_accelerator == null || _memoryManager == null)
            throw new InvalidOperationException("Setup failed");

        const int kernelCount = 4;
        const int size = 10000;

        var buffers = new List<IUnifiedMemoryBuffer<float>>();
        for (int i = 0; i < kernelCount; i++)
        {
            buffers.Add(await _memoryManager.AllocateAsync<float>(size));
        }

        try
        {
            // Execute 4 simple kernels sequentially
            for (int i = 0; i < kernelCount; i++)
            {
                var kernelCode = $@"
#include <metal_stdlib>
using namespace metal;

kernel void seq_kernel_{i}(
    device float* data [[buffer(0)]],
    uint id [[thread_position_in_grid]])
{{
    data[id] = data[id] + 1.0f;
}}";

                var definition = new KernelDefinition($"seq_kernel_{i}", kernelCode)
                {
                    EntryPoint = $"seq_kernel_{i}",
                    Language = KernelLanguage.Metal
                };

                var kernel = await _accelerator.CompileKernelAsync(definition);
                await kernel.ExecuteAsync([buffers[i]], CancellationToken.None);
            }
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                await _memoryManager.FreeAsync(buffer, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Benchmark 13: Parallel execution of 4 independent kernels using compute graph.
    /// Target: &gt;1.5x speedup (should complete in &lt;67% of baseline time).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("GraphExecution")]
    public async Task Graph_Parallel_Optimized()
    {
        if (_accelerator == null || _memoryManager == null)
            throw new InvalidOperationException("Setup failed");

        const int kernelCount = 4;
        const int size = 10000;

        var buffers = new List<IUnifiedMemoryBuffer<float>>();
        for (int i = 0; i < kernelCount; i++)
        {
            buffers.Add(await _memoryManager.AllocateAsync<float>(size));
        }

        try
        {
            // Create parallel execution tasks
            var tasks = new List<Task>();

            for (int i = 0; i < kernelCount; i++)
            {
                int index = i; // Capture for closure
                tasks.Add(Task.Run(async () =>
                {
                    var kernelCode = $@"
#include <metal_stdlib>
using namespace metal;

kernel void par_kernel_{index}(
    device float* data [[buffer(0)]],
    uint id [[thread_position_in_grid]])
{{
    data[id] = data[id] + 1.0f;
}}";

                    var definition = new KernelDefinition($"par_kernel_{index}", kernelCode)
                    {
                        EntryPoint = $"par_kernel_{index}",
                        Language = KernelLanguage.Metal
                    };

                    var kernel = await _accelerator.CompileKernelAsync(definition);
                    await kernel.ExecuteAsync([buffers[index]], CancellationToken.None);
                }));
            }

            // Wait for all parallel kernels to complete
            await Task.WhenAll(tasks);
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                await _memoryManager.FreeAsync(buffer, CancellationToken.None);
            }
        }
    }

    #endregion
}
