// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Hardware.Cuda.Tests;


/// <summary>
/// Performance benchmarks for CUDA backend on real hardware
/// </summary>
[Trait("Category", "HardwareRequired")]
[Trait("Category", "CudaRequired")]
[Trait("Category", "Performance")]
[Collection("Hardware")]
public sealed class CudaPerformanceBenchmarks : IDisposable
{
    private readonly ILogger<CudaPerformanceBenchmarks> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CudaBackend? _backend;
    private readonly CudaAccelerator? _accelerator;
    private bool _disposed;

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, Exception?> LogStartingMemoryBandwidthBenchmark =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(LogStartingMemoryBandwidthBenchmark)),
            "Starting memory bandwidth benchmark");

    private static readonly Action<ILogger, double, double, double, Exception?> LogMemoryBandwidth =
        LoggerMessage.Define<double, double, double>(
            LogLevel.Information,
            new EventId(2, nameof(LogMemoryBandwidth)),
            "Memory Bandwidth {SizeMB}MB: H2D={H2DBandwidth:F1} GB/s, D2H={D2HBandwidth:F1} GB/s");

    private static readonly Action<ILogger, double, double, Exception?> LogSAXPYPerformance =
        LoggerMessage.Define<double, double>(
            LogLevel.Information,
            new EventId(3, nameof(LogSAXPYPerformance)),
            "SAXPY Performance: {GFLOPS:F1} GFLOPS, {Bandwidth:F1} GB/s effective bandwidth");

    private static readonly Action<ILogger, double, Exception?> LogAverageKernelLaunchTime =
        LoggerMessage.Define<double>(
            LogLevel.Information,
            new EventId(4, nameof(LogAverageKernelLaunchTime)),
            "Average kernel launch time: {AvgTime:F3} ms");

    private static readonly Action<ILogger, string, string, long, Exception?> LogCompilationTime =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Information,
            new EventId(5, nameof(LogCompilationTime)),
            "Compilation time for {KernelName} ({OptLevel}): {CompileTime}ms");

    private static readonly Action<ILogger, long, long, Exception?> LogExecutionTimes =
        LoggerMessage.Define<long, long>(
            LogLevel.Information,
            new EventId(6, nameof(LogExecutionTimes)),
            "Execution times - Sequential: {Sequential}ms, Concurrent: {Concurrent}ms");

    private static readonly Action<ILogger, double, Exception?> LogSpeedupFromConcurrency =
        LoggerMessage.Define<double>(
            LogLevel.Information,
            new EventId(7, nameof(LogSpeedupFromConcurrency)),
            "Speedup from concurrency: {Speedup:F2}x");

    public CudaPerformanceBenchmarks(ITestOutputHelper output)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Information));

        _logger = _loggerFactory.CreateLogger<CudaPerformanceBenchmarks>();

        if (CudaBackend.IsAvailable())
        {
            _backend = new CudaBackend(_loggerFactory.CreateLogger<CudaBackend>());
            _accelerator = _backend.GetDefaultAccelerator();
        }
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "Performance")]
    public async Task MemoryBandwidth_ShouldAchieveHighThroughput()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        // Note: GetStatistics() method doesn't exist in new IMemoryManager interface
        LogStartingMemoryBandwidthBenchmark(_logger, null);

        // Test memory bandwidth with different sizes
        var sizes = new[] { 1, 4, 16, 64, 256, 1024 }; // MB

        foreach (var sizeMB in sizes)
        {
            var sizeBytes = sizeMB * 1024 * 1024;
            var data = new byte[sizeBytes];
            new Random(42).NextBytes(data);

            var buffer = await _accelerator.Memory.AllocateAsync(sizeBytes);

            try
            {
                // Warm up
                await buffer.CopyFromHostAsync<byte>(data);
                await buffer.CopyToHostAsync<byte>(new byte[sizeBytes]);

                // Measure host to device transfer
                var stopwatch = Stopwatch.StartNew();
                for (var i = 0; i < 10; i++)
                {
                    await buffer.CopyFromHostAsync<byte>(data);
                }
                stopwatch.Stop();

                var h2dBandwidth = 10.0 * sizeBytes / 1024 / 1024 / 1024 / (stopwatch.ElapsedMilliseconds / 1000.0);

                // Measure device to host transfer
                var readData = new byte[sizeBytes];
                stopwatch.Restart();
                for (var i = 0; i < 10; i++)
                {
                    await buffer.CopyToHostAsync<byte>(readData);
                }
                stopwatch.Stop();

                var d2hBandwidth = 10.0 * sizeBytes / 1024 / 1024 / 1024 / (stopwatch.ElapsedMilliseconds / 1000.0);

                LogMemoryBandwidth(_logger, sizeMB, h2dBandwidth, d2hBandwidth, null);

                // RTX 2000 Ada Gen should achieve reasonable bandwidth >100 GB/s for large transfers
                if (sizeMB >= 64)
                {
                    _ = h2dBandwidth.Should().BeGreaterThan(50.0, $"H2D bandwidth too low: {h2dBandwidth:F1} GB/s");
                    _ = d2hBandwidth.Should().BeGreaterThan(50.0, $"D2H bandwidth too low: {d2hBandwidth:F1} GB/s");
                }
            }
            finally
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "Performance")]
    public async Task ComputeThroughput_ShouldAchieveHighPerformance()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        // SAXPY benchmark(Single-precision A*X Plus Y)
        const int N = 32 * 1024 * 1024; // 32M elements
        const float ALPHA = 2.5f;

        var x = new float[N];
        var y = new float[N];

        for (var i = 0; i < N; i++)
        {
            x[i] = i * 0.001f;
            y[i] = i * 0.002f + 1.0f;
        }

        var bufferX = await _accelerator.Memory.AllocateAsync(N * sizeof(float));
        var bufferY = await _accelerator.Memory.AllocateAsync(N * sizeof(float));

        try
        {
            await bufferX.CopyFromHostAsync<float>(x);
            await bufferY.CopyFromHostAsync<float>(y);

            var kernelSource = @"
extern ""C"" __global__ void saxpy(float* x, float* y, float alpha, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    int stride = blockDim.x * gridDim.x;
    
    for(int i = idx; i < n; i += stride) {
        y[i] = alpha * x[i] + y[i];
    }
}";

            var kernelSourceObj = new TextKernelSource(kernelSource, "saxpy", KernelLanguage.Cuda, "saxpy");
            var kernelDefinition = new KernelDefinition("saxpy", kernelSourceObj.Code, kernelSourceObj.EntryPoint);
            var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum };
            var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition, options);

            // Warm up
            var arguments = new KernelArguments(bufferX, bufferY, ALPHA, N);
            await compiledKernel.ExecuteAsync(arguments);

            // Benchmark multiple runs
            const int RUNS = 100;
            var stopwatch = Stopwatch.StartNew();

            for (var run = 0; run < RUNS; run++)
            {
                await compiledKernel.ExecuteAsync(arguments);
            }

            stopwatch.Stop();

            // Calculate performance metrics
            var totalOps = (long)RUNS * N * 2; // 2 ops per element(multiply + add)
            var gflops = totalOps / 1e9 / (stopwatch.ElapsedMilliseconds / 1000.0);
            var bandwidth = (long)RUNS * N * 3 * sizeof(float) / 1024.0 / 1024 / 1024 / (stopwatch.ElapsedMilliseconds / 1000.0); // 3 accesses per element

            LogSAXPYPerformance(_logger, gflops, bandwidth, null);

            // RTX 2000 Ada Gen should achieve substantial performance
            _ = gflops.Should().BeGreaterThan(100.0, $"Compute performance too low: {gflops:F1} GFLOPS");

            // Verify correctness of final result
            var result = new float[100]; // Check first 100 elements
            await bufferY.CopyToHostAsync<float>(result, 0);

            for (var i = 0; i < 100; i++)
            {
                var expected = ALPHA * x[i] + y[i];
                Assert.True(Math.Abs(result[i] - expected) < 0.001f,
                    $"SAXPY result incorrect at {i}: expected {expected}, got {result[i]}");
            }
        }
        finally
        {
            await bufferX.DisposeAsync();
            await bufferY.DisposeAsync();
        }
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "Performance")]
    public async Task KernelLaunchOverhead_ShouldBeLow()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        // Simple kernel with minimal work
        var kernelSource = @"
extern ""C"" __global__ void emptyKernel()
{
    // Minimal work to avoid optimization away
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    if(tid == 0) {
        // Ensure kernel actually runs
        atomicAdd((int*)0, 0); // This will be optimized but forces execution
    }
}";

        var kernelSourceObj = new TextKernelSource(kernelSource, "emptyKernel", KernelLanguage.Cuda, "emptyKernel");
        var kernelDefinition = new KernelDefinition("emptyKernel", kernelSourceObj.Code, kernelSourceObj.EntryPoint);
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition);

        // Warm up
        var arguments = new KernelArguments();
        await compiledKernel.ExecuteAsync(arguments);

        // Measure launch overhead
        const int LAUNCHES = 1000;
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < LAUNCHES; i++)
        {
            await compiledKernel.ExecuteAsync(arguments);
        }

        stopwatch.Stop();

        var avgLaunchTime = stopwatch.ElapsedMilliseconds / (double)LAUNCHES;

        LogAverageKernelLaunchTime(_logger, avgLaunchTime, null);

        // Launch overhead should be reasonable < 1ms on modern GPUs
        _ = avgLaunchTime.Should().BeLessThan(1.0, $"Kernel launch overhead too high: {avgLaunchTime:F3} ms");
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "Performance")]
    public async Task CompilationSpeed_ShouldBeReasonable()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        var kernelSources = new[]
        {
        // Simple kernel
        @"extern ""C"" __global__ void simple(float* data, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if(idx < n) data[idx] *= 2.0f;
            }",
        
        // Complex kernel with math functions
        @"extern ""C"" __global__ void complex(float* input, float* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if(idx < n) {
                    float x = input[idx];
                    output[idx] = sinf(x) * cosf(x * 2.0f) + expf(x * 0.1f) - logf(fabsf(x) + 1.0f);
                }
            }",
        
        // Kernel with shared memory
        @"extern ""C"" __global__ void sharedMem(float* input, float* output, int n) {
                __shared__ float sdata[256];
                int tid = threadIdx.x;
                int idx = blockIdx.x * blockDim.x + tid;
                
                sdata[tid] =(idx < n) ? input[idx] : 0.0f;
                __syncthreads();
                
                float sum = 0.0f;
                for(int i = 0; i < blockDim.x; i++) {
                    sum += sdata[i];
                }
                
                if(idx < n) output[idx] = sum;
            }"
    };

        var kernelNames = new[] { "simple", "complex", "sharedMem" };
        var optimizationLevels = new[] { OptimizationLevel.None, OptimizationLevel.Default, OptimizationLevel.Maximum };

        for (var i = 0; i < kernelSources.Length; i++)
        {
            var kernelSource = kernelSources[i];
            var kernelName = kernelNames[i];

            foreach (var optLevel in optimizationLevels)
            {
                var options = new CompilationOptions { OptimizationLevel = optLevel };
                var kernelSourceObj = new TextKernelSource(kernelSource, kernelName, KernelLanguage.Cuda, kernelName);
                var definition = new KernelDefinition($"{kernelName}_{optLevel}", kernelSourceObj.Code, kernelSourceObj.EntryPoint);

                var stopwatch = Stopwatch.StartNew();
                var compiledKernel = await _accelerator.CompileKernelAsync(definition, options);
                stopwatch.Stop();

                LogCompilationTime(_logger, kernelName, optLevel.ToString(), stopwatch.ElapsedMilliseconds, null);

                // Compilation should complete in reasonable time < 10 seconds for complex kernels
                _ = stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
                    $"Compilation too slow for {kernelName}: {stopwatch.ElapsedMilliseconds}ms");

                await compiledKernel.DisposeAsync();
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "Performance")]
    public async Task ConcurrentExecution_ShouldScaleWithStreams()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        const int KERNEL_COUNT = 8;
        const int ELEMENTS_PER_KERNEL = 1024 * 1024;

        var kernelSource = @"
extern ""C"" __global__ void workload(float* data, int n, int iterations)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        float val = data[idx];
        for(int i = 0; i < iterations; i++) {
            val = sinf(val) + 0.001f;
        }
        data[idx] = val;
    }
}";

        var kernelSourceObj = new TextKernelSource(kernelSource, "workload", KernelLanguage.Cuda, "workload");
        var kernelDefinition = new KernelDefinition("workload", kernelSourceObj.Code, kernelSourceObj.EntryPoint);
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition);

        // Test sequential execution
        var sequentialTime = await MeasureExecutionTime(async () =>
        {
            for (var i = 0; i < KERNEL_COUNT; i++)
            {
                var buffer = await _accelerator.Memory.AllocateAsync(ELEMENTS_PER_KERNEL * sizeof(float));
                try
                {
                    var arguments = new KernelArguments(buffer, ELEMENTS_PER_KERNEL, 100);
                    await compiledKernel.ExecuteAsync(arguments);
                }
                finally
                {
                    await buffer.DisposeAsync();
                }
            }
        });

        // Test concurrent execution
        var concurrentTime = await MeasureExecutionTime(async () =>
        {
            var tasks = new List<Task>();
            var buffers = new List<IMemoryBuffer>();

            try
            {
                for (var i = 0; i < KERNEL_COUNT; i++)
                {
                    var buffer = await _accelerator.Memory.AllocateAsync(ELEMENTS_PER_KERNEL * sizeof(float));
                    buffers.Add(buffer);

                    var task = Task.Run(async () =>
                    {
                        var arguments = new KernelArguments(buffer, ELEMENTS_PER_KERNEL, 100);
                        await compiledKernel.ExecuteAsync(arguments);
                    });

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
            }
            finally
            {
                foreach (var buffer in buffers)
                {
                    await buffer.DisposeAsync();
                }
            }
        });

        LogExecutionTimes(_logger, sequentialTime, concurrentTime, null);

        // Concurrent execution should show some speedup(at least 20% faster)
        var speedup = sequentialTime / (double)concurrentTime;
        LogSpeedupFromConcurrency(_logger, speedup, null);

        _ = speedup.Should().BeGreaterThan(1.2, $"Insufficient speedup from concurrency: {speedup:F2}x");

        await compiledKernel.DisposeAsync();
    }

    private static async Task<long> MeasureExecutionTime(Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _accelerator?.Dispose();
        _backend?.Dispose();
        _loggerFactory?.Dispose();
        _disposed = true;
    }
}
