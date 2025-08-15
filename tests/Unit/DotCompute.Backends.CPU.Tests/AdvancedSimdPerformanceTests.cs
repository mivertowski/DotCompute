// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CA5394 // Do not use insecure randomness - Random is used for performance testing, not security
#pragma warning disable CA1822 // Mark members as static - Test methods cannot be static in xUnit

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using DotCompute.Backends.CPU.Kernels;
using Xunit.Abstractions;

#pragma warning disable CA1515 // Make types internal
namespace DotCompute.Backends.CPU;

/// <summary>
/// Production-level performance tests for advanced SIMD implementations.
/// Validates critical path completion: FMA, integer SIMD, ARM NEON, and advanced operations.
/// </summary>
public sealed class AdvancedSimdPerformanceTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region FMA(Fused Multiply-Add) Performance Tests - CRITICAL

    [Fact]
    public void FmaFloat32ShowsCriticalPerformanceGain()
    {
        const int elementCount = 1_000_000;
        var a = new float[elementCount];
        var b = new float[elementCount];
        var c = new float[elementCount];
        var result = new float[elementCount];
        var scalarResult = new float[elementCount];

        // Initialize with random data
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            a[i] = (float)random.NextDouble() * 10.0f;
            b[i] = (float)random.NextDouble() * 10.0f;
            c[i] = (float)random.NextDouble() * 10.0f;
        }

        // Warm-up
        FmaScalarFloat32(a, b, c, scalarResult);
        unsafe
        {
            fixed (float* pA = a, pB = b, pC = c, pResult = result)
            {
                AdvancedSimdKernels.VectorFmaFloat32(pA, pB, pC, pResult, elementCount);
            }
        }

        // Measure scalar FMA performance
        var scalarTime = MeasureTime(() => FmaScalarFloat32(a, b, c, scalarResult), 100);
        _output.WriteLine($"FMA Scalar time: {scalarTime:F2}ms");

        // Measure SIMD FMA performance
        var simdTime = MeasureTime(() =>
        {
            unsafe
            {
                fixed (float* pA = a, pB = b, pC = c, pResult = result)
                {
                    AdvancedSimdKernels.VectorFmaFloat32(pA, pB, pC, pResult, elementCount);
                }
            }
        }, 100);
        _output.WriteLine($"FMA SIMD time: {simdTime:F2}ms");

        // Calculate speedup
        var speedup = scalarTime / simdTime;
        _output.WriteLine($"FMA SIMD speedup: {speedup:F2}x");

        // Validate correctness(allowing for FMA precision differences)
        ValidateResultsFloat32(scalarResult, result, elementCount, 1e-5f);

        // Assert critical performance gain - FMA should show significant speedup
        if (Fma.IsSupported)
        {
            PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 3.0, "FMA SIMD with hardware");
        }
        else
        {
            PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 1.5, "FMA SIMD without hardware");
        }
    }

    [Fact]
    public void FmaFloat64ShowsCriticalDoubleSpeedup()
    {
        const int elementCount = 500_000;
        var a = new double[elementCount];
        var b = new double[elementCount];
        var c = new double[elementCount];
        var result = new double[elementCount];
        var scalarResult = new double[elementCount];

        // Initialize with random data
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            a[i] = random.NextDouble() * 10.0;
            b[i] = random.NextDouble() * 10.0;
            c[i] = random.NextDouble() * 10.0;
        }

        // Measure performance
        var scalarTime = MeasureTime(() => FmaScalarFloat64(a, b, c, scalarResult), 50);
        var simdTime = MeasureTime(() =>
        {
            unsafe
            {
                fixed (double* pA = a, pB = b, pC = c, pResult = result)
                {
                    AdvancedSimdKernels.VectorFmaFloat64(pA, pB, pC, pResult, elementCount);
                }
            }
        }, 50);

        var speedup = scalarTime / simdTime;
        _output.WriteLine($"FMA Double precision speedup: {speedup:F2}x");

        // Validate correctness
        ValidateResultsFloat64(scalarResult, result, elementCount, 1e-14);

        PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 2.0, "FMA double precision");
    }

    #endregion

    #region Integer SIMD Performance Tests - CRITICAL

    [Fact]
    public void IntegerSimdShowsCriticalPathPerformance()
    {
        const int elementCount = 2_000_000;
        var a = new int[elementCount];
        var b = new int[elementCount];
        var result = new int[elementCount];
        var scalarResult = new int[elementCount];

        // Initialize with random data
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            a[i] = random.Next(-1000, 1000);
            b[i] = random.Next(-1000, 1000);
        }

        // Measure scalar integer addition
        var scalarTime = MeasureTime(() =>
        {
            for (var i = 0; i < elementCount; i++)
            {
                scalarResult[i] = a[i] + b[i];
            }
        }, 100);

        // Measure SIMD integer addition
        var simdTime = MeasureTime(() =>
        {
            unsafe
            {
                fixed (int* pA = a, pB = b, pResult = result)
                {
                    AdvancedSimdKernels.VectorAddInt32(pA, pB, pResult, elementCount);
                }
            }
        }, 100);

        var speedup = scalarTime / simdTime;
        _output.WriteLine($"Integer SIMD speedup: {speedup:F2}x");

        // Validate correctness
        for (var i = 0; i < elementCount; i++)
        {
            Assert.Equal(scalarResult[i], result[i]);
        }

        // Critical: Integer SIMD should unlock significant performance
        if (Avx2.IsSupported)
        {
            PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 4.0, "integer SIMD with AVX2");
        }
        else
        {
            PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 2.0, "integer SIMD");
        }
    }

    [Fact]
    public void Int16SimdShowsImageProcessingPerformance()
    {
        const int elementCount = 4_000_000; // Typical for large images
        var a = new short[elementCount];
        var b = new short[elementCount];
        var result = new short[elementCount];

        // Initialize with typical image data
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            a[i] = (short)random.Next(0, 256);
            b[i] = (short)random.Next(0, 256);
        }

        // Measure SIMD performance
        var simdTime = MeasureTime(() =>
        {
            unsafe
            {
                fixed (short* pA = a, pB = b, pResult = result)
                {
                    AdvancedSimdKernels.VectorAddInt16(pA, pB, pResult, elementCount);
                }
            }
        }, 50);

        _output.WriteLine($"Int16 SIMD time for {elementCount} elements: {simdTime:F2}ms");

        // Should process 4M 16-bit values very quickly
        simdTime.Should().BeLessThan(10.0, $"Expected Int16 SIMD to process 4M elements in < 10ms, but took {simdTime:F2}ms");
    }

    #endregion

    #region ARM NEON Enhanced Performance Tests - HIGH PRIORITY

    [Fact]
    public void ArmNeonAdvancedShowsCrossplatformExcellence()
    {
        const int elementCount = 1_000_000;
        var a = new float[elementCount];
        var b = new float[elementCount];
        var c = new float[elementCount];
        var result = new float[elementCount];

        // Initialize data
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
            c[i] = (float)random.NextDouble();
        }

        if (AdvSimd.IsSupported)
        {
            // Test various NEON operations
            var operations = new[]
            {
                NeonOperation.Add,
                NeonOperation.Multiply,
                NeonOperation.MultiplyAdd,
                NeonOperation.Maximum,
                NeonOperation.Minimum
            };

            foreach (var operation in operations)
            {
                var time = MeasureTime(() =>
                {
                    unsafe
                    {
                        fixed (float* pA = a, pB = b, pC = c, pResult = result)
                        {
                            AdvancedSimdKernels.VectorAdvancedNeonFloat32(pA, pB, pC, pResult, elementCount, operation);
                        }
                    }
                }, 50);

                _output.WriteLine($"ARM NEON {operation} time: {time:F2}ms");

                // Validate ARM NEON performance
                time.Should().BeLessThan(5.0, $"ARM NEON {operation} should complete in < 5ms, but took {time:F2}ms");
            }
        }
        else
        {
            _output.WriteLine("ARM NEON not supported on this platform - skipping NEON-specific tests");
        }
    }

    #endregion

    #region Advanced Operations Performance Tests - HIGH PRIORITY

    [Fact]
    public void GatherScatterShowsAdvancedSimdCapabilities()
    {
        const int elementCount = 100_000;
        var data = new float[elementCount * 2]; // Double size for scatter space
        var indices = new int[elementCount];
        var gathered = new float[elementCount];
        var values = new float[elementCount];

        // Initialize data
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            data[i] = (float)random.NextDouble();
            indices[i] = random.Next(0, elementCount); // Random indices for gather
            values[i] = (float)random.NextDouble();
        }

        if (Avx2.IsSupported)
        {
            // Test gather operation
            var gatherTime = MeasureTime(() =>
            {
                unsafe
                {
                    fixed (float* pData = data, pGathered = gathered)
                    fixed (int* pIndices = indices)
                    {
                        AdvancedSimdKernels.VectorGatherFloat32(pData, pIndices, pGathered, elementCount);
                    }
                }
            }, 20);

            _output.WriteLine($"SIMD Gather time: {gatherTime:F2}ms");

            // Test scatter operation(AVX-512 only)
            if (Avx512F.IsSupported)
            {
                var scatterTime = MeasureTime(() =>
                {
                    unsafe
                    {
                        fixed (float* pData = data, pValues = values)
                        fixed (int* pIndices = indices)
                        {
                            AdvancedSimdKernels.VectorScatterFloat32(pValues, pIndices, pData + elementCount, elementCount);
                        }
                    }
                }, 20);

                _output.WriteLine($"SIMD Scatter time: {scatterTime:F2}ms");
                scatterTime.Should().BeLessThan(5.0, $"AVX-512 scatter should complete quickly, but took {scatterTime:F2}ms");
            }

            gatherTime.Should().BeLessThan(10.0, $"AVX2 gather should complete quickly, but took {gatherTime:F2}ms");
        }
        else
        {
            _output.WriteLine("AVX2+ not supported - skipping gather/scatter tests");
        }
    }

    [Fact]
    public void ConditionalSelectShowsMaskingPerformance()
    {
        const int elementCount = 1_000_000;
        var condition = new float[elementCount];
        var a = new float[elementCount];
        var b = new float[elementCount];
        var result = new float[elementCount];
        var scalarResult = new float[elementCount];

        const float threshold = 0.5f;

        // Initialize data
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            condition[i] = (float)random.NextDouble();
            a[i] = (float)random.NextDouble() * 10.0f;
            b[i] = (float)random.NextDouble() * 20.0f;
        }

        // Measure scalar conditional selection
        var scalarTime = MeasureTime(() =>
        {
            for (var i = 0; i < elementCount; i++)
            {
                scalarResult[i] = condition[i] > threshold ? a[i] : b[i];
            }
        }, 50);

        // Measure SIMD conditional selection
        var simdTime = MeasureTime(() =>
        {
            unsafe
            {
                fixed (float* pCond = condition, pA = a, pB = b, pResult = result)
                {
                    AdvancedSimdKernels.VectorConditionalSelect(pCond, pA, pB, pResult, elementCount, threshold);
                }
            }
        }, 50);

        var speedup = scalarTime / simdTime;
        _output.WriteLine($"Conditional select speedup: {speedup:F2}x");

        // Validate correctness
        ValidateResultsFloat32(scalarResult, result, elementCount, 1e-6f);

        // Masking should provide good speedup by avoiding branch divergence
        PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 2.0, "conditional select");
    }

    [Fact]
    public void HorizontalReductionShowsOptimalPerformance()
    {
        const int elementCount = 10_000_000;
        var data = new float[elementCount];

        // Initialize with random data
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            data[i] = (float)random.NextDouble();
        }

        // Measure scalar sum
        var scalarTime = MeasureTime(() =>
        {
            var sum = 0.0f;
            for (var i = 0; i < elementCount; i++)
            {
                sum += data[i];
            }
            return sum;
        }, 20);

        // Measure SIMD horizontal sum
        var simdSum = 0.0f;
        var simdTime = MeasureTime(() =>
        {
            unsafe
            {
                fixed (float* pData = data)
                {
                    simdSum = AdvancedSimdKernels.VectorHorizontalSum(pData, elementCount);
                }
            }
            return simdSum;
        }, 20);

        var speedup = scalarTime / simdTime;
        _output.WriteLine($"Horizontal reduction speedup: {speedup:F2}x");

        // Should achieve excellent speedup for reduction
        PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 4.0, "horizontal reduction");
    }

    #endregion

    #region Matrix Multiplication Performance Tests - HIGH PRIORITY

    [Fact]
    public void OptimizedMatrixMultiplyShowsLinearAlgebraPerformance()
    {
        const int size = 256; // Reasonable size for testing
        var a = new float[size * size];
        var b = new float[size * size];
        var result = new float[size * size];
        var scalarResult = new float[size * size];

        // Initialize matrices
        var random = new Random(42);
        for (var i = 0; i < a.Length; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }

        // Measure scalar matrix multiplication
        var scalarTime = MeasureTime(() =>
        {
            MatrixMultiplyScalar(a, b, scalarResult, size);
        }, 5);

        // Measure optimized SIMD matrix multiplication
        var simdTime = MeasureTime(() =>
        {
            unsafe
            {
                fixed (float* pA = a, pB = b, pResult = result)
                {
                    AdvancedSimdKernels.OptimizedMatrixMultiplyFloat32(pA, pB, pResult, size, size, size);
                }
            }
        }, 5);

        var speedup = scalarTime / simdTime;
        _output.WriteLine($"Optimized matrix multiply speedup: {speedup:F2}x");

        // Matrix multiplication should show excellent speedup with FMA
        if (Fma.IsSupported)
        {
            PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 5.0, "optimized matrix multiply with FMA");
        }
        else
        {
            PerformanceTestHelpers.AssertSpeedupWithinExpectedRange(speedup, 3.0, "optimized matrix multiply");
        }
    }

    #endregion

    #region Performance Validation and Roadmap Completion Tests

    [Fact]
    public void PerformanceRoadmapTargetsAreAchieved()
    {
        _output.WriteLine("=== SIMD Roadmap Performance Validation ===");

        // Report detected capabilities
        ReportSimdCapabilities();

        // Validate all critical implementations are working
        const int testSize = 100_000;

        // 1. FMA Performance Target: Should achieve significant speedup
        var fmaSpeedup = TestFmaPerformance(testSize);
        _output.WriteLine($"✓ FMA Performance: {fmaSpeedup:F2}x speedup");

        // 2. Integer SIMD Performance Target
        var intSpeedup = TestIntegerSimdPerformance(testSize);
        _output.WriteLine($"✓ Integer SIMD Performance: {intSpeedup:F2}x speedup");

        // 3. Cross-platform compatibility
        var crossPlatformScore = TestCrossPlatformCompatibility();
        _output.WriteLine($"✓ Cross-platform Compatibility: {crossPlatformScore}");

        // Assert roadmap completion
        fmaSpeedup.Should().BeGreaterThanOrEqualTo(2.0, "FMA implementation must achieve at least 2x speedup");
        intSpeedup.Should().BeGreaterThanOrEqualTo(2.0, "Integer SIMD must achieve at least 2x speedup");
        crossPlatformScore.Should().BeGreaterThanOrEqualTo(80, "Cross-platform compatibility must be >= 80%");

        _output.WriteLine("=== SIMD Roadmap Completion: SUCCESS ===");
    }

    #endregion

    #region Helper Methods

    private static double MeasureTime(Func<float> action, int iterations)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            action();
        }
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / iterations;
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

    private static void FmaScalarFloat32(float[] a, float[] b, float[] c, float[] result)
    {
        for (var i = 0; i < a.Length; i++)
        {
            result[i] = MathF.FusedMultiplyAdd(a[i], b[i], c[i]);
        }
    }

    private static void FmaScalarFloat64(double[] a, double[] b, double[] c, double[] result)
    {
        for (var i = 0; i < a.Length; i++)
        {
            result[i] = Math.FusedMultiplyAdd(a[i], b[i], c[i]);
        }
    }

    private static void MatrixMultiplyScalar(float[] a, float[] b, float[] result, int size)
    {
        Array.Clear(result, 0, result.Length);
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                for (var k = 0; k < size; k++)
                {
                    result[i * size + j] += a[i * size + k] * b[k * size + j];
                }
            }
        }
    }

    private static void ValidateResultsFloat32(float[] expected, float[] actual, int count, float tolerance)
    {
        for (var i = 0; i < count; i++)
        {
            var diff = Math.Abs(expected[i] - actual[i]);
            diff.Should().BeLessThanOrEqualTo(tolerance,
                $"Results differ at index {i}: expected={expected[i]}, actual={actual[i]}, diff={diff}, tolerance={tolerance}");
        }
    }

    private static void ValidateResultsFloat64(double[] expected, double[] actual, int count, double tolerance)
    {
        for (var i = 0; i < count; i++)
        {
            var diff = Math.Abs(expected[i] - actual[i]);
            diff.Should().BeLessThanOrEqualTo(tolerance,
                $"Results differ at index {i}: expected={expected[i]}, actual={actual[i]}, diff={diff}, tolerance={tolerance}");
        }
    }

    private void ReportSimdCapabilities()
    {
        _output.WriteLine($"Vector<T>.IsHardwareAccelerated: {Vector.IsHardwareAccelerated}");
        _output.WriteLine($"Vector<float>.Count: {Vector<float>.Count}");

        if (RuntimeInformation.ProcessArchitecture is Architecture.X64 or
            Architecture.X86)
        {
            _output.WriteLine($"SSE: {Sse.IsSupported}");
            _output.WriteLine($"SSE2: {Sse2.IsSupported}");
            _output.WriteLine($"AVX: {Avx.IsSupported}");
            _output.WriteLine($"AVX2: {Avx2.IsSupported}");
            _output.WriteLine($"FMA: {Fma.IsSupported}");
            _output.WriteLine($"AVX-512F: {Avx512F.IsSupported}");
        }
        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            _output.WriteLine($"AdvSimd: {AdvSimd.IsSupported}");
            _output.WriteLine($"AdvSimd.Arm64: {AdvSimd.Arm64.IsSupported}");
        }
    }

    private double TestFmaPerformance(int testSize)
    {
        var a = new float[testSize];
        var b = new float[testSize];
        var c = new float[testSize];
        var result = new float[testSize];
        var scalarResult = new float[testSize];

        var random = new Random(42);
        for (var i = 0; i < testSize; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
            c[i] = (float)random.NextDouble();
        }

        var scalarTime = MeasureTime(() => FmaScalarFloat32(a, b, c, scalarResult), 50);
        var simdTime = MeasureTime(() =>
        {
            unsafe
            {
                fixed (float* pA = a, pB = b, pC = c, pResult = result)
                {
                    AdvancedSimdKernels.VectorFmaFloat32(pA, pB, pC, pResult, testSize);
                }
            }
        }, 50);

        return scalarTime / simdTime;
    }

    private double TestIntegerSimdPerformance(int testSize)
    {
        var a = new int[testSize];
        var b = new int[testSize];
        var result = new int[testSize];
        var scalarResult = new int[testSize];

        var random = new Random(42);
        for (var i = 0; i < testSize; i++)
        {
            a[i] = random.Next(-1000, 1000);
            b[i] = random.Next(-1000, 1000);
        }

        var scalarTime = MeasureTime(() =>
        {
            for (var i = 0; i < testSize; i++)
            {
                scalarResult[i] = a[i] + b[i];
            }
        }, 50);

        var simdTime = MeasureTime(() =>
        {
            unsafe
            {
                fixed (int* pA = a, pB = b, pResult = result)
                {
                    AdvancedSimdKernels.VectorAddInt32(pA, pB, pResult, testSize);
                }
            }
        }, 50);

        return scalarTime / simdTime;
    }

    private int TestCrossPlatformCompatibility()
    {
        var score = 0;
        var totalTests = 5;

        // Test 1: Basic vector support
        if (Vector.IsHardwareAccelerated)
        {
            score++;
        }

        // Test 2: Platform-specific SIMD
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64 && Sse2.IsSupported)
        {
            score++;
        }

        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 && AdvSimd.IsSupported)
        {
            score++;
        }

        // Test 3: Advanced features
        if (Avx2.IsSupported || AdvSimd.IsSupported)
        {
            score++;
        }

        // Test 4: FMA support
        if (Fma.IsSupported || AdvSimd.IsSupported)
        {
            score++;
        }

        // Test 5: Wide vectors
        if (Avx512F.IsSupported || Vector<float>.Count >= 4)
        {
            score++;
        }

        return (score * 100) / totalTests;
    }

    #endregion
}
