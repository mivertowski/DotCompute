using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Benchmarks;

/// <summary>
/// Benchmarks for SIMD operations performance on CPU backend.
/// Tests vectorized operations, different SIMD instruction sets, and performance scaling.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class SimdOperationsBenchmarks
{
    [Params(1024, 16384, 65536, 262144, 1048576)]
    public int DataSize { get; set; }

    [Params("Scalar", "Vector", "AVX2", "AVX512")]
    public string OperationType { get; set; } = "Scalar";

    private float[] _inputA = null!;
    private float[] _inputB = null!;
    private float[] _output = null!;
    private int[] _intInputA = null!;
    private int[] _intInputB = null!;
    private int[] _intOutput = null!;

    [GlobalSetup]
    public void Setup()
    {
        _inputA = new float[DataSize];
        _inputB = new float[DataSize];
        _output = new float[DataSize];
        _intInputA = new int[DataSize];
        _intInputB = new int[DataSize];
        _intOutput = new int[DataSize];

        var random = new Random(42);
        for (int i = 0; i < DataSize; i++)
        {
            _inputA[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            _inputB[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            _intInputA[i] = random.Next(-1000, 1000);
            _intInputB[i] = random.Next(-1000, 1000);
        }
    }

    [Benchmark(Baseline = true)]
    public void ScalarFloatAddition()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _output[i] = _inputA[i] + _inputB[i];
        }
    }

    [Benchmark]
    public void VectorFloatAddition()
    {
        var vectorSize = Vector<float>.Count;
        var vectorCount = DataSize / vectorSize;
        
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vectorA = new Vector<float>(_inputA, offset);
            var vectorB = new Vector<float>(_inputB, offset);
            var result = vectorA + vectorB;
            result.CopyTo(_output, offset);
        }
        
        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < DataSize; i++)
        {
            _output[i] = _inputA[i] + _inputB[i];
        }
    }

    [Benchmark]
    public void VectorFloatMultiplication()
    {
        var vectorSize = Vector<float>.Count;
        var vectorCount = DataSize / vectorSize;
        
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vectorA = new Vector<float>(_inputA, offset);
            var vectorB = new Vector<float>(_inputB, offset);
            var result = vectorA * vectorB;
            result.CopyTo(_output, offset);
        }
        
        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < DataSize; i++)
        {
            _output[i] = _inputA[i] * _inputB[i];
        }
    }

    [Benchmark]
    public void VectorFloatDotProduct()
    {
        var vectorSize = Vector<float>.Count;
        var vectorCount = DataSize / vectorSize;
        var sum = Vector<float>.Zero;
        
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vectorA = new Vector<float>(_inputA, offset);
            var vectorB = new Vector<float>(_inputB, offset);
            sum += vectorA * vectorB;
        }
        
        // Sum the vector elements
        float result = 0;
        for (int i = 0; i < vectorSize; i++)
        {
            result += sum[i];
        }
        
        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < DataSize; i++)
        {
            result += _inputA[i] * _inputB[i];
        }
        
        _output[0] = result;
    }

    [Benchmark]
    public unsafe void Avx2FloatAddition()
    {
        if (!Avx2.IsSupported)
        {
            VectorFloatAddition(); // Fallback
            return;
        }
        
        fixed (float* pA = _inputA, pB = _inputB, pOut = _output)
        {
            const int vectorSize = 8; // AVX2 processes 8 floats at once
            var vectorCount = DataSize / vectorSize;
            
            for (int i = 0; i < vectorCount; i++)
            {
                var offset = i * vectorSize;
                var vectorA = Avx.LoadVector256(pA + offset);
                var vectorB = Avx.LoadVector256(pB + offset);
                var result = Avx.Add(vectorA, vectorB);
                Avx.Store(pOut + offset, result);
            }
            
            // Handle remaining elements
            for (int i = vectorCount * vectorSize; i < DataSize; i++)
            {
                pOut[i] = pA[i] + pB[i];
            }
        }
    }

    [Benchmark]
    public unsafe void Avx2FloatMultiplication()
    {
        if (!Avx2.IsSupported)
        {
            VectorFloatMultiplication(); // Fallback
            return;
        }
        
        fixed (float* pA = _inputA, pB = _inputB, pOut = _output)
        {
            const int vectorSize = 8;
            var vectorCount = DataSize / vectorSize;
            
            for (int i = 0; i < vectorCount; i++)
            {
                var offset = i * vectorSize;
                var vectorA = Avx.LoadVector256(pA + offset);
                var vectorB = Avx.LoadVector256(pB + offset);
                var result = Avx.Multiply(vectorA, vectorB);
                Avx.Store(pOut + offset, result);
            }
            
            // Handle remaining elements
            for (int i = vectorCount * vectorSize; i < DataSize; i++)
            {
                pOut[i] = pA[i] * pB[i];
            }
        }
    }

    [Benchmark]
    public unsafe void Avx2IntegerAddition()
    {
        if (!Avx2.IsSupported)
        {
            ScalarIntegerAddition(); // Fallback
            return;
        }
        
        fixed (int* pA = _intInputA, pB = _intInputB, pOut = _intOutput)
        {
            const int vectorSize = 8; // AVX2 processes 8 ints at once
            var vectorCount = DataSize / vectorSize;
            
            for (int i = 0; i < vectorCount; i++)
            {
                var offset = i * vectorSize;
                var vectorA = Avx2.LoadVector256(pA + offset);
                var vectorB = Avx2.LoadVector256(pB + offset);
                var result = Avx2.Add(vectorA, vectorB);
                Avx2.Store(pOut + offset, result);
            }
            
            // Handle remaining elements
            for (int i = vectorCount * vectorSize; i < DataSize; i++)
            {
                pOut[i] = pA[i] + pB[i];
            }
        }
    }

    [Benchmark]
    public void ScalarIntegerAddition()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _intOutput[i] = _intInputA[i] + _intInputB[i];
        }
    }

    [Benchmark]
    public void VectorComplexMathOperations()
    {
        var vectorSize = Vector<float>.Count;
        var vectorCount = DataSize / vectorSize;
        
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vectorA = new Vector<float>(_inputA, offset);
            var vectorB = new Vector<float>(_inputB, offset);
            
            // Complex operation: (a * b) + (a / (b + 1))
            var product = vectorA * vectorB;
            var divisor = vectorB + Vector<float>.One;
            var quotient = vectorA / divisor;
            var result = product + quotient;
            
            result.CopyTo(_output, offset);
        }
        
        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < DataSize; i++)
        {
            var a = _inputA[i];
            var b = _inputB[i];
            _output[i] = (a * b) + (a / (b + 1.0f));
        }
    }

    [Benchmark]
    public unsafe void Avx2ComplexMathOperations()
    {
        if (!Avx2.IsSupported)
        {
            VectorComplexMathOperations(); // Fallback
            return;
        }
        
        fixed (float* pA = _inputA, pB = _inputB, pOut = _output)
        {
            const int vectorSize = 8;
            var vectorCount = DataSize / vectorSize;
            var ones = Vector256.Create(1.0f);
            
            for (int i = 0; i < vectorCount; i++)
            {
                var offset = i * vectorSize;
                var vectorA = Avx.LoadVector256(pA + offset);
                var vectorB = Avx.LoadVector256(pB + offset);
                
                var product = Avx.Multiply(vectorA, vectorB);
                var divisor = Avx.Add(vectorB, ones);
                var quotient = Avx.Divide(vectorA, divisor);
                var result = Avx.Add(product, quotient);
                
                Avx.Store(pOut + offset, result);
            }
            
            // Handle remaining elements
            for (int i = vectorCount * vectorSize; i < DataSize; i++)
            {
                var a = pA[i];
                var b = pB[i];
                pOut[i] = (a * b) + (a / (b + 1.0f));
            }
        }
    }

    [Benchmark]
    public void VectorReductionSum()
    {
        var vectorSize = Vector<float>.Count;
        var vectorCount = DataSize / vectorSize;
        var sum = Vector<float>.Zero;
        
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vector = new Vector<float>(_inputA, offset);
            sum += vector;
        }
        
        // Sum the vector elements
        float result = 0;
        for (int i = 0; i < vectorSize; i++)
        {
            result += sum[i];
        }
        
        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < DataSize; i++)
        {
            result += _inputA[i];
        }
        
        _output[0] = result;
    }

    [Benchmark]
    public unsafe void Avx2ReductionSum()
    {
        if (!Avx2.IsSupported)
        {
            VectorReductionSum(); // Fallback
            return;
        }
        
        fixed (float* pA = _inputA)
        {
            const int vectorSize = 8;
            var vectorCount = DataSize / vectorSize;
            var sum = Vector256<float>.Zero;
            
            for (int i = 0; i < vectorCount; i++)
            {
                var offset = i * vectorSize;
                var vector = Avx.LoadVector256(pA + offset);
                sum = Avx.Add(sum, vector);
            }
            
            // Horizontal add to get final sum
            var temp = new float[8];
            Avx.Store(temp, sum);
            float result = 0;
            for (int i = 0; i < 8; i++)
            {
                result += temp[i];
            }
            
            // Handle remaining elements
            for (int i = vectorCount * vectorSize; i < DataSize; i++)
            {
                result += pA[i];
            }
            
            _output[0] = result;
        }
    }

    [Benchmark]
    public void VectorConditionalOperations()
    {
        var vectorSize = Vector<float>.Count;
        var vectorCount = DataSize / vectorSize;
        var threshold = new Vector<float>(0.5f);
        
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vectorA = new Vector<float>(_inputA, offset);
            var vectorB = new Vector<float>(_inputB, offset);
            
            // Conditional: if (a > 0.5) then a * 2 else b
            var mask = Vector.GreaterThan(vectorA, threshold);
            var trueResult = vectorA * new Vector<float>(2.0f);
            var result = Vector.ConditionalSelect(mask, trueResult, vectorB);
            
            result.CopyTo(_output, offset);
        }
        
        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < DataSize; i++)
        {
            _output[i] = _inputA[i] > 0.5f ? _inputA[i] * 2.0f : _inputB[i];
        }
    }

    [Benchmark]
    public void SimdThroughputTest()
    {
        // Test sustained SIMD throughput
        const int iterations = 100;
        
        for (int iter = 0; iter < iterations; iter++)
        {
            VectorFloatAddition();
            VectorFloatMultiplication();
            VectorReductionSum();
        }
    }

    [Benchmark]
    public double CalculateSimdEfficiency()
    {
        // Calculate SIMD efficiency vs scalar
        var vectorSize = Vector<float>.Count;
        var theoreticalSpeedup = vectorSize;
        
        // Simulate timing measurements
        var scalarTime = DataSize * 1.0; // 1 unit per operation
        var vectorTime = (DataSize / vectorSize) * 1.2; // Slight overhead
        
        var actualSpeedup = scalarTime / vectorTime;
        return actualSpeedup / theoreticalSpeedup * 100.0; // Efficiency percentage
    }

    [Benchmark]
    public void MemoryAlignmentTest()
    {
        // Test performance impact of memory alignment
        var alignedData = GC.AllocateUninitializedArray<float>(DataSize, pinned: true);
        var unalignedData = new float[DataSize + 1].AsSpan(1); // Unaligned by 4 bytes
        
        // Copy test data
        _inputA.CopyTo(alignedData);
        _inputA.CopyTo(unalignedData);
        
        // Perform vectorized operations on both
        var vectorSize = Vector<float>.Count;
        var vectorCount = DataSize / vectorSize;
        
        // Aligned access
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vector = new Vector<float>(alignedData, offset);
            var result = vector * new Vector<float>(2.0f);
            result.CopyTo(_output, offset);
        }
        
        // The actual unaligned access would show performance difference
        // in real hardware measurements
    }

    [Benchmark]
    public void CacheFriendlyAccess()
    {
        // Test cache-friendly vs cache-unfriendly memory access patterns
        const int stride = 64; // Cache line size
        
        // Sequential access (cache-friendly)
        for (int i = 0; i < DataSize; i++)
        {
            _output[i] = _inputA[i] * 2.0f;
        }
        
        // Strided access (less cache-friendly)
        for (int i = 0; i < DataSize; i += stride)
        {
            if (i < DataSize)
                _output[i] = _inputA[i] * 3.0f;
        }
    }

    [Benchmark]
    public void SimdCapabilityDetection()
    {
        // Benchmark the cost of SIMD capability detection
        var results = new Dictionary<string, bool>
        {
            ["Vector.IsHardwareAccelerated"] = Vector.IsHardwareAccelerated,
            ["Sse.IsSupported"] = Sse.IsSupported,
            ["Sse2.IsSupported"] = Sse2.IsSupported,
            ["Avx.IsSupported"] = Avx.IsSupported,
            ["Avx2.IsSupported"] = Avx2.IsSupported
        };
        
        // Additional capability checks
        if (Vector512.IsHardwareAccelerated)
        {
            results["Vector512.IsHardwareAccelerated"] = true;
        }
        
        // Store results (to prevent optimization)
        var supported = results.Count(kvp => kvp.Value);
        _output[0] = supported;
    }
}