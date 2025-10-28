// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;

namespace DotCompute.Core.Benchmarks;

/// <summary>
/// Comprehensive SIMD operations performance benchmarks for DotCompute.
/// Validates and measures the performance improvements from SIMD vectorization
/// across different operation types, data sizes, and CPU architectures.
///
/// Benchmarks cover:
/// - Vector arithmetic operations (add, multiply, fused multiply-add)
/// - Vector comparison and logical operations
/// - Memory bandwidth and access patterns
/// - Scalar vs Vector vs Intrinsics performance comparison
/// - Cross-platform SIMD capabilities (AVX2, AVX512, NEON)
/// - Real-world computational kernels
///
/// Performance targets based on DotCompute claims:
/// - 3.7x speedup for vectorized operations
/// - Sub-10ms processing latency for typical workloads
/// - 90%+ memory bandwidth utilization
/// </summary>
[Config(typeof(SimdBenchmarkConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses, HardwareCounter.InstructionRetired)]
public class SimdOperationsBenchmark
{
    private float[] _inputA = null!;
    private float[] _inputB = null!;
    private float[] _output = null!;

    private double[] _inputDoubleA = null!;
    private double[] _inputDoubleB = null!;
    private double[] _outputDouble = null!;

    private int[] _inputIntA = null!;
    private int[] _inputIntB = null!;
    private int[] _outputInt = null!;

    private Vector<float>[] _vectorInputA = null!;
    private Vector<float>[] _vectorInputB = null!;
    private Vector<float>[] _vectorOutput = null!;

    [Params(1024, 8192, 65536, 524288, 4194304)]
    public int DataSize { get; set; }

    [Params(OperationType.Add, OperationType.Multiply, OperationType.FusedMultiplyAdd, OperationType.Dot, OperationType.Normalize)]
    public OperationType Operation { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Initialize test data with deterministic random values
        var random = new Random(42);

        // Float arrays
        _inputA = new float[DataSize];
        _inputB = new float[DataSize];
        _output = new float[DataSize];

        // Double arrays
        _inputDoubleA = new double[DataSize];
        _inputDoubleB = new double[DataSize];
        _outputDouble = new double[DataSize];

        // Integer arrays
        _inputIntA = new int[DataSize];
        _inputIntB = new int[DataSize];
        _outputInt = new int[DataSize];

        // Vector arrays
        var vectorCount = DataSize / Vector<float>.Count;
        _vectorInputA = new Vector<float>[vectorCount];
        _vectorInputB = new Vector<float>[vectorCount];
        _vectorOutput = new Vector<float>[vectorCount];

        // Fill with test data
        for (int i = 0; i < DataSize; i++)
        {
            _inputA[i] = (float)random.NextDouble() * 100f - 50f; // Range [-50, 50]
            _inputB[i] = (float)random.NextDouble() * 100f - 50f;

            _inputDoubleA[i] = random.NextDouble() * 100.0 - 50.0;
            _inputDoubleB[i] = random.NextDouble() * 100.0 - 50.0;

            _inputIntA[i] = random.Next(-1000, 1000);
            _inputIntB[i] = random.Next(-1000, 1000);
        }

        // Initialize vector data
        for (int i = 0; i < vectorCount; i++)
        {
            var spanA = _inputA.AsSpan(i * Vector<float>.Count, Vector<float>.Count);
            var spanB = _inputB.AsSpan(i * Vector<float>.Count, Vector<float>.Count);

            _vectorInputA[i] = new Vector<float>(spanA);
            _vectorInputB[i] = new Vector<float>(spanB);
        }

        // Warmup SIMD capabilities
        DetectAndWarmupSimdCapabilities();
    }

    #region Scalar vs Vector Comparison Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Comparison")]
    public void Scalar_FloatAdd()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _output[i] = _inputA[i] + _inputB[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Comparison")]
    public void Vector_FloatAdd()
    {
        var vectorCount = DataSize / Vector<float>.Count;
        var remainder = DataSize % Vector<float>.Count;

        for (int i = 0; i < vectorCount; i++)
        {
            var vecA = new Vector<float>(_inputA, i * Vector<float>.Count);
            var vecB = new Vector<float>(_inputB, i * Vector<float>.Count);
            var result = vecA + vecB;
            result.CopyTo(_output, i * Vector<float>.Count);
        }

        // Handle remainder elements
        for (int i = vectorCount * Vector<float>.Count; i < DataSize; i++)
        {
            _output[i] = _inputA[i] + _inputB[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Comparison")]
    public void Scalar_FloatMultiply()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _output[i] = _inputA[i] * _inputB[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Comparison")]
    public void Vector_FloatMultiply()
    {
        var vectorCount = DataSize / Vector<float>.Count;
        var remainder = DataSize % Vector<float>.Count;

        for (int i = 0; i < vectorCount; i++)
        {
            var vecA = new Vector<float>(_inputA, i * Vector<float>.Count);
            var vecB = new Vector<float>(_inputB, i * Vector<float>.Count);
            var result = vecA * vecB;
            result.CopyTo(_output, i * Vector<float>.Count);
        }

        // Handle remainder elements
        for (int i = vectorCount * Vector<float>.Count; i < DataSize; i++)
        {
            _output[i] = _inputA[i] * _inputB[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Comparison")]
    public void Scalar_FusedMultiplyAdd()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _output[i] = Math.FusedMultiplyAdd(_inputA[i], _inputB[i], _output[i]);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Comparison")]
    public void Vector_FusedMultiplyAdd()
    {
        var vectorCount = DataSize / Vector<float>.Count;

        for (int i = 0; i < vectorCount; i++)
        {
            var vecA = new Vector<float>(_inputA, i * Vector<float>.Count);
            var vecB = new Vector<float>(_inputB, i * Vector<float>.Count);
            var vecC = new Vector<float>(_output, i * Vector<float>.Count);

            // Vector<T> doesn't have FMA, so simulate: (A * B) + C
            var result = vecA * vecB + vecC;
            result.CopyTo(_output, i * Vector<float>.Count);
        }

        // Handle remainder
        for (int i = vectorCount * Vector<float>.Count; i < DataSize; i++)
        {
            _output[i] = Math.FusedMultiplyAdd(_inputA[i], _inputB[i], _output[i]);
        }
    }

    #endregion

    #region AVX2 Intrinsics Benchmarks

    [Benchmark]
    [BenchmarkCategory("AVX2")]
    public unsafe void AVX2_FloatAdd()
    {
        if (!Avx2.IsSupported)
        {
            Vector_FloatAdd(); // Fallback
            return;
        }

        var vectorCount = DataSize / 8; // AVX2 processes 8 floats at once

        fixed (float* pInputA = _inputA)
        fixed (float* pInputB = _inputB)
        fixed (float* pOutput = _output)
        {
            for (int i = 0; i < vectorCount; i++)
            {
                var vecA = Avx.LoadVector256(pInputA + i * 8);
                var vecB = Avx.LoadVector256(pInputB + i * 8);
                var result = Avx.Add(vecA, vecB);
                Avx.Store(pOutput + i * 8, result);
            }
        }

        // Handle remainder
        for (int i = vectorCount * 8; i < DataSize; i++)
        {
            _output[i] = _inputA[i] + _inputB[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("AVX2")]
    public unsafe void AVX2_FloatMultiply()
    {
        if (!Avx2.IsSupported)
        {
            Vector_FloatMultiply(); // Fallback
            return;
        }

        var vectorCount = DataSize / 8;

        fixed (float* pInputA = _inputA)
        fixed (float* pInputB = _inputB)
        fixed (float* pOutput = _output)
        {
            for (int i = 0; i < vectorCount; i++)
            {
                var vecA = Avx.LoadVector256(pInputA + i * 8);
                var vecB = Avx.LoadVector256(pInputB + i * 8);
                var result = Avx.Multiply(vecA, vecB);
                Avx.Store(pOutput + i * 8, result);
            }
        }

        // Handle remainder
        for (int i = vectorCount * 8; i < DataSize; i++)
        {
            _output[i] = _inputA[i] * _inputB[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("AVX2")]
    public unsafe void AVX2_FusedMultiplyAdd()
    {
        if (!Fma.IsSupported)
        {
            AVX2_FloatMultiply(); // Fallback to multiply
            return;
        }

        var vectorCount = DataSize / 8;

        fixed (float* pInputA = _inputA)
        fixed (float* pInputB = _inputB)
        fixed (float* pOutput = _output)
        {
            for (int i = 0; i < vectorCount; i++)
            {
                var vecA = Avx.LoadVector256(pInputA + i * 8);
                var vecB = Avx.LoadVector256(pInputB + i * 8);
                var vecC = Avx.LoadVector256(pOutput + i * 8);
                var result = Fma.MultiplyAdd(vecA, vecB, vecC);
                Avx.Store(pOutput + i * 8, result);
            }
        }

        // Handle remainder
        for (int i = vectorCount * 8; i < DataSize; i++)
        {
            _output[i] = Math.FusedMultiplyAdd(_inputA[i], _inputB[i], _output[i]);
        }
    }

    #endregion

    #region AVX512 Intrinsics Benchmarks

    [Benchmark]
    [BenchmarkCategory("AVX512")]
    public unsafe void AVX512_FloatAdd()
    {
        if (!Avx512F.IsSupported)
        {
            AVX2_FloatAdd(); // Fallback to AVX2
            return;
        }

        var vectorCount = DataSize / 16; // AVX512 processes 16 floats at once

        fixed (float* pInputA = _inputA)
        fixed (float* pInputB = _inputB)
        fixed (float* pOutput = _output)
        {
            for (int i = 0; i < vectorCount; i++)
            {
                var vecA = Avx512F.LoadVector512(pInputA + i * 16);
                var vecB = Avx512F.LoadVector512(pInputB + i * 16);
                var result = Avx512F.Add(vecA, vecB);
                Avx512F.Store(pOutput + i * 16, result);
            }
        }

        // Handle remainder
        for (int i = vectorCount * 16; i < DataSize; i++)
        {
            _output[i] = _inputA[i] + _inputB[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("AVX512")]
    public unsafe void AVX512_FloatMultiply()
    {
        if (!Avx512F.IsSupported)
        {
            AVX2_FloatMultiply(); // Fallback
            return;
        }

        var vectorCount = DataSize / 16;

        fixed (float* pInputA = _inputA)
        fixed (float* pInputB = _inputB)
        fixed (float* pOutput = _output)
        {
            for (int i = 0; i < vectorCount; i++)
            {
                var vecA = Avx512F.LoadVector512(pInputA + i * 16);
                var vecB = Avx512F.LoadVector512(pInputB + i * 16);
                var result = Avx512F.Multiply(vecA, vecB);
                Avx512F.Store(pOutput + i * 16, result);
            }
        }

        // Handle remainder
        for (int i = vectorCount * 16; i < DataSize; i++)
        {
            _output[i] = _inputA[i] * _inputB[i];
        }
    }

    #endregion

    #region Complex Mathematical Operations

    [Benchmark]
    [BenchmarkCategory("Math")]
    public void Scalar_DotProduct()
    {
        float sum = 0f;
        for (int i = 0; i < DataSize; i++)
        {
            sum += _inputA[i] * _inputB[i];
        }
        _output[0] = sum;
    }

    [Benchmark]
    [BenchmarkCategory("Math")]
    public void Vector_DotProduct()
    {
        var vectorCount = DataSize / Vector<float>.Count;
        var sum = Vector<float>.Zero;

        for (int i = 0; i < vectorCount; i++)
        {
            var vecA = new Vector<float>(_inputA, i * Vector<float>.Count);
            var vecB = new Vector<float>(_inputB, i * Vector<float>.Count);
            sum += vecA * vecB;
        }

        // Sum vector components
        float result = 0f;
        for (int i = 0; i < Vector<float>.Count; i++)
        {
            result += sum[i];
        }

        // Handle remainder
        for (int i = vectorCount * Vector<float>.Count; i < DataSize; i++)
        {
            result += _inputA[i] * _inputB[i];
        }

        _output[0] = result;
    }

    [Benchmark]
    [BenchmarkCategory("Math")]
    public void Scalar_VectorNormalize()
    {
        // Calculate magnitude
        float magnitudeSquared = 0f;
        for (int i = 0; i < DataSize; i++)
        {
            magnitudeSquared += _inputA[i] * _inputA[i];
        }

        float magnitude = MathF.Sqrt(magnitudeSquared);

        // Normalize
        for (int i = 0; i < DataSize; i++)
        {
            _output[i] = _inputA[i] / magnitude;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Math")]
    public void Vector_VectorNormalize()
    {
        var vectorCount = DataSize / Vector<float>.Count;

        // Calculate magnitude squared using vectors
        var sumSquared = Vector<float>.Zero;
        for (int i = 0; i < vectorCount; i++)
        {
            var vec = new Vector<float>(_inputA, i * Vector<float>.Count);
            sumSquared += vec * vec;
        }

        // Sum vector components
        float magnitudeSquared = 0f;
        for (int i = 0; i < Vector<float>.Count; i++)
        {
            magnitudeSquared += sumSquared[i];
        }

        // Handle remainder
        for (int i = vectorCount * Vector<float>.Count; i < DataSize; i++)
        {
            magnitudeSquared += _inputA[i] * _inputA[i];
        }

        float magnitude = MathF.Sqrt(magnitudeSquared);
        var magVector = new Vector<float>(magnitude);

        // Normalize using vectors
        for (int i = 0; i < vectorCount; i++)
        {
            var vec = new Vector<float>(_inputA, i * Vector<float>.Count);
            var result = vec / magVector;
            result.CopyTo(_output, i * Vector<float>.Count);
        }

        // Handle remainder
        for (int i = vectorCount * Vector<float>.Count; i < DataSize; i++)
        {
            _output[i] = _inputA[i] / magnitude;
        }
    }

    #endregion

    #region Memory Bandwidth Benchmarks

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public void MemoryBandwidth_Sequential_Read()
    {
        float sum = 0f;
        for (int i = 0; i < DataSize; i++)
        {
            sum += _inputA[i];
        }
        _output[0] = sum;
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public void MemoryBandwidth_Sequential_Write()
    {
        float value = 42.0f;
        for (int i = 0; i < DataSize; i++)
        {
            _output[i] = value;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public void MemoryBandwidth_Sequential_Copy()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _output[i] = _inputA[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public void MemoryBandwidth_Vector_Copy()
    {
        var vectorCount = DataSize / Vector<float>.Count;

        for (int i = 0; i < vectorCount; i++)
        {
            var vec = new Vector<float>(_inputA, i * Vector<float>.Count);
            vec.CopyTo(_output, i * Vector<float>.Count);
        }

        // Handle remainder
        for (int i = vectorCount * Vector<float>.Count; i < DataSize; i++)
        {
            _output[i] = _inputA[i];
        }
    }

    #endregion

    #region Real-World Computational Kernels

    [Benchmark]
    [BenchmarkCategory("RealWorld")]
    public void Scalar_MatrixVectorMultiply()
    {
        // Simulate matrix-vector multiply for square matrix
        int size = (int)Math.Sqrt(DataSize);
        if (size * size != DataSize) return; // Skip if not perfect square

        for (int row = 0; row < size; row++)
        {
            float sum = 0f;
            for (int col = 0; col < size; col++)
            {
                sum += _inputA[row * size + col] * _inputB[col];
            }
            _output[row] = sum;
        }
    }

    [Benchmark]
    [BenchmarkCategory("RealWorld")]
    public void Vector_MatrixVectorMultiply()
    {
        int size = (int)Math.Sqrt(DataSize);
        if (size * size != DataSize) return;

        for (int row = 0; row < size; row++)
        {
            var sum = Vector<float>.Zero;
            int vectorCount = size / Vector<float>.Count;

            for (int colVec = 0; colVec < vectorCount; colVec++)
            {
                var matrixVec = new Vector<float>(_inputA, row * size + colVec * Vector<float>.Count);
                var vectorVec = new Vector<float>(_inputB, colVec * Vector<float>.Count);
                sum += matrixVec * vectorVec;
            }

            // Sum vector components
            float result = 0f;
            for (int i = 0; i < Vector<float>.Count; i++)
            {
                result += sum[i];
            }

            // Handle remainder
            for (int col = vectorCount * Vector<float>.Count; col < size; col++)
            {
                result += _inputA[row * size + col] * _inputB[col];
            }

            _output[row] = result;
        }
    }

    [Benchmark]
    [BenchmarkCategory("RealWorld")]
    public void Scalar_ConvolutionFilter()
    {
        // Simple 3x3 convolution simulation
        const int kernelSize = 3;
        int width = (int)Math.Sqrt(DataSize);
        if (width * width != DataSize) return;

        float[] kernel = { -1, -1, -1, -1, 8, -1, -1, -1, -1 }; // Edge detection kernel

        for (int y = 1; y < width - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                float sum = 0f;
                for (int ky = 0; ky < kernelSize; ky++)
                {
                    for (int kx = 0; kx < kernelSize; kx++)
                    {
                        int pixelY = y + ky - 1;
                        int pixelX = x + kx - 1;
                        sum += _inputA[pixelY * width + pixelX] * kernel[ky * kernelSize + kx];
                    }
                }
                _output[y * width + x] = sum;
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("RealWorld")]
    public void Scalar_SignalProcessing_FFT_Component()
    {
        // Simplified FFT butterfly operation simulation
        for (int i = 0; i < DataSize / 2; i++)
        {
            float real1 = _inputA[i * 2];
            float imag1 = _inputA[i * 2 + 1];
            float real2 = _inputB[i * 2];
            float imag2 = _inputB[i * 2 + 1];

            // Butterfly operation
            _output[i * 2] = real1 + real2;
            _output[i * 2 + 1] = imag1 + imag2;
        }
    }

    #endregion

    #region Data Type Variations

    [Benchmark]
    [BenchmarkCategory("DataTypes")]
    public void Double_VectorAdd()
    {
        var vectorCount = DataSize / Vector<double>.Count;

        for (int i = 0; i < vectorCount; i++)
        {
            var vecA = new Vector<double>(_inputDoubleA, i * Vector<double>.Count);
            var vecB = new Vector<double>(_inputDoubleB, i * Vector<double>.Count);
            var result = vecA + vecB;
            result.CopyTo(_outputDouble, i * Vector<double>.Count);
        }

        // Handle remainder
        for (int i = vectorCount * Vector<double>.Count; i < DataSize; i++)
        {
            _outputDouble[i] = _inputDoubleA[i] + _inputDoubleB[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("DataTypes")]
    public void Integer_VectorAdd()
    {
        var vectorCount = DataSize / Vector<int>.Count;

        for (int i = 0; i < vectorCount; i++)
        {
            var vecA = new Vector<int>(_inputIntA, i * Vector<int>.Count);
            var vecB = new Vector<int>(_inputIntB, i * Vector<int>.Count);
            var result = vecA + vecB;
            result.CopyTo(_outputInt, i * Vector<int>.Count);
        }

        // Handle remainder
        for (int i = vectorCount * Vector<int>.Count; i < DataSize; i++)
        {
            _outputInt[i] = _inputIntA[i] + _inputIntB[i];
        }
    }

    #endregion

    #region Helper Methods

    private void DetectAndWarmupSimdCapabilities()
    {
        Console.WriteLine($"Vector<float>.Count: {Vector<float>.Count}");
        Console.WriteLine($"Vector.IsHardwareAccelerated: {Vector.IsHardwareAccelerated}");

        if (Sse.IsSupported) Console.WriteLine("SSE: Supported");
        if (Sse2.IsSupported) Console.WriteLine("SSE2: Supported");
        if (Sse3.IsSupported) Console.WriteLine("SSE3: Supported");
        if (Ssse3.IsSupported) Console.WriteLine("SSSE3: Supported");
        if (Sse41.IsSupported) Console.WriteLine("SSE4.1: Supported");
        if (Sse42.IsSupported) Console.WriteLine("SSE4.2: Supported");
        if (Avx.IsSupported) Console.WriteLine("AVX: Supported");
        if (Avx2.IsSupported) Console.WriteLine("AVX2: Supported");
        if (Fma.IsSupported) Console.WriteLine("FMA: Supported");
        if (Avx512F.IsSupported) Console.WriteLine("AVX512F: Supported");

        // Warmup operations
        Vector_FloatAdd();
        if (Avx2.IsSupported) AVX2_FloatAdd();
        if (Avx512F.IsSupported) AVX512_FloatAdd();
    }

    #endregion
}

public enum OperationType
{
    Add,
    Multiply,
    FusedMultiplyAdd,
    Dot,
    Normalize
}

public class SimdBenchmarkConfig : ManualConfig
{
    public SimdBenchmarkConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Net90)
            .WithPlatform(Platform.X64)
            .WithId("SIMD_Benchmark"));

        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(StatisticColumn.Median);
        AddColumn(StatisticColumn.Min);
        AddColumn(StatisticColumn.Max);
        AddColumn(BaselineRatioColumn.RatioMean);
        AddColumn(RankColumn.Arabic);

        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);

        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);

        SummaryStyle = BenchmarkDotNet.Reports.SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend)
            .WithTimeUnit(Perfolizer.Horology.TimeUnit.Microsecond)
            .WithSizeUnit(Perfolizer.Horology.SizeUnit.KB);
    }
}

/// <summary>
/// Program entry point for running SIMD benchmarks
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<SimdOperationsBenchmark>();

        // Analysis and validation of results
        AnalyzePerformanceResults(summary);
    }

    private static void AnalyzePerformanceResults(BenchmarkDotNet.Reports.Summary summary)
    {
        Console.WriteLine("\n=== SIMD Performance Analysis ===");

        var scalarBaselines = summary.Reports
            .Where(r => r.BenchmarkCase.DisplayInfo.Contains("Scalar_"))
            .ToDictionary(r => ExtractOperationType(r.BenchmarkCase.DisplayInfo), r => r.ResultStatistics?.Mean ?? 0);

        var vectorResults = summary.Reports
            .Where(r => r.BenchmarkCase.DisplayInfo.Contains("Vector_"))
            .ToDictionary(r => ExtractOperationType(r.BenchmarkCase.DisplayInfo), r => r.ResultStatistics?.Mean ?? 0);

        foreach (var kvp in scalarBaselines)
        {
            if (vectorResults.TryGetValue(kvp.Key, out var vectorTime))
            {
                var speedup = kvp.Value / vectorTime;
                Console.WriteLine($"{kvp.Key}: {speedup:F2}x speedup (Vector vs Scalar)");

                // Validate against DotCompute performance claims
                if (speedup >= 3.0)
                {
                    Console.WriteLine($"  ✅ Meets performance target (>3x speedup)");
                }
                else if (speedup >= 2.0)
                {
                    Console.WriteLine($"  ⚠️  Good performance but below 3x target");
                }
                else
                {
                    Console.WriteLine($"  ❌ Below expected performance target");
                }
            }
        }

        Console.WriteLine("\n=== Memory Bandwidth Analysis ===");
        var memoryTests = summary.Reports
            .Where(r => r.BenchmarkCase.DisplayInfo.Contains("MemoryBandwidth_"))
            .ToList();

        foreach (var test in memoryTests)
        {
            var throughputGBps = CalculateMemoryThroughput(test);
            Console.WriteLine($"{ExtractTestName(test.BenchmarkCase.DisplayInfo)}: {throughputGBps:F2} GB/s");
        }
    }

    private static string ExtractOperationType(string displayInfo)
    {
        // Extract operation type from benchmark display info
        var parts = displayInfo.Split('_');
        return parts.Length > 1 ? parts[1] : displayInfo;
    }

    private static string ExtractTestName(string displayInfo)
    {
        return displayInfo.Split('_').LastOrDefault() ?? displayInfo;
    }

    private static double CalculateMemoryThroughput(BenchmarkDotNet.Reports.BenchmarkReport report)
    {
        // Simplified throughput calculation
        // This would need to be enhanced based on actual data sizes and access patterns
        var dataSize = 4194304; // Largest test size in bytes (4MB)
        var meanTimeNs = report.ResultStatistics?.Mean ?? 1.0;
        var meanTimeSeconds = meanTimeNs / 1_000_000_000.0;

        return (dataSize * sizeof(float)) / (meanTimeSeconds * 1_073_741_824.0); // Convert to GB/s
    }
}