// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Backends.Metal.MPS;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Benchmarks.Metal;

/// <summary>
/// Benchmarks to verify 3x+ speedup claim for MPS operations.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporter]
public class MPSPerformanceBenchmarks : IDisposable
{
    private IntPtr _device;
    private MetalMPSOrchestrator? _orchestrator;
    private float[]? _matrixA;
    private float[]? _matrixB;
    private float[]? _matrixC;
    private float[]? _vectorInput;
    private float[]? _vectorOutput;

    [Params(16, 32, 64, 128, 256, 512)]
    public int MatrixSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!MetalNative.IsMetalSupported())
        {
            throw new InvalidOperationException("Metal is not supported on this system");
        }

        _device = MetalNative.CreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal device");
        }

        _orchestrator = new MetalMPSOrchestrator(_device, NullLogger<MetalMPSOrchestrator>.Instance);

        // Allocate matrices
        int size = MatrixSize * MatrixSize;
        _matrixA = new float[size];
        _matrixB = new float[size];
        _matrixC = new float[size];

        // Fill with random data
        var random = new Random(42);
        for (int i = 0; i < size; i++)
        {
            _matrixA[i] = (float)random.NextDouble();
            _matrixB[i] = (float)random.NextDouble();
        }

        // Allocate vectors for activation benchmarks
        _vectorInput = new float[size];
        _vectorOutput = new float[size];
        for (int i = 0; i < size; i++)
        {
            _vectorInput[i] = (float)(random.NextDouble() * 2 - 1); // Range [-1, 1]
        }
    }

    [Benchmark(Description = "MPS Matrix Multiply (Auto-select)")]
    public void MatrixMultiply_MPS()
    {
        _orchestrator!.MatrixMultiply(
            _matrixA!, MatrixSize, MatrixSize,
            _matrixB!, MatrixSize, MatrixSize,
            _matrixC!, MatrixSize, MatrixSize);
    }

    [Benchmark(Description = "MPS Matrix Multiply (Force CPU)", Baseline = true)]
    public void MatrixMultiply_CPU()
    {
        // Force CPU by using orchestrator with small size threshold exceeded
        // This simulates CPU-only path
        MatrixMultiplyCPU(
            _matrixA!, MatrixSize, MatrixSize,
            _matrixB!, MatrixSize, MatrixSize,
            _matrixC!, MatrixSize, MatrixSize);
    }

    [Benchmark(Description = "MPS ReLU Activation (Auto-select)")]
    public void ReLU_MPS()
    {
        _orchestrator!.ReLU(_vectorInput!, _vectorOutput!);
    }

    [Benchmark(Description = "MPS ReLU Activation (CPU Baseline)", Baseline = false)]
    public void ReLU_CPU()
    {
        for (int i = 0; i < _vectorInput!.Length; i++)
        {
            _vectorOutput![i] = Math.Max(0, _vectorInput[i]);
        }
    }

    private static void MatrixMultiplyCPU(
        float[] a, int rowsA, int colsA,
        float[] b, int rowsB, int colsB,
        float[] c, int rowsC, int colsC)
    {
        for (int i = 0; i < rowsC; i++)
        {
            for (int j = 0; j < colsC; j++)
            {
                float sum = 0;
                for (int k = 0; k < colsA; k++)
                {
                    sum += a[i * colsA + k] * b[k * colsB + j];
                }
                c[i * colsC + j] = sum;
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    public void Dispose()
    {
        _orchestrator?.Dispose();

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
            _device = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Detailed comparison benchmark between MPS and CPU for different operation types.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporter]
public class MPSDetailedComparisonBenchmarks : IDisposable
{
    private IntPtr _device;
    private MetalPerformanceShadersBackend? _mpsBackend;
    private float[]? _largeMatrix;
    private float[]? _largeVector;
    private float[]? _result;

    [Params(1024, 2048, 4096)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!MetalNative.IsMetalSupported())
        {
            throw new InvalidOperationException("Metal is not supported");
        }

        _device = MetalNative.CreateSystemDefaultDevice();
        var logger = NullLogger<MetalPerformanceShadersBackend>.Instance;
        _mpsBackend = new MetalPerformanceShadersBackend(_device, logger);

        _largeMatrix = new float[DataSize];
        _largeVector = new float[DataSize];
        _result = new float[DataSize];

        var random = new Random(42);
        for (int i = 0; i < DataSize; i++)
        {
            _largeMatrix[i] = (float)random.NextDouble();
            _largeVector[i] = (float)random.NextDouble();
        }
    }

    [Benchmark(Description = "MPS BLAS Performance")]
    public void MPS_BLAS_Performance()
    {
        // Square root to get matrix dimension
        int dim = (int)Math.Sqrt(DataSize);
        _mpsBackend!.MatrixMultiply(
            _largeMatrix!, dim, dim,
            _largeMatrix!, dim, dim,
            _result!, dim, dim);
    }

    [Benchmark(Description = "MPS Neural Network Performance")]
    public void MPS_NeuralNetwork_Performance()
    {
        _mpsBackend!.ReLU(_largeVector!, _result!);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    public void Dispose()
    {
        _mpsBackend?.Dispose();

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
            _device = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
