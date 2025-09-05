using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Numerics;

namespace DotCompute.Benchmarks;

/// <summary>
/// Benchmarks for CPU backend operations including SIMD optimizations.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class CpuBenchmarks
{
    private const int SmallSize = 1024;
    private const int MediumSize = 1024 * 1024;
    private const int LargeSize = 16 * 1024 * 1024;

    private float[] _dataA = null!;
    private float[] _dataB = null!;
    private float[] _result = null!;

    [Params(SmallSize, MediumSize, LargeSize)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _dataA = new float[DataSize];
        _dataB = new float[DataSize];
        _result = new float[DataSize];
        
        var random = new Random(42);
        for (var i = 0; i < DataSize; i++)
        {
            _dataA[i] = (float)random.NextDouble();
            _dataB[i] = (float)random.NextDouble();
        }
    }

    [Benchmark(Baseline = true)]
    public void ScalarVectorAddition()
    {
        for (var i = 0; i < DataSize; i++)
        {
            _result[i] = _dataA[i] + _dataB[i];
        }
    }

    [Benchmark]
    public void VectorizedAddition()
    {
        var vectorSize = Vector<float>.Count;
        var i = 0;
        
        // Process vectors
        for (; i <= DataSize - vectorSize; i += vectorSize)
        {
            var vecA = new Vector<float>(_dataA, i);
            var vecB = new Vector<float>(_dataB, i);
            var result = vecA + vecB;
            result.CopyTo(_result, i);
        }
        
        // Process remaining elements
        for (; i < DataSize; i++)
        {
            _result[i] = _dataA[i] + _dataB[i];
        }
    }

    [Benchmark]
    public void ScalarDotProduct()
    {
        float sum = 0;
        for (var i = 0; i < DataSize; i++)
        {
            sum += _dataA[i] * _dataB[i];
        }
        
        // Prevent optimization
        if (float.IsNaN(sum))
        {
            throw new InvalidOperationException();
        }
    }

    [Benchmark]
    public void VectorizedDotProduct()
    {
        var vectorSize = Vector<float>.Count;
        var sumVector = Vector<float>.Zero;
        var i = 0;
        
        // Process vectors
        for (; i <= DataSize - vectorSize; i += vectorSize)
        {
            var vecA = new Vector<float>(_dataA, i);
            var vecB = new Vector<float>(_dataB, i);
            sumVector += vecA * vecB;
        }
        
        // Sum the vector components
        var sum = Vector.Dot(sumVector, Vector<float>.One);
        
        // Process remaining elements
        for (; i < DataSize; i++)
        {
            sum += _dataA[i] * _dataB[i];
        }
        
        // Prevent optimization
        if (float.IsNaN(sum))
        {
            throw new InvalidOperationException();
        }
    }

    [Benchmark]
    public void ScalarSaxpy() // y = a*x + y
    {
        const float alpha = 2.5f;
        for (var i = 0; i < DataSize; i++)
        {
            _result[i] = alpha * _dataA[i] + _dataB[i];
        }
    }

    [Benchmark]
    public void VectorizedSaxpy()
    {
        const float alpha = 2.5f;
        var alphaVector = new Vector<float>(alpha);
        var vectorSize = Vector<float>.Count;
        var i = 0;
        
        // Process vectors
        for (; i <= DataSize - vectorSize; i += vectorSize)
        {
            var vecA = new Vector<float>(_dataA, i);
            var vecB = new Vector<float>(_dataB, i);
            var result = alphaVector * vecA + vecB;
            result.CopyTo(_result, i);
        }
        
        // Process remaining elements
        for (; i < DataSize; i++)
        {
            _result[i] = alpha * _dataA[i] + _dataB[i];
        }
    }

    [Benchmark]
    public void ParallelScalarAddition()
    {
        _ = Parallel.For(0, DataSize, i =>
        {
            _result[i] = _dataA[i] + _dataB[i];
        });
    }

    [Benchmark]
    public void ParallelVectorizedAddition()
    {
        var vectorSize = Vector<float>.Count;
        var numVectors = DataSize / vectorSize;

        _ = Parallel.For(0, numVectors, vectorIndex =>
        {
            var i = vectorIndex * vectorSize;
            var vecA = new Vector<float>(_dataA, i);
            var vecB = new Vector<float>(_dataB, i);
            var result = vecA + vecB;
            result.CopyTo(_result, i);
        });
        
        // Process remaining elements
        var remaining = DataSize % vectorSize;
        if (remaining > 0)
        {
            var startIndex = DataSize - remaining;
            for (var i = startIndex; i < DataSize; i++)
            {
                _result[i] = _dataA[i] + _dataB[i];
            }
        }
    }

    [Benchmark]
    public void LinqSum()
    {
        var sum = _dataA.Sum();
        
        // Prevent optimization
        if (float.IsNaN(sum))
        {
            throw new InvalidOperationException();
        }
    }

    [Benchmark]
    public void ParallelLinqSum()
    {
        var sum = _dataA.AsParallel().Sum();
        
        // Prevent optimization
        if (float.IsNaN(sum))
        {
            throw new InvalidOperationException();
        }
    }

    [Benchmark]
    public void SpanBasedOperations()
    {
        var spanA = _dataA.AsSpan();
        var spanB = _dataB.AsSpan();
        var spanResult = _result.AsSpan();
        
        for (var i = 0; i < spanA.Length; i++)
        {
            spanResult[i] = spanA[i] + spanB[i];
        }
    }

    [Benchmark]
    public void UnsafeMemoryOperations()
    {
        unsafe
        {
            fixed (float* pA = _dataA)
            fixed (float* pB = _dataB)
            fixed (float* pResult = _result)
            {
                for (var i = 0; i < DataSize; i++)
                {
                    pResult[i] = pA[i] + pB[i];
                }
            }
        }
    }
}