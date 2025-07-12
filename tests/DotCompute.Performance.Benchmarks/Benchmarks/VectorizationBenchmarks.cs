// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Performance.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 10)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[RankColumn]
public class VectorizationBenchmarks
{
    private float[] _data1 = Array.Empty<float>();
    private float[] _data2 = Array.Empty<float>();
    private float[] _result = Array.Empty<float>();
    
    [Params(1024, 4096, 16384, 65536)]
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
            _data1[i] = (float)random.NextDouble();
            _data2[i] = (float)random.NextDouble();
        }
    }

    [Benchmark(Baseline = true)]
    public void ScalarAddition()
    {
        for (int i = 0; i < Size; i++)
        {
            _result[i] = _data1[i] + _data2[i];
        }
    }

    [Benchmark]
    public void VectorAddition()
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
    public unsafe void AvxAddition()
    {
        if (!Avx.IsSupported) return;
        
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
    public void ScalarMultiplication()
    {
        for (int i = 0; i < Size; i++)
        {
            _result[i] = _data1[i] * _data2[i];
        }
    }

    [Benchmark]
    public void VectorMultiplication()
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
    public void ScalarDotProduct()
    {
        float sum = 0;
        for (int i = 0; i < Size; i++)
        {
            sum += _data1[i] * _data2[i];
        }
        DoNotOptimize(sum);
    }

    [Benchmark]
    public void VectorDotProduct()
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
        
        DoNotOptimize(result);
    }

    [Benchmark]
    public void ScalarMatrixMultiplication()
    {
        // Simple 2x2 matrix multiplication for demonstration
        var matrixSize = (int)Math.Sqrt(Size / 4) * 2;
        if (matrixSize * matrixSize > Size) return;
        
        for (int i = 0; i < matrixSize; i += 2)
        {
            for (int j = 0; j < matrixSize; j += 2)
            {
                _result[i * matrixSize + j] = 
                    _data1[i * matrixSize + j] * _data2[j * matrixSize + i] +
                    _data1[i * matrixSize + j + 1] * _data2[(j + 1) * matrixSize + i];
            }
        }
    }

    [Benchmark]
    public void ParallelVectorAddition()
    {
        Parallel.For(0, Size / Vector<float>.Count, i =>
        {
            var offset = i * Vector<float>.Count;
            var v1 = new Vector<float>(_data1, offset);
            var v2 = new Vector<float>(_data2, offset);
            var result = v1 + v2;
            result.CopyTo(_result, offset);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DoNotOptimize<T>(T value) => GC.KeepAlive(value);
}