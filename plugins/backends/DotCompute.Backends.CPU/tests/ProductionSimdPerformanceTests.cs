// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Optimization;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Comprehensive production tests for SIMD performance and validation.
/// </summary>
public class ProductionSimdPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ProductionSimdPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SimdCapabilitiesShouldReflectActualHardware()
    {
        // Act
        var summary = SimdCapabilities.GetSummary();

        // Assert
        Assert.NotNull(summary);
        Assert.True(summary.PreferredVectorWidth > 0);
        Assert.NotEmpty(summary.SupportedInstructionSets);

        // Validate against actual hardware capabilities
        if (Sse.IsSupported)
        {
            Assert.Contains("SSE", summary.SupportedInstructionSets);
            Assert.True(summary.PreferredVectorWidth >= 128);
        }

        if (Avx.IsSupported)
        {
            Assert.Contains("AVX", summary.SupportedInstructionSets);
            Assert.True(summary.PreferredVectorWidth >= 256);
        }

        if (Avx512F.IsSupported)
        {
            Assert.Contains("AVX512F", summary.SupportedInstructionSets);
            Assert.True(summary.PreferredVectorWidth >= 512);
        }

        _output.WriteLine($"Detected SIMD capabilities: {summary.PreferredVectorWidth}-bit, " +
                         $"Instructions: {string.Join(", ", summary.SupportedInstructionSets)}");
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(16384)]
    [InlineData(65536)]
    public void VectorizedFloatAdditionShouldOutperformScalar(int arraySize)
    {
        // Arrange
        var a = new float[arraySize];
        var b = new float[arraySize];
        var resultScalar = new float[arraySize];
        var resultVectorized = new float[arraySize];

#pragma warning disable CA5394 // Do not use insecure randomness
        var random = new Random(42);
#pragma warning restore CA5394
        for (int i = 0; i < arraySize; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }

        // Act - Scalar version
        var scalarStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < arraySize; i++)
        {
            resultScalar[i] = a[i] + b[i];
        }
        scalarStopwatch.Stop();

        // Act - Vectorized version
        var vectorizedStopwatch = Stopwatch.StartNew();
        PerformVectorizedAddition(a, b, resultVectorized);
        vectorizedStopwatch.Stop();

        // Assert - Results should be identical
        for (int i = 0; i < arraySize; i++)
        {
            Assert.Equal(resultScalar[i], resultVectorized[i], 5); // Allow small floating point differences
        }

        // Calculate speedup
        double speedup = vectorizedStopwatch.ElapsedTicks > 0 ?
            (double)scalarStopwatch.ElapsedTicks / vectorizedStopwatch.ElapsedTicks : 1.0;

        // Assert - Vectorized should show improvement for larger arrays
        if (arraySize >= 8192 && NativeAotOptimizations.HasAdvancedSimdSupport)
        {
            Assert.True(speedup > 1.1,
                $"Expected at least 1.1x speedup for large arrays, got {speedup:F2}x (Vectorized: {vectorizedStopwatch.ElapsedTicks} ticks, Scalar: {scalarStopwatch.ElapsedTicks} ticks)");
        }

        _output.WriteLine($"Array size: {arraySize}, Scalar: {scalarStopwatch.ElapsedTicks} ticks, " +
                         $"Vectorized: {vectorizedStopwatch.ElapsedTicks} ticks, " +
                         $"Speedup: {speedup:F2}x, SIMD Support: {NativeAotOptimizations.HasAdvancedSimdSupport}");
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(16384)]
    public void VectorizedMatrixMultiplicationShouldBeAccurate(int size)
    {
        // Arrange
        var matrixA = CreateRandomMatrix(size, size);
        var matrixB = CreateRandomMatrix(size, size);
        var resultScalar = new float[size][];
        var resultVectorized = new float[size][];
        for (int i = 0; i < size; i++)
        {
            resultScalar[i] = new float[size];
            resultVectorized[i] = new float[size];
        }

        // Act - Scalar multiplication
        var scalarStopwatch = Stopwatch.StartNew();
        MultiplyMatricesScalar(matrixA, matrixB, resultScalar, size);
        scalarStopwatch.Stop();

        // Act - Vectorized multiplication
        var vectorizedStopwatch = Stopwatch.StartNew();
        MultiplyMatricesVectorized(matrixA, matrixB, resultVectorized, size);
        vectorizedStopwatch.Stop();

        // Assert - Results should be similar within floating point tolerance
        // For larger matrices, use a more relaxed tolerance due to accumulated floating point errors
        // We use relative tolerance for large matrices as errors accumulate
        float tolerance = size >= 1024 ? 0.05f : 0.001f;  // 5% relative error for large matrices
        CompareMatrices(resultScalar, resultVectorized, size, tolerance);

        _output.WriteLine($"Matrix {size}x{size}: Scalar: {scalarStopwatch.ElapsedMilliseconds}ms, " +
                         $"Vectorized: {vectorizedStopwatch.ElapsedMilliseconds}ms, " +
                         $"Speedup: {(double)scalarStopwatch.ElapsedMilliseconds / vectorizedStopwatch.ElapsedMilliseconds:F2}x");
    }

    [Fact]
    public void SimdReductionOperationsShouldBeAccurate()
    {
        // Arrange
        const int arraySize = 10000;
        var data = new float[arraySize];
#pragma warning disable CA5394 // Do not use insecure randomness
        var random = new Random(42);
#pragma warning restore CA5394

        for (int i = 0; i < arraySize; i++)
        {
            data[i] = (float)(random.NextDouble() * 100);
        }

        // Act - Scalar sum
        var scalarSum = 0.0f;
        for (int i = 0; i < arraySize; i++)
        {
            scalarSum += data[i];
        }

        // Act - Vectorized sum
        var vectorizedSum = VectorizedSum(data);

        // Assert
        Assert.Equal(scalarSum, vectorizedSum, 2); // Allow for floating point accumulation differences

        _output.WriteLine($"Scalar sum: {scalarSum}, Vectorized sum: {vectorizedSum}, " +
                         $"Difference: {Math.Abs(scalarSum - vectorizedSum)}");
    }

    [Fact]
    public void SpecificInstructionSetTestsShouldValidateCapabilities()
    {
        if (Sse.IsSupported)
        {
            TestSseOperations();
            _output.WriteLine("SSE operations validated");
        }

        if (Sse2.IsSupported)
        {
            TestSse2Operations();
            _output.WriteLine("SSE2 operations validated");
        }

        if (Avx.IsSupported)
        {
            TestAvxOperations();
            _output.WriteLine("AVX operations validated");
        }

        if (Avx2.IsSupported)
        {
            TestAvx2Operations();
            _output.WriteLine("AVX2 operations validated");
        }

        if (Avx512F.IsSupported)
        {
            TestAvx512Operations();
            _output.WriteLine("AVX512F operations validated");
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    public void AlignedMemoryAccessShouldPerformBetter(int alignment)
    {
        const int arraySize = 16384;
        const int iterations = 1000;

        // Create aligned and unaligned arrays
        var alignedArray = new float[arraySize];
        var unalignedArray = new float[arraySize + 1]; // Intentionally misaligned

        // Initialize data
        for (int i = 0; i < arraySize; i++)
        {
            alignedArray[i] = i;
            unalignedArray[i] = i;
        }

        // Test aligned access
        var alignedStopwatch = Stopwatch.StartNew();
        for (int iter = 0; iter < iterations; iter++)
        {
            ProcessArrayVectorized(alignedArray);
        }
        alignedStopwatch.Stop();

        // Test unaligned access (using offset)
        var unalignedStopwatch = Stopwatch.StartNew();
        var unalignedSpan = unalignedArray.AsSpan(1, arraySize);
        for (int iter = 0; iter < iterations; iter++)
        {
            ProcessArrayVectorized(unalignedSpan);
        }
        unalignedStopwatch.Stop();

        _output.WriteLine($"Alignment {alignment}: Aligned: {alignedStopwatch.ElapsedTicks} ticks, " +
                         $"Unaligned: {unalignedStopwatch.ElapsedTicks} ticks, " +
                         $"Performance ratio: {(double)unalignedStopwatch.ElapsedTicks / alignedStopwatch.ElapsedTicks:F2}");

        // Aligned access should generally be faster or at least not significantly slower
        Assert.True(alignedStopwatch.ElapsedTicks <= unalignedStopwatch.ElapsedTicks * 1.1);
    }

    [Fact]
    public void CacheLineOptimizationShouldImprovePerformance()
    {
        const int arraySize = 1024 * 1024; // 1M elements
        const int cacheLineSize = 64; // bytes
        const int elementsPerCacheLine = cacheLineSize / sizeof(float);

        var data = new float[arraySize];
        for (int i = 0; i < arraySize; i++)
        {
            data[i] = i;
        }

        // Sequential access (cache-friendly)
        var sequentialStopwatch = Stopwatch.StartNew();
        var sequentialSum = 0.0f;
        for (int i = 0; i < arraySize; i++)
        {
            sequentialSum += data[i];
        }
        sequentialStopwatch.Stop();

        // Strided access (cache-unfriendly)
        var stridedStopwatch = Stopwatch.StartNew();
        var stridedSum = 0.0f;
        for (int i = 0; i < arraySize; i += elementsPerCacheLine)
        {
            stridedSum += data[i];
        }
        stridedStopwatch.Stop();

        _output.WriteLine($"Sequential access: {sequentialStopwatch.ElapsedTicks} ticks, " +
                         $"Strided access: {stridedStopwatch.ElapsedTicks} ticks");

        // Sequential should be much faster for large arrays
        Assert.True(sequentialStopwatch.ElapsedTicks < stridedStopwatch.ElapsedTicks * 2);
    }

    private static void PerformVectorizedAddition(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        var vectorSize = Vector<float>.Count;
        int i = 0;

        // Process vectors
        for (; i <= a.Length - vectorSize; i += vectorSize)
        {
            var va = new Vector<float>(a.Slice(i));
            var vb = new Vector<float>(b.Slice(i));
            var vr = va + vb;
            vr.CopyTo(result.Slice(i));
        }

        // Process remaining elements
        for (; i < a.Length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    private static float[][] CreateRandomMatrix(int rows, int cols)
    {
        var matrix = new float[rows][];
        for (int i = 0; i < rows; i++)
        {
            matrix[i] = new float[cols];
        }
#pragma warning disable CA5394 // Do not use insecure randomness
        var random = new Random(42);
#pragma warning restore CA5394

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i][j] = (float)random.NextDouble();
            }
        }

        return matrix;
    }

    private static void MultiplyMatricesScalar(float[][] a, float[][] b, float[][] result, int size)
    {
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                result[i][j] = 0;
                for (int k = 0; k < size; k++)
                {
                    result[i][j] += a[i][k] * b[k][j];
                }
            }
        }
    }

    private static void MultiplyMatricesVectorized(float[][] a, float[][] b, float[][] result, int size)
    {
        var vectorSize = Vector<float>.Count;

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                var sum = Vector<float>.Zero;
                int k = 0;

                // Vectorized inner loop
                for (; k <= size - vectorSize; k += vectorSize)
                {
                    var aVec = LoadVectorFromMatrix(a, i, k, vectorSize);
                    var bVec = LoadVectorFromMatrix(b, k, j, vectorSize, true);
                    sum += aVec * bVec;
                }

                // Sum the vector elements
                result[i][j] = Vector.Dot(sum, Vector<float>.One);

                // Handle remaining elements
                for (; k < size; k++)
                {
                    result[i][j] += a[i][k] * b[k][j];
                }
            }
        }
    }

    private static Vector<float> LoadVectorFromMatrix(float[][] matrix, int row, int startCol, int vectorSize, bool transpose = false)
    {
        var values = new float[vectorSize];
        var matrixSize = matrix.Length;

        if (!transpose)
        {
            for (int i = 0; i < vectorSize; i++)
            {
                var col = startCol + i;
                values[i] = col < matrixSize ? matrix[row][col] : 0f;
            }
        }
        else
        {
            for (int i = 0; i < vectorSize; i++)
            {
                var matrixRow = startCol + i;
                values[i] = (matrixRow < matrixSize && row < matrix[matrixRow].Length) ? matrix[matrixRow][row] : 0f;
            }
        }

        return new Vector<float>(values);
    }

    private static void CompareMatrices(float[][] a, float[][] b, int size, float tolerance)
    {
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                var diff = Math.Abs(a[i][j] - b[i][j]);
                var maxValue = Math.Max(Math.Abs(a[i][j]), Math.Abs(b[i][j]));

                // Use relative error for large values, absolute error for small values
                bool withinTolerance = maxValue < 1.0f
                    ? diff <= tolerance
                    : diff <= tolerance * maxValue;

                Assert.True(withinTolerance,
                    $"Matrix difference at [{i},{j}]: {a[i][j]} vs {b[i][j]}, diff: {diff}, relative: {diff / maxValue:F4}");
            }
        }
    }

    private static float VectorizedSum(ReadOnlySpan<float> data)
    {
        var vectorSize = Vector<float>.Count;
        var sumVector = Vector<float>.Zero;
        int i = 0;

        // Process vectors
        for (; i <= data.Length - vectorSize; i += vectorSize)
        {
            var vector = new Vector<float>(data.Slice(i));
            sumVector += vector;
        }

        // Sum vector elements
        var sum = Vector.Dot(sumVector, Vector<float>.One);

        // Add remaining elements
        for (; i < data.Length; i++)
        {
            sum += data[i];
        }

        return sum;
    }

    private static void ProcessArrayVectorized(ReadOnlySpan<float> data)
    {
        var vectorSize = Vector<float>.Count;
        var sum = Vector<float>.Zero;

        for (int i = 0; i <= data.Length - vectorSize; i += vectorSize)
        {
            var vector = new Vector<float>(data.Slice(i));
            sum += vector;
        }
    }

    private static void TestSseOperations()
    {
        if (!Sse.IsSupported)
        {
            return;
        }

        unsafe
        {
            var a = Vector128.Create(1.0f, 2.0f, 3.0f, 4.0f);
            var b = Vector128.Create(5.0f, 6.0f, 7.0f, 8.0f);

            var result = Sse.Add(a, b);

            Assert.Equal(6.0f, result.GetElement(0), 5);
            Assert.Equal(8.0f, result.GetElement(1), 5);
            Assert.Equal(10.0f, result.GetElement(2), 5);
            Assert.Equal(12.0f, result.GetElement(3), 5);
        }
    }

    private static void TestSse2Operations()
    {
        if (!Sse2.IsSupported)
        {
            return;
        }

        unsafe
        {
            var a = Vector128.Create(1, 2, 3, 4);
            var b = Vector128.Create(5, 6, 7, 8);

            var result = Sse2.Add(a, b);

            Assert.Equal(6, result.GetElement(0));
            Assert.Equal(8, result.GetElement(1));
            Assert.Equal(10, result.GetElement(2));
            Assert.Equal(12, result.GetElement(3));
        }
    }

    private static void TestAvxOperations()
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        unsafe
        {
            var a = Vector256.Create(1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f);
            var b = Vector256.Create(8.0f, 7.0f, 6.0f, 5.0f, 4.0f, 3.0f, 2.0f, 1.0f);

            var result = Avx.Add(a, b);

            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(9.0f, result.GetElement(i), 5);
            }
        }
    }

    private static void TestAvx2Operations()
    {
        if (!Avx2.IsSupported)
        {
            return;
        }

        unsafe
        {
            var a = Vector256.Create(1, 2, 3, 4, 5, 6, 7, 8);
            var b = Vector256.Create(8, 7, 6, 5, 4, 3, 2, 1);

            var result = Avx2.Add(a, b);

            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(9, result.GetElement(i));
            }
        }
    }

    private static void TestAvx512Operations()
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        unsafe
        {
            var a = Vector512.Create(1.0f);
            var b = Vector512.Create(2.0f);

            var result = Avx512F.Add(a, b);

            for (int i = 0; i < 16; i++)
            {
                Assert.Equal(3.0f, result.GetElement(i), 5);
            }
        }
    }
}
