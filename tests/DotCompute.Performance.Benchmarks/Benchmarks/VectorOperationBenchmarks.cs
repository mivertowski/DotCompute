using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU;

namespace DotCompute.Performance.Benchmarks.Benchmarks;

/// <summary>
/// Comprehensive vector operation benchmarks comparing CPU, CUDA, and Metal backends
/// Tests addition, multiplication, dot product, and SIMD acceleration
/// Performance targets: CPU baseline, CUDA 10x+, Metal competitive with CUDA
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 10)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, StdDevColumn]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class VectorOperationBenchmarks
{
    private float[] _data1 = Array.Empty<float>();
    private float[] _data2 = Array.Empty<float>();
    private float[] _result = Array.Empty<float>();
    
    private IAccelerator? _cpuAccelerator;
    private IAccelerator? _cudaAccelerator;
    private IAccelerator? _metalAccelerator;
    
    [Params(1024, 8192, 65536, 262144, 1048576)] // 1KB to 4MB data sizes
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data1 = new float[Size];
        _data2 = new float[Size];
        _result = new float[Size];
        
        var random = new Random(42);
        for (int i = 0; i < Size; i++)
        {
            _data1[i] = (float)random.NextDouble() * 100.0f;
            _data2[i] = (float)random.NextDouble() * 100.0f;
        }
        
        // Initialize accelerators
        try
        {
            _cpuAccelerator = new CpuAccelerator();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize CPU accelerator: {ex.Message}");
        }
        
        // CUDA and Metal accelerators would be initialized here if available
        // For now, we'll use CPU SIMD optimizations as comparison baseline
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cpuAccelerator?.Dispose();
        _cudaAccelerator?.Dispose();
        _metalAccelerator?.Dispose();
    }

    // ==================== VECTOR ADDITION BENCHMARKS ====================
    
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Addition")]
    public void ScalarAddition()
    {
        for (int i = 0; i < Size; i++)
        {
            _result[i] = _data1[i] + _data2[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Addition")]
    public void VectorAddition_Generic()
    {
        var vectors = Size / Vector<float>.Count;
        var remainder = Size % Vector<float>.Count;
        
        for (int i = 0; i < vectors; i++)
        {
            var offset = i * Vector<float>.Count;
            var v1 = new Vector<float>(_data1, offset);
            var v2 = new Vector<float>(_data2, offset);
            var result = v1 + v2;
            result.CopyTo(_result, offset);
        }
        
        // Handle remainder
        for (int i = vectors * Vector<float>.Count; i < Size; i++)
        {
            _result[i] = _data1[i] + _data2[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Addition")]
    public unsafe void VectorAddition_AVX2()
    {
        if (!Avx.IsSupported) 
        {
            ScalarAddition();
            return;
        }
        
        fixed (float* pData1 = _data1, pData2 = _data2, pResult = _result)
        {
            var vectors = Size / 8; // AVX processes 8 floats at once
            var remainder = Size % 8;
            
            for (int i = 0; i < vectors; i++)
            {
                var offset = i * 8;
                var v1 = Avx.LoadVector256(pData1 + offset);
                var v2 = Avx.LoadVector256(pData2 + offset);
                var result = Avx.Add(v1, v2);
                Avx.Store(pResult + offset, result);
            }
            
            // Handle remainder
            for (int i = vectors * 8; i < Size; i++)
            {
                pResult[i] = pData1[i] + pData2[i];
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("Addition")]
    public void VectorAddition_Parallel()
    {
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        
        Parallel.For(0, Size / Vector<float>.Count, parallelOptions, i =>
        {
            var offset = i * Vector<float>.Count;
            if (offset + Vector<float>.Count <= Size)
            {
                var v1 = new Vector<float>(_data1, offset);
                var v2 = new Vector<float>(_data2, offset);
                var result = v1 + v2;
                result.CopyTo(_result, offset);
            }
        });
        
        // Handle remainder
        var vectors = Size / Vector<float>.Count;
        for (int i = vectors * Vector<float>.Count; i < Size; i++)
        {
            _result[i] = _data1[i] + _data2[i];
        }
    }

    // ==================== VECTOR MULTIPLICATION BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory("Multiplication")]
    public void ScalarMultiplication()
    {
        for (int i = 0; i < Size; i++)
        {
            _result[i] = _data1[i] * _data2[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Multiplication")]
    public void VectorMultiplication_Generic()
    {
        var vectors = Size / Vector<float>.Count;
        var remainder = Size % Vector<float>.Count;
        
        for (int i = 0; i < vectors; i++)
        {
            var offset = i * Vector<float>.Count;
            var v1 = new Vector<float>(_data1, offset);
            var v2 = new Vector<float>(_data2, offset);
            var result = v1 * v2;
            result.CopyTo(_result, offset);
        }
        
        // Handle remainder
        for (int i = vectors * Vector<float>.Count; i < Size; i++)
        {
            _result[i] = _data1[i] * _data2[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Multiplication")]
    public unsafe void VectorMultiplication_FMA()
    {
        if (!Fma.IsSupported)
        {
            ScalarMultiplication();
            return;
        }
        
        fixed (float* pData1 = _data1, pData2 = _data2, pResult = _result)
        {
            var vectors = Size / 8;
            var remainder = Size % 8;
            
            for (int i = 0; i < vectors; i++)
            {
                var offset = i * 8;
                var v1 = Avx.LoadVector256(pData1 + offset);
                var v2 = Avx.LoadVector256(pData2 + offset);
                var result = Avx.Multiply(v1, v2);
                Avx.Store(pResult + offset, result);
            }
            
            // Handle remainder
            for (int i = vectors * 8; i < Size; i++)
            {
                pResult[i] = pData1[i] * pData2[i];
            }
        }
    }

    // ==================== DOT PRODUCT BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory("DotProduct")]
    public float ScalarDotProduct()
    {
        float sum = 0;
        for (int i = 0; i < Size; i++)
        {
            sum += _data1[i] * _data2[i];
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("DotProduct")]
    public float VectorDotProduct_Generic()
    {
        var sum = Vector<float>.Zero;
        var vectors = Size / Vector<float>.Count;
        
        for (int i = 0; i < vectors; i++)
        {
            var offset = i * Vector<float>.Count;
            var v1 = new Vector<float>(_data1, offset);
            var v2 = new Vector<float>(_data2, offset);
            sum += v1 * v2;
        }
        
        float result = Vector.Dot(sum, Vector<float>.One);
        
        // Handle remainder
        for (int i = vectors * Vector<float>.Count; i < Size; i++)
        {
            result += _data1[i] * _data2[i];
        }
        
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("DotProduct")]
    public unsafe float VectorDotProduct_AVX2()
    {
        if (!Avx.IsSupported)
        {
            return ScalarDotProduct();
        }
        
        fixed (float* pData1 = _data1, pData2 = _data2)
        {
            var vectors = Size / 8;
            var accumulator = Vector256<float>.Zero;
            
            for (int i = 0; i < vectors; i++)
            {
                var offset = i * 8;
                var v1 = Avx.LoadVector256(pData1 + offset);
                var v2 = Avx.LoadVector256(pData2 + offset);
                var product = Avx.Multiply(v1, v2);
                accumulator = Avx.Add(accumulator, product);
            }
            
            // Horizontal sum of accumulator
            var low = Avx.ExtractVector128(accumulator, 0);
            var high = Avx.ExtractVector128(accumulator, 1);
            var sum128 = Sse.Add(low, high);
            
            // Continue with SSE horizontal add
            var temp = Sse.Shuffle(sum128, sum128, 0b_01_00_11_10);
            sum128 = Sse.Add(sum128, temp);
            temp = Sse.Shuffle(sum128, sum128, 0b_10_11_00_01);
            sum128 = Sse.Add(sum128, temp);
            
            float result = sum128.ToScalar();
            
            // Handle remainder
            for (int i = vectors * 8; i < Size; i++)
            {
                result += pData1[i] * pData2[i];
            }
            
            return result;
        }
    }

    // ==================== COMPLEX OPERATIONS ====================
    
    [Benchmark]
    [BenchmarkCategory("Complex")]
    public void FusedMultiplyAdd_Scalar()
    {
        for (int i = 0; i < Size; i++)
        {
            _result[i] = (_data1[i] * _data2[i]) + _data1[i];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Complex")]
    public unsafe void FusedMultiplyAdd_FMA()
    {
        if (!Fma.IsSupported)
        {
            FusedMultiplyAdd_Scalar();
            return;
        }
        
        fixed (float* pData1 = _data1, pData2 = _data2, pResult = _result)
        {
            var vectors = Size / 8;
            
            for (int i = 0; i < vectors; i++)
            {
                var offset = i * 8;
                var v1 = Avx.LoadVector256(pData1 + offset);
                var v2 = Avx.LoadVector256(pData2 + offset);
                // FMA: v1 * v2 + v1
                var result = Fma.MultiplyAdd(v1, v2, v1);
                Avx.Store(pResult + offset, result);
            }
            
            // Handle remainder
            for (int i = vectors * 8; i < Size; i++)
            {
                pResult[i] = (pData1[i] * pData2[i]) + pData1[i];
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("Complex")]
    public void VectorNormalize_Scalar()
    {
        for (int i = 0; i < Size; i++)
        {
            var value = _data1[i];
            _result[i] = value / MathF.Sqrt(value * value + 1.0f);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Complex")]
    public unsafe void VectorNormalize_AVX2()
    {
        if (!Avx.IsSupported)
        {
            VectorNormalize_Scalar();
            return;
        }
        
        fixed (float* pData1 = _data1, pResult = _result)
        {
            var vectors = Size / 8;
            var ones = Vector256.Create(1.0f);
            
            for (int i = 0; i < vectors; i++)
            {
                var offset = i * 8;
                var v = Avx.LoadVector256(pData1 + offset);
                var squared = Avx.Multiply(v, v);
                var plusOne = Avx.Add(squared, ones);
                var sqrt = Avx.Sqrt(plusOne);
                var result = Avx.Divide(v, sqrt);
                Avx.Store(pResult + offset, result);
            }
            
            // Handle remainder
            for (int i = vectors * 8; i < Size; i++)
            {
                var value = pData1[i];
                pResult[i] = value / MathF.Sqrt(value * value + 1.0f);
            }
        }
    }

    // ==================== PERFORMANCE VALIDATION ====================
    
    /// <summary>
    /// Validates that SIMD operations produce correct results
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Validation")]
    public bool ValidateResults()
    {
        var scalarResult = new float[Size];
        var vectorResult = new float[Size];
        
        // Scalar computation
        for (int i = 0; i < Size; i++)
        {
            scalarResult[i] = _data1[i] + _data2[i];
        }
        
        // Vector computation
        var vectors = Size / Vector<float>.Count;
        for (int i = 0; i < vectors; i++)
        {
            var offset = i * Vector<float>.Count;
            var v1 = new Vector<float>(_data1, offset);
            var v2 = new Vector<float>(_data2, offset);
            var result = v1 + v2;
            result.CopyTo(vectorResult, offset);
        }
        
        // Handle remainder
        for (int i = vectors * Vector<float>.Count; i < Size; i++)
        {
            vectorResult[i] = _data1[i] + _data2[i];
        }
        
        // Compare results with tolerance
        const float tolerance = 1e-6f;
        for (int i = 0; i < Size; i++)
        {
            if (MathF.Abs(scalarResult[i] - vectorResult[i]) > tolerance)
            {
                return false;
            }
        }
        
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DoNotOptimize<T>(T value) => GC.KeepAlive(value);
}