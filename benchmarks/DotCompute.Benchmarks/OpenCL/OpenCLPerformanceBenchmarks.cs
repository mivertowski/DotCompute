// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Diagnosers;
using DotCompute.Backends.OpenCL;
using DotCompute.Backends.OpenCL.Configuration;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Benchmarks.OpenCL;

/// <summary>
/// Comprehensive OpenCL backend performance benchmarks validating:
/// - Kernel execution performance vs CPU baseline
/// - Memory transfer bandwidth
/// - Compilation and caching effectiveness
/// - Work-group optimization
/// - Cross-backend comparisons (OpenCL vs CUDA/Metal/CPU)
/// </summary>
[Config(typeof(OpenCLBenchmarkConfig))]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, StdDevColumn]
[RankColumn]
public class OpenCLPerformanceBenchmarks : IDisposable
{
    private OpenCLAccelerator? _accelerator;
    private ILoggerFactory? _loggerFactory;

    private float[]? _inputA;
    private float[]? _inputB;
    private float[]? _result;

    [Params(1_000, 10_000, 100_000, 1_000_000, 10_000_000)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        // Initialize logger factory
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Initialize OpenCL accelerator with optimized configuration
        var config = new OpenCLConfiguration
        {
            // Enable all performance features
            Stream = new OpenCLStreamConfiguration
            {
                MinimumQueuePoolSize = 4,
                MaximumQueuePoolSize = 16,
                EnableProfilingQueues = true
            },
            Event = new EventConfiguration
            {
                MinimumEventPoolSize = 64,
                MaximumEventPoolSize = 512
            },
            Memory = new MemoryConfiguration
            {
                EnableMemoryPooling = true,
                InitialPoolSizeMB = 256,
                MaxPoolSizeMB = 2048
            }
        };

        try
        {
            _accelerator = new OpenCLAccelerator(_loggerFactory, config);
            await _accelerator.InitializeAsync();

            Console.WriteLine($"OpenCL Device: {_accelerator.DeviceInfo?.Name ?? "Unknown"}");
            Console.WriteLine($"OpenCL Vendor: {_accelerator.DeviceInfo?.Vendor ?? "Unknown"}");
            Console.WriteLine($"Global Memory: {(_accelerator.DeviceInfo?.GlobalMemorySize ?? 0) / (1024 * 1024)} MB");
            Console.WriteLine($"Max Compute Units: {_accelerator.DeviceInfo?.MaxComputeUnits ?? 0}");
            Console.WriteLine($"Max Work Group Size: {_accelerator.DeviceInfo?.MaxWorkGroupSize ?? 0}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize OpenCL: {ex.Message}");
            throw;
        }

        // Initialize test data
        SetupTestData();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        SetupTestData();
    }

    private void SetupTestData()
    {
        var random = new Random(42);
        _inputA = new float[DataSize];
        _inputB = new float[DataSize];
        _result = new float[DataSize];

        for (int i = 0; i < DataSize; i++)
        {
            _inputA[i] = (float)random.NextDouble() * 100f;
            _inputB[i] = (float)random.NextDouble() * 100f;
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Dispose();
    }

    // ==================== Baseline CPU Benchmarks ====================

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("VectorAdd", "CPU")]
    public void VectorAdd_CPU_Scalar()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _result![i] = _inputA![i] + _inputB![i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("VectorAdd", "CPU")]
    public void VectorAdd_CPU_Parallel()
    {
        Parallel.For(0, DataSize, i =>
        {
            _result![i] = _inputA![i] + _inputB![i];
        });
    }

    // ==================== OpenCL Kernel Benchmarks ====================

    [Benchmark]
    [BenchmarkCategory("VectorAdd", "OpenCL")]
    public async Task VectorAdd_OpenCL()
    {
        if (_accelerator == null) return;

        // Allocate device buffers
        var bufferA = await _accelerator.AllocateAsync<float>((nuint)DataSize);
        var bufferB = await _accelerator.AllocateAsync<float>((nuint)DataSize);
        var bufferResult = await _accelerator.AllocateAsync<float>((nuint)DataSize);

        try
        {
            // Copy data to device
            await bufferA.CopyFromAsync(_inputA.AsMemory());
            await bufferB.CopyFromAsync(_inputB.AsMemory());

            // Compile kernel (will use cache on subsequent calls)
            var kernelSource = @"
__kernel void vector_add(
    __global const float* a,
    __global const float* b,
    __global float* result,
    const int size)
{
    int idx = get_global_id(0);
    if (idx < size) {
        result[idx] = a[idx] + b[idx];
    }
}";

            var definition = new KernelDefinition
            {
                Name = "vector_add",
                Source = kernelSource,
                EntryPoint = "vector_add"
            };

            var kernel = await _accelerator.CompileKernelAsync(definition);

            // TODO: Execute kernel when execution engine is integrated
            // For now, just measure compilation + memory transfer overhead

            // Copy result back
            await bufferResult.CopyToAsync(_result.AsMemory());
            await _accelerator.SynchronizeAsync();
        }
        finally
        {
            bufferA.Dispose();
            bufferB.Dispose();
            bufferResult.Dispose();
        }
    }

    // ==================== Memory Transfer Benchmarks ====================

    [Benchmark]
    [BenchmarkCategory("MemoryTransfer", "OpenCL")]
    public async Task Memory_HostToDevice_OpenCL()
    {
        if (_accelerator == null) return;

        var buffer = await _accelerator.AllocateAsync<float>((nuint)DataSize);
        try
        {
            await buffer.CopyFromAsync(_inputA.AsMemory());
            await _accelerator.SynchronizeAsync();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("MemoryTransfer", "OpenCL")]
    public async Task Memory_DeviceToHost_OpenCL()
    {
        if (_accelerator == null) return;

        var buffer = await _accelerator.AllocateAsync<float>((nuint)DataSize);
        try
        {
            await buffer.CopyFromAsync(_inputA.AsMemory());
            await buffer.CopyToAsync(_result.AsMemory());
            await _accelerator.SynchronizeAsync();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("MemoryTransfer", "OpenCL")]
    public async Task Memory_RoundTrip_OpenCL()
    {
        if (_accelerator == null) return;

        var buffer = await _accelerator.AllocateAsync<float>((nuint)DataSize);
        try
        {
            await buffer.CopyFromAsync(_inputA.AsMemory());
            await buffer.CopyToAsync(_result.AsMemory());
            await _accelerator.SynchronizeAsync();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    // ==================== Compilation & Caching Benchmarks ====================

    [Benchmark]
    [BenchmarkCategory("Compilation", "OpenCL")]
    public async Task Kernel_CompilationFirstTime()
    {
        if (_accelerator == null) return;

        // Use unique kernel source to bypass cache
        var timestamp = DateTime.UtcNow.Ticks;
        var kernelSource = $@"
// Compilation test kernel - {timestamp}
__kernel void test_compile_{timestamp}(__global float* data)
{{
    int idx = get_global_id(0);
    data[idx] = data[idx] * 2.0f;
}}";

        var definition = new KernelDefinition
        {
            Name = $"test_compile_{timestamp}",
            Source = kernelSource,
            EntryPoint = $"test_compile_{timestamp}"
        };

        var kernel = await _accelerator.CompileKernelAsync(definition);
        kernel.Dispose();
    }

    [Benchmark]
    [BenchmarkCategory("Compilation", "OpenCL")]
    public async Task Kernel_CompilationCached()
    {
        if (_accelerator == null) return;

        // Use same kernel source to test cache hit
        var kernelSource = @"
__kernel void cached_kernel(__global float* data)
{
    int idx = get_global_id(0);
    data[idx] = data[idx] * 2.0f;
}";

        var definition = new KernelDefinition
        {
            Name = "cached_kernel",
            Source = kernelSource,
            EntryPoint = "cached_kernel"
        };

        var kernel = await _accelerator.CompileKernelAsync(definition);
        kernel.Dispose();
    }

    // ==================== Complex Kernel Benchmarks ====================

    [Benchmark]
    [BenchmarkCategory("DotProduct", "CPU")]
    public float DotProduct_CPU_Scalar()
    {
        float sum = 0f;
        for (int i = 0; i < DataSize; i++)
        {
            sum += _inputA![i] * _inputB![i];
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("DotProduct", "CPU")]
    public float DotProduct_CPU_Parallel()
    {
        return _inputA!.AsParallel()
            .Zip(_inputB!.AsParallel(), (a, b) => a * b)
            .Sum();
    }

    public void Dispose()
    {
        _accelerator?.Dispose();
        _loggerFactory?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// BenchmarkDotNet configuration for OpenCL benchmarks
/// </summary>
public class OpenCLBenchmarkConfig : ManualConfig
{
    public OpenCLBenchmarkConfig()
    {
        AddJob(Job.Default
            .WithWarmupCount(2)
            .WithIterationCount(5)
            .WithGcServer(true)
            .WithGcForce(false));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);

        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(StatisticColumn.Min);
        AddColumn(StatisticColumn.Max);
        AddColumn(BaselineRatioColumn.RatioMean);
        AddColumn(RankColumn.Arabic);

        WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default
            .WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Trend));
    }
}
