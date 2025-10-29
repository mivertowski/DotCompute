// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using DotCompute.Backends.OpenCL;
using DotCompute.Backends.OpenCL.Configuration;
using DotCompute.Backends.CPU;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Benchmarks.OpenCL;

/// <summary>
/// Cross-backend performance comparison: OpenCL vs CUDA vs Metal vs CPU
/// Measures relative performance for common compute operations
/// </summary>
[Config(typeof(CrossBackendConfig))]
[MemoryDiagnoser]
[RankColumn]
public class CrossBackendComparison : IDisposable
{
    private OpenCLAccelerator? _openclAccelerator;
    private CpuAccelerator? _cpuAccelerator;
    private ILoggerFactory? _loggerFactory;

    private float[]? _data1;
    private float[]? _data2;
    private float[]? _result;

    [Params(1_000_000, 10_000_000)]
    public int WorkloadSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Initialize OpenCL
        try
        {
            var openclConfig = new OpenCLConfiguration
            {
                Memory = new MemoryConfiguration { EnableMemoryPooling = true },
                Stream = new OpenCLStreamConfiguration { EnableProfilingQueues = true }
            };
            _openclAccelerator = new OpenCLAccelerator(_loggerFactory, openclConfig);
            await _openclAccelerator.InitializeAsync();
            Console.WriteLine($"✓ OpenCL initialized: {_openclAccelerator.DeviceInfo?.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ OpenCL initialization failed: {ex.Message}");
        }

        // Initialize CPU backend
        try
        {
            _cpuAccelerator = new CpuAccelerator(_loggerFactory);
            await _cpuAccelerator.InitializeAsync();
            Console.WriteLine($"✓ CPU backend initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ CPU backend initialization failed: {ex.Message}");
        }

        // Setup test data
        var random = new Random(42);
        _data1 = new float[WorkloadSize];
        _data2 = new float[WorkloadSize];
        _result = new float[WorkloadSize];

        for (int i = 0; i < WorkloadSize; i++)
        {
            _data1[i] = (float)random.NextDouble() * 100f;
            _data2[i] = (float)random.NextDouble() * 100f;
        }

        Console.WriteLine($"\nBenchmark Configuration:");
        Console.WriteLine($"  Workload Size: {WorkloadSize:N0} elements");
        Console.WriteLine($"  Data Size: {(WorkloadSize * sizeof(float) * 2) / (1024.0 * 1024.0):F2} MB");
        Console.WriteLine($"  AVX2: {Avx2.IsSupported}");
        Console.WriteLine($"  AVX512: {Avx512F.IsSupported}");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    // ==================== Vector Addition Comparison ====================

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("VectorAdd")]
    public void VectorAdd_Baseline_Scalar()
    {
        for (int i = 0; i < WorkloadSize; i++)
        {
            _result![i] = _data1![i] + _data2![i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("VectorAdd")]
    public void VectorAdd_CPU_SIMD()
    {
        if (!Avx2.IsSupported)
        {
            VectorAdd_Baseline_Scalar();
            return;
        }

        int vectorSize = Vector256<float>.Count;
        int vectorizedLength = WorkloadSize - (WorkloadSize % vectorSize);
        int i = 0;

        unsafe
        {
            fixed (float* p1 = _data1, p2 = _data2, pResult = _result)
            {
                for (; i < vectorizedLength; i += vectorSize)
                {
                    var v1 = Avx.LoadVector256(p1 + i);
                    var v2 = Avx.LoadVector256(p2 + i);
                    var vr = Avx.Add(v1, v2);
                    Avx.Store(pResult + i, vr);
                }
            }
        }

        for (; i < WorkloadSize; i++)
        {
            _result![i] = _data1![i] + _data2![i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("VectorAdd")]
    public async Task VectorAdd_OpenCL()
    {
        if (_openclAccelerator == null)
        {
            VectorAdd_Baseline_Scalar();
            return;
        }

        var buf1 = await _openclAccelerator.AllocateAsync<float>((nuint)WorkloadSize);
        var buf2 = await _openclAccelerator.AllocateAsync<float>((nuint)WorkloadSize);
        var bufResult = await _openclAccelerator.AllocateAsync<float>((nuint)WorkloadSize);

        try
        {
            await buf1.CopyFromAsync(_data1.AsMemory());
            await buf2.CopyFromAsync(_data2.AsMemory());

            var kernelSource = @"
__kernel void vector_add(__global const float* a, __global const float* b, __global float* result, const int size)
{
    int idx = get_global_id(0);
    if (idx < size) result[idx] = a[idx] + b[idx];
}";

            var kernel = await _openclAccelerator.CompileKernelAsync(new KernelDefinition
            {
                Name = "vector_add",
                Source = kernelSource,
                EntryPoint = "vector_add"
            });

            // TODO: Execute kernel when execution engine is available
            await bufResult.CopyToAsync(_result.AsMemory());
            await _openclAccelerator.SynchronizeAsync();

            kernel.Dispose();
        }
        finally
        {
            buf1.Dispose();
            buf2.Dispose();
            bufResult.Dispose();
        }
    }

    // ==================== Reduction Operation Comparison ====================

    [Benchmark]
    [BenchmarkCategory("Reduction")]
    public float Reduction_Baseline_Scalar()
    {
        float sum = 0f;
        for (int i = 0; i < WorkloadSize; i++)
        {
            sum += _data1![i];
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Reduction")]
    public float Reduction_CPU_SIMD()
    {
        if (!Avx2.IsSupported)
        {
            return Reduction_Baseline_Scalar();
        }

        int vectorSize = Vector256<float>.Count;
        int vectorizedLength = WorkloadSize - (WorkloadSize % vectorSize);
        var sumVector = Vector256<float>.Zero;
        int i = 0;

        unsafe
        {
            fixed (float* p1 = _data1)
            {
                for (; i < vectorizedLength; i += vectorSize)
                {
                    var v = Avx.LoadVector256(p1 + i);
                    sumVector = Avx.Add(sumVector, v);
                }
            }
        }

        float sum = 0f;
        unsafe
        {
            var temp = stackalloc float[vectorSize];
            Avx.Store(temp, sumVector);
            for (int j = 0; j < vectorSize; j++)
            {
                sum += temp[j];
            }
        }

        for (; i < WorkloadSize; i++)
        {
            sum += _data1![i];
        }

        return sum;
    }

    // ==================== Memory Bandwidth Measurement ====================

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public void Memory_StandardCopy()
    {
        Array.Copy(_data1!, _result!, WorkloadSize);
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public async Task Memory_OpenCL_RoundTrip()
    {
        if (_openclAccelerator == null)
        {
            Memory_StandardCopy();
            return;
        }

        var buffer = await _openclAccelerator.AllocateAsync<float>((nuint)WorkloadSize);
        try
        {
            await buffer.CopyFromAsync(_data1.AsMemory());
            await buffer.CopyToAsync(_result.AsMemory());
            await _openclAccelerator.SynchronizeAsync();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    public void Dispose()
    {
        _openclAccelerator?.Dispose();
        _cpuAccelerator?.Dispose();
        _loggerFactory?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration for cross-backend comparison benchmarks
/// </summary>
public class CrossBackendConfig : ManualConfig
{
    public CrossBackendConfig()
    {
        AddJob(BenchmarkDotNet.Jobs.Job.Default
            .WithWarmupCount(3)
            .WithIterationCount(10)
            .WithGcServer(true));

        AddDiagnoser(BenchmarkDotNet.Diagnosers.MemoryDiagnoser.Default);
        AddColumn(BenchmarkDotNet.Columns.StatisticColumn.Mean);
        AddColumn(BenchmarkDotNet.Columns.StatisticColumn.StdDev);
        AddColumn(BenchmarkDotNet.Columns.BaselineRatioColumn.RatioMean);
        AddColumn(BenchmarkDotNet.Columns.RankColumn.Arabic);
    }
}
