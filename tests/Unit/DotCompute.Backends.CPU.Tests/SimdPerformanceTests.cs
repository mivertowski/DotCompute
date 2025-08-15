// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CA5394 // Do not use insecure randomness - Random is used for performance testing, not security
#pragma warning disable CA1822 // Mark members as static - Test methods cannot be static in xUnit

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using Xunit.Abstractions;

#pragma warning disable CA1515 // Make types internal
namespace DotCompute.Backends.CPU;

/// <summary>
/// Performance tests demonstrating SIMD speedup for CPU backend.
/// </summary>
public sealed class SimdPerformanceTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void VectorAdditionShowsSimdSpeedup()
    {
        const int elementCount = 1_000_000;
        var a = new float[elementCount];
        var b = new float[elementCount];
        var result = new float[elementCount];

        // Initialize with random data
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }

        // Warm-up
        VectorAddScalar(a, b, result);
        VectorAddSimd(a, b, result);

        // Measure scalar performance
        var scalarTime = MeasureTime(() => VectorAddScalar(a, b, result), 100);
        _output.WriteLine($"Scalar time: {scalarTime:F2}ms");

        // Measure SIMD performance
        var simdTime = MeasureTime(() => VectorAddSimd(a, b, result), 100);
        _output.WriteLine($"SIMD time: {simdTime:F2}ms");

        // Calculate speedup
        var speedup = scalarTime / simdTime;
        _output.WriteLine($"SIMD speedup: {speedup:F2}x");

        // Assert significant speedup
        PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 2.0, "vector addition SIMD");
    }

    [Fact]
    public void MatrixMultiplyShowsSimdSpeedup()
    {
        const int size = 256;
        var a = new float[size * size];
        var b = new float[size * size];
        var result = new float[size * size];

        // Initialize matrices
        var random = new Random(42);
        for (var i = 0; i < a.Length; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }

        // Warm-up
        MatrixMultiplyScalar(a, b, result, size);
        MatrixMultiplySimd(a, b, result, size);

        // Measure performance
        var scalarTime = MeasureTime(() => MatrixMultiplyScalar(a, b, result, size), 5);
        _output.WriteLine($"Matrix multiply scalar time: {scalarTime:F2}ms");

        var simdTime = MeasureTime(() => MatrixMultiplySimd(a, b, result, size), 5);
        _output.WriteLine($"Matrix multiply SIMD time: {simdTime:F2}ms");

        var speedup = scalarTime / simdTime;
        _output.WriteLine($"Matrix multiply SIMD speedup: {speedup:F2}x");

        PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 3.0, "matrix multiply SIMD");
    }

    [Fact]
    public void DotProductShowsSimdSpeedup()
    {
        const int elementCount = 1_000_000;
        var a = new float[elementCount];
        var b = new float[elementCount];

        // Initialize vectors
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }

        // Warm-up
        DotProductScalar(a, b);
        DotProductSimd(a, b);

        // Measure performance
        float scalarResult = 0;
        var scalarTime = MeasureTime(() => scalarResult = DotProductScalar(a, b), 100);
        _output.WriteLine($"Dot product scalar time: {scalarTime:F2}ms");

        float simdResult = 0;
        var simdTime = MeasureTime(() => simdResult = DotProductSimd(a, b), 100);
        _output.WriteLine($"Dot product SIMD time: {simdTime:F2}ms");

        var speedup = scalarTime / simdTime;
        _output.WriteLine($"Dot product SIMD speedup: {speedup:F2}x");

        // Verify results are close(allowing for floating-point error)
        // Use relative tolerance due to large numbers involved in dot product
        var tolerance = Math.Max(Math.Abs(scalarResult), Math.Abs(simdResult)) * 0.001f; // 0.1% relative tolerance
        Assert.True(Math.Abs(scalarResult - simdResult) < tolerance,
            $"Results differ: scalar={scalarResult}, simd={simdResult}, tolerance={tolerance}");

        PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 2.0, "dot product SIMD");
    }

    private static double MeasureTime(Action action, int iterations)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            action();
        }
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / iterations;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void VectorAddScalar(float[] a, float[] b, float[] result)
    {
        for (var i = 0; i < a.Length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe void VectorAddSimd(float[] a, float[] b, float[] result)
    {
        var vectorSize = Vector<float>.Count;
        var i = 0;

        fixed (float* pA = a)
        fixed (float* pB = b)
        fixed (float* pResult = result)
        {
            // Process using the best available instruction set
            if (Avx2.IsSupported)
            {
                // Process 8 floats at a time with AVX2
                for (; i + 8 <= a.Length; i += 8)
                {
                    var va = Avx.LoadVector256(pA + i);
                    var vb = Avx.LoadVector256(pB + i);
                    var vr = Avx.Add(va, vb);
                    Avx.Store(pResult + i, vr);
                }
            }
            else if (Sse2.IsSupported)
            {
                // Process 4 floats at a time with SSE
                for (; i + 4 <= a.Length; i += 4)
                {
                    var va = Sse.LoadVector128(pA + i);
                    var vb = Sse.LoadVector128(pB + i);
                    var vr = Sse.Add(va, vb);
                    Sse.Store(pResult + i, vr);
                }
            }
            else
            {
                // Use portable Vector<T> API
                for (; i + vectorSize <= a.Length; i += vectorSize)
                {
                    var va = new Vector<float>(a, i);
                    var vb = new Vector<float>(b, i);
                    var vr = va + vb;
                    vr.CopyTo(result, i);
                }
            }
        }

        // Process remaining elements
        for (; i < a.Length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MatrixMultiplyScalar(float[] a, float[] b, float[] result, int size)
    {
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                float sum = 0;
                for (var k = 0; k < size; k++)
                {
                    sum += a[i * size + k] * b[k * size + j];
                }
                result[i * size + j] = sum;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe void MatrixMultiplySimd(float[] a, float[] b, float[] result, int size)
    {
        // Tiled matrix multiplication with SIMD
        const int tileSize = 64;

        fixed (float* pA = a)
        fixed (float* pB = b)
        fixed (float* pResult = result)
        {
            // Clear result matrix
            Array.Clear(result, 0, result.Length);

            for (var i0 = 0; i0 < size; i0 += tileSize)
            {
                for (var j0 = 0; j0 < size; j0 += tileSize)
                {
                    for (var k0 = 0; k0 < size; k0 += tileSize)
                    {
                        // Process tile
                        var iMax = Math.Min(i0 + tileSize, size);
                        var jMax = Math.Min(j0 + tileSize, size);
                        var kMax = Math.Min(k0 + tileSize, size);

                        for (var i = i0; i < iMax; i++)
                        {
                            for (var k = k0; k < kMax; k++)
                            {
                                var aik = a[i * size + k];
                                var j = j0;

                                if (Avx2.IsSupported)
                                {
                                    var vaik = Vector256.Create(aik);
                                    for (; j + 8 <= jMax; j += 8)
                                    {
                                        var vb = Avx.LoadVector256(pB + k * size + j);
                                        var vr = Avx.LoadVector256(pResult + i * size + j);
                                        vr = Avx.Add(vr, Avx.Multiply(vaik, vb));
                                        Avx.Store(pResult + i * size + j, vr);
                                    }
                                }

                                // Scalar remainder
                                for (; j < jMax; j++)
                                {
                                    result[i * size + j] += aik * b[k * size + j];
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float DotProductScalar(float[] a, float[] b)
    {
        float sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe float DotProductSimd(float[] a, float[] b)
    {
        var i = 0;

        if (Avx2.IsSupported)
        {
            // Use 8-wide AVX2 vectors
            var sum256 = Vector256<float>.Zero;

            fixed (float* pA = a)
            fixed (float* pB = b)
            {
                for (; i + 8 <= a.Length; i += 8)
                {
                    var va = Avx.LoadVector256(pA + i);
                    var vb = Avx.LoadVector256(pB + i);
                    sum256 = Avx.Add(sum256, Avx.Multiply(va, vb));
                }
            }

            // Horizontal sum of the vector
            var sum128 = Sse.Add(Avx.ExtractVector128(sum256, 1), sum256.GetLower());
            sum128 = Sse.Add(sum128, Sse.Shuffle(sum128, sum128, 0x4E));
            sum128 = Sse.Add(sum128, Sse.Shuffle(sum128, sum128, 0xB1));

            var sum = sum128.ToScalar();

            // Process remaining elements
            for (; i < a.Length; i++)
            {
                sum += a[i] * b[i];
            }

            return sum;
        }
        else
        {
            // Fallback to portable SIMD
            var vectorSize = Vector<float>.Count;
            var sumVector = Vector<float>.Zero;

            for (; i + vectorSize <= a.Length; i += vectorSize)
            {
                var va = new Vector<float>(a, i);
                var vb = new Vector<float>(b, i);
                sumVector += va * vb;
            }

            var sum = Vector.Dot(sumVector, Vector<float>.One);

            // Process remaining elements
            for (; i < a.Length; i++)
            {
                sum += a[i] * b[i];
            }

            return sum;
        }
    }
}

/// <summary>
/// Benchmark class for detailed performance analysis.
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - BenchmarkDotNet benchmark class
[MemoryDiagnoser]
[DisassemblyDiagnoser]
internal sealed class SimdBenchmarks
#pragma warning restore CA1812
{
    private float[] _a = default!;
    private float[] _b = default!;
    private float[] _result = default!;

    [Params(1000, 10000, 100000, 1000000)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _a = new float[Size];
        _b = new float[Size];
        _result = new float[Size];

        var random = new Random(42);
        for (var i = 0; i < Size; i++)
        {
            _a[i] = (float)random.NextDouble();
            _b[i] = (float)random.NextDouble();
        }
    }

    [Benchmark(Baseline = true)]
    public void VectorAddScalar()
    {
        for (var i = 0; i < _a.Length; i++)
        {
            _result[i] = _a[i] + _b[i];
        }
    }

    [Benchmark]
    public void VectorAddVector()
    {
        var vectorSize = Vector<float>.Count;
        var i = 0;

        for (; i + vectorSize <= _a.Length; i += vectorSize)
        {
            var va = new Vector<float>(_a, i);
            var vb = new Vector<float>(_b, i);
            (va + vb).CopyTo(_result, i);
        }

        for (; i < _a.Length; i++)
        {
            _result[i] = _a[i] + _b[i];
        }
    }

    [Benchmark]
    public unsafe void VectorAddAvx2()
    {
        if (!Avx2.IsSupported)
        {
            VectorAddScalar();
            return;
        }

        var i = 0;
        fixed (float* pA = _a)
        fixed (float* pB = _b)
        fixed (float* pResult = _result)
        {
            for (; i + 8 <= _a.Length; i += 8)
            {
                var va = Avx.LoadVector256(pA + i);
                var vb = Avx.LoadVector256(pB + i);
                Avx.Store(pResult + i, Avx.Add(va, vb));
            }
        }

        for (; i < _a.Length; i++)
        {
            _result[i] = _a[i] + _b[i];
        }
    }
}
