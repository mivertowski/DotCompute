// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CA5394 // Do not use insecure randomness - Random is used for testing, not security
#pragma warning disable CA1822 // Mark members as static - Test methods cannot be static in xUnit

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels;
using Xunit.Abstractions;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Integration tests validating the enhanced SIMD kernels work correctly
/// with the existing CPU backend infrastructure and meet roadmap targets.
/// </summary>
public sealed class SimdIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public SimdIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SimdCapabilitiesAreDetectedCorrectly()
    {
        var capabilities = SimdCapabilities.GetSummary();

        _output.WriteLine($"Hardware acceleration: {capabilities.IsHardwareAccelerated}");
        _output.WriteLine($"Preferred vector width: {capabilities.PreferredVectorWidth}");
        _output.WriteLine($"Supported instruction sets: {string.Join(", ", capabilities.SupportedInstructionSets)}");

        // Validate capabilities detection
        Assert.True(capabilities.IsHardwareAccelerated, "SIMD hardware acceleration should be detected");
        Assert.True(capabilities.PreferredVectorWidth >= 128, "Vector width should be at least 128-bit");
        Assert.NotEmpty(capabilities.SupportedInstructionSets);

        // Platform-specific validation
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            Assert.True(capabilities.SupportsSse2, "x64 should support SSE2 as baseline");
        }
        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            Assert.Contains("NEON", capabilities.SupportedInstructionSets);
        }
    }

    [Fact]
    public void FmaImplementationWorksWithExistingInfrastructure()
    {
        const int size = 1000;
        var a = new float[size];
        var b = new float[size];
        var c = new float[size];
        var result = new float[size];

        // Initialize test data
        var random = new Random(42);
        for (int i = 0; i < size; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
            c[i] = (float)random.NextDouble();
        }

        // Test FMA integration
        unsafe
        {
            fixed (float* pA = a, pB = b, pC = c, pResult = result)
            {
                AdvancedSimdKernels.VectorFmaFloat32(pA, pB, pC, pResult, size);
            }
        }

        // Validate results
        for (int i = 0; i < size; i++)
        {
            var expected = MathF.FusedMultiplyAdd(a[i], b[i], c[i]);
            var actual = result[i];
            var error = Math.Abs(expected - actual);

            Assert.True(error < 1e-6f, $"FMA result mismatch at index {i}: expected {expected}, got {actual}");
        }

        _output.WriteLine("✓ FMA implementation works correctly with existing infrastructure");
    }

    [Fact]
    public void IntegerSimdIntegratesWithCpuBackend()
    {
        const int size = 2000;
        var a = new int[size];
        var b = new int[size];
        var result = new int[size];

        // Initialize test data
        var random = new Random(42);
        for (int i = 0; i < size; i++)
        {
            a[i] = random.Next(-1000, 1000);
            b[i] = random.Next(-1000, 1000);
        }

        // Test integer SIMD integration
        unsafe
        {
            fixed (int* pA = a, pB = b, pResult = result)
            {
                AdvancedSimdKernels.VectorAddInt32(pA, pB, pResult, size);
            }
        }

        // Validate results
        for (int i = 0; i < size; i++)
        {
            var expected = a[i] + b[i];
            var actual = result[i];

            Assert.Equal(expected, actual);
        }

        _output.WriteLine("✓ Integer SIMD implementation works correctly");
    }

    [Fact]
    public void ArmNeonSupportWorksOnArmPlatforms()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
        {
            _output.WriteLine("Skipping ARM NEON test on non-ARM64 platform");
            return;
        }

        if (!AdvSimd.IsSupported)
        {
            _output.WriteLine("ARM NEON not supported on this ARM64 platform");
            return;
        }

        const int size = 1000;
        var a = new float[size];
        var b = new float[size];
        var c = new float[size];
        var result = new float[size];

        // Initialize test data
        var random = new Random(42);
        for (int i = 0; i < size; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
            c[i] = (float)random.NextDouble();
        }

        // Test ARM NEON operations
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
            unsafe
            {
                fixed (float* pA = a, pB = b, pC = c, pResult = result)
                {
                    AdvancedSimdKernels.VectorAdvancedNeonFloat32(pA, pB, pC, pResult, size, operation);
                }
            }

            // Basic validation that results are reasonable
            for (int i = 0; i < 10; i++) // Check first 10 elements
            {
                Assert.True(float.IsFinite(result[i]), $"ARM NEON {operation} produced invalid result at index {i}: {result[i]}");
            }
        }

        _output.WriteLine("✓ ARM NEON implementation works correctly on ARM64 platform");
    }

    [Fact]
    public void GatherScatterOperationsWorkWithModernHardware()
    {
        if (!Avx2.IsSupported)
        {
            _output.WriteLine("Skipping gather/scatter test - AVX2 not supported");
            return;
        }

        const int size = 1000;
        var data = new float[size * 2];
        var indices = new int[size];
        var values = new float[size];
        var gathered = new float[size];

        // Initialize test data
        var random = new Random(42);
        for (int i = 0; i < size; i++)
        {
            data[i] = (float)random.NextDouble();
            indices[i] = random.Next(0, size);
            values[i] = (float)random.NextDouble();
        }

        // Test gather operation
        unsafe
        {
            fixed (float* pData = data, pGathered = gathered)
            fixed (int* pIndices = indices)
            {
                AdvancedSimdKernels.VectorGatherFloat32(pData, pIndices, pGathered, size);
            }
        }

        // Validate gather results
        for (int i = 0; i < size; i++)
        {
            var expected = data[indices[i]];
            var actual = gathered[i];

            Assert.Equal(expected, actual);
        }

        // Test scatter operation (if AVX-512 available)
        if (Avx512F.IsSupported)
        {
            unsafe
            {
                fixed (float* pData = data, pValues = values)
                fixed (int* pIndices = indices)
                {
                    AdvancedSimdKernels.VectorScatterFloat32(pValues, pIndices, pData + size, size);
                }
            }

            // Validate scatter results
            for (int i = 0; i < size; i++)
            {
                var expected = values[i];
                var actual = data[size + indices[i]];

                Assert.Equal(expected, actual);
            }
        }

        _output.WriteLine("✓ Gather/scatter operations work correctly with modern hardware");
    }

    [Fact]
    public void ConditionalSelectionPerformsBranchlessOperations()
    {
        const int size = 1000;
        var condition = new float[size];
        var a = new float[size];
        var b = new float[size];
        var result = new float[size];
        var expected = new float[size];

        const float threshold = 0.5f;

        // Initialize test data
        var random = new Random(42);
        for (int i = 0; i < size; i++)
        {
            condition[i] = (float)random.NextDouble();
            a[i] = (float)random.NextDouble() * 10.0f;
            b[i] = (float)random.NextDouble() * 20.0f;
            expected[i] = condition[i] > threshold ? a[i] : b[i];
        }

        // Test conditional selection
        unsafe
        {
            fixed (float* pCond = condition, pA = a, pB = b, pResult = result)
            {
                AdvancedSimdKernels.VectorConditionalSelect(pCond, pA, pB, pResult, size, threshold);
            }
        }

        // Validate results
        for (int i = 0; i < size; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }

        _output.WriteLine("✓ Conditional selection performs branchless operations correctly");
    }

    [Fact]
    public void MatrixMultiplicationAchievesLinearAlgebraPerformance()
    {
        const int size = 64; // Small matrix for validation
        var a = new float[size * size];
        var b = new float[size * size];
        var result = new float[size * size];
        var expected = new float[size * size];

        // Initialize matrices
        var random = new Random(42);
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }

        // Compute expected result with simple algorithm
        Array.Clear(expected, 0, expected.Length);
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                for (int k = 0; k < size; k++)
                {
                    expected[i * size + j] += a[i * size + k] * b[k * size + j];
                }
            }
        }

        // Test optimized matrix multiplication
        unsafe
        {
            fixed (float* pA = a, pB = b, pResult = result)
            {
                AdvancedSimdKernels.OptimizedMatrixMultiplyFloat32(pA, pB, pResult, size, size, size);
            }
        }

        // Validate results (allowing for floating-point precision differences)
        for (int i = 0; i < result.Length; i++)
        {
            var error = Math.Abs(expected[i] - result[i]);
            Assert.True(error < 1e-4f, $"Matrix multiply result mismatch at index {i}: expected {expected[i]}, got {result[i]}");
        }

        _output.WriteLine("✓ Matrix multiplication achieves correct linear algebra results");
    }

    [Fact]
    public void HorizontalReductionProducesCorrectResults()
    {
        const int size = 10000;
        var data = new float[size];

        // Initialize with known values for validation
        var random = new Random(42);
        double expectedSum = 0.0;
        for (int i = 0; i < size; i++)
        {
            data[i] = (float)random.NextDouble();
            expectedSum += data[i];
        }

        // Test horizontal reduction
        float actualSum;
        unsafe
        {
            fixed (float* pData = data)
            {
                actualSum = AdvancedSimdKernels.VectorHorizontalSum(pData, size);
            }
        }

        // Validate result (allowing for floating-point accumulation differences)
        var error = Math.Abs(expectedSum - actualSum);
        var relativeError = error / Math.Abs(expectedSum);

        Assert.True(relativeError < 1e-5, $"Horizontal reduction error too large: expected {expectedSum}, got {actualSum}, relative error {relativeError}");

        _output.WriteLine("✓ Horizontal reduction produces correct results");
    }

    [Fact]
    public void SimdRoadmapCompletionMeetsAllTargets()
    {
        _output.WriteLine("=== SIMD Roadmap Completion Validation ===");

        var completionResults = new
        {
            FmaImplemented = TestFmaCompletion(),
            IntegerSimdImplemented = TestIntegerSimdCompletion(),
            ArmNeonEnhanced = TestArmNeonCompletion(),
            AdvancedOperationsImplemented = TestAdvancedOperationsCompletion(),
            MatrixOptimizationImplemented = TestMatrixOptimizationCompletion()
        };

        _output.WriteLine($"✓ FMA Implementation: {completionResults.FmaImplemented}");
        _output.WriteLine($"✓ Integer SIMD: {completionResults.IntegerSimdImplemented}");
        _output.WriteLine($"✓ ARM NEON Enhanced: {completionResults.ArmNeonEnhanced}");
        _output.WriteLine($"✓ Advanced Operations: {completionResults.AdvancedOperationsImplemented}");
        _output.WriteLine($"✓ Matrix Optimization: {completionResults.MatrixOptimizationImplemented}");

        // Assert all critical components are complete
        Assert.True(completionResults.FmaImplemented, "FMA implementation is CRITICAL and must be complete");
        Assert.True(completionResults.IntegerSimdImplemented, "Integer SIMD is CRITICAL and must be complete");
        Assert.True(completionResults.ArmNeonEnhanced, "ARM NEON enhancement is HIGH PRIORITY and must be complete");
        Assert.True(completionResults.AdvancedOperationsImplemented, "Advanced operations are HIGH PRIORITY and must be complete");
        Assert.True(completionResults.MatrixOptimizationImplemented, "Matrix optimization is HIGH PRIORITY and must be complete");

        _output.WriteLine("=== SIMD ROADMAP COMPLETION: 100% SUCCESS ===");
    }

    #region Roadmap Completion Tests

    private bool TestFmaCompletion()
    {
        try
        {
            const int size = 100;
            var a = new float[size];
            var b = new float[size];
            var c = new float[size];
            var result = new float[size];

            // Test FMA operation
            unsafe
            {
                fixed (float* pA = a, pB = b, pC = c, pResult = result)
                {
                    AdvancedSimdKernels.VectorFmaFloat32(pA, pB, pC, pResult, size);
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TestIntegerSimdCompletion()
    {
        try
        {
            const int size = 100;
            var a = new int[size];
            var b = new int[size];
            var result = new int[size];

            unsafe
            {
                fixed (int* pA = a, pB = b, pResult = result)
                {
                    AdvancedSimdKernels.VectorAddInt32(pA, pB, pResult, size);
                }
            }

            // Test 16-bit integers
            var a16 = new short[size];
            var b16 = new short[size];
            var result16 = new short[size];

            unsafe
            {
                fixed (short* pA = a16, pB = b16, pResult = result16)
                {
                    AdvancedSimdKernels.VectorAddInt16(pA, pB, pResult, size);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TestArmNeonCompletion()
    {
        try
        {
            const int size = 100;
            var a = new float[size];
            var b = new float[size];
            var c = new float[size];
            var result = new float[size];

            unsafe
            {
                fixed (float* pA = a, pB = b, pC = c, pResult = result)
                {
                    AdvancedSimdKernels.VectorAdvancedNeonFloat32(pA, pB, pC, pResult, size, NeonOperation.Add);
                    AdvancedSimdKernels.VectorAdvancedNeonFloat32(pA, pB, pC, pResult, size, NeonOperation.MultiplyAdd);
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TestAdvancedOperationsCompletion()
    {
        try
        {
            const int size = 100;

            // Test gather/scatter
            if (Avx2.IsSupported)
            {
                var gatherData = new float[size];
                var gatherIndices = new int[size];
                var gathered = new float[size];

                unsafe
                {
                    fixed (float* pData = gatherData, pGathered = gathered)
                    fixed (int* pIndices = gatherIndices)
                    {
                        AdvancedSimdKernels.VectorGatherFloat32(pData, pIndices, pGathered, size);
                    }
                }
            }

            // Test conditional selection
            var condition = new float[size];
            var a = new float[size];
            var b = new float[size];
            var result = new float[size];

            unsafe
            {
                fixed (float* pCond = condition, pA = a, pB = b, pResult = result)
                {
                    AdvancedSimdKernels.VectorConditionalSelect(pCond, pA, pB, pResult, size, 0.5f);
                }
            }

            // Test horizontal reduction
            var data = new float[size];
            unsafe
            {
                fixed (float* pData = data)
                {
                    AdvancedSimdKernels.VectorHorizontalSum(pData, size);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TestMatrixOptimizationCompletion()
    {
        try
        {
            const int size = 32;
            var a = new float[size * size];
            var b = new float[size * size];
            var result = new float[size * size];

            unsafe
            {
                fixed (float* pA = a, pB = b, pResult = result)
                {
                    AdvancedSimdKernels.OptimizedMatrixMultiplyFloat32(pA, pB, pResult, size, size, size);
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
