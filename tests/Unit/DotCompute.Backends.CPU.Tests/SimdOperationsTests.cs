// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Tests.Common;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Tests for SIMD operations including vector operation correctness, different vector widths,
/// fallback to scalar operations, and performance comparisons.
/// </summary>
[Trait("Category", TestCategories.HardwareIndependent)]
public class SimdOperationsTests
{
    [Fact]
    public void SimdCapabilities_IsSupported_ReturnsValidValue()
    {
        // Act
        var isSupported = SimdCapabilities.IsSupported;
        
        // Assert
        isSupported.Should().Be(Vector.IsHardwareAccelerated);
    }
    
    [Fact]
    public void SimdCapabilities_PreferredVectorWidth_ReturnsValidWidth()
    {
        // Act
        var vectorWidth = SimdCapabilities.PreferredVectorWidth;
        
        // Assert
        vectorWidth.Should().BeGreaterThan(0);
        vectorWidth.Should().BeOneOf(64, 128, 256, 512);
    }
    
    [Fact]
    public void SimdCapabilities_GetSummary_ReturnsCompleteInformation()
    {
        // Act
        var summary = SimdCapabilities.GetSummary();
        
        // Assert
        summary.Should().NotBeNull();
        summary.IsHardwareAccelerated.Should().Be(Vector.IsHardwareAccelerated);
        summary.PreferredVectorWidth.Should().BeGreaterThan(0);
        summary.SupportedInstructionSets.Should().NotBeNull();
        
        if (Vector.IsHardwareAccelerated)
        {
            summary.SupportedInstructionSets.Should().NotBeEmpty();
        }
    }
    
    [SkippableFactAttribute]
    [Trait("Category", TestCategories.RequiresSIMD)]
    public void X86SimdInfo_SSE_Detection_ReturnsCorrectValues()
    {
        Skip.IfNot(Environment.Is64BitProcess && (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Unix));
        
        // Act & Assert
        X86SimdInfo.HasSse.Should().Be(Sse.IsSupported);
        X86SimdInfo.HasSse2.Should().Be(Sse2.IsSupported);
        X86SimdInfo.HasSse3.Should().Be(Sse3.IsSupported);
        X86SimdInfo.HasSsse3.Should().Be(Ssse3.IsSupported);
        X86SimdInfo.HasSse41.Should().Be(Sse41.IsSupported);
        X86SimdInfo.HasSse42.Should().Be(Sse42.IsSupported);
    }
    
    [SkippableFactAttribute]
    [Trait("Category", TestCategories.RequiresSIMD)]
    public void X86SimdInfo_AVX_Detection_ReturnsCorrectValues()
    {
        Skip.IfNot(Environment.Is64BitProcess && (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Unix));
        
        // Act & Assert
        X86SimdInfo.HasAvx.Should().Be(Avx.IsSupported);
        X86SimdInfo.HasAvx2.Should().Be(Avx2.IsSupported);
        X86SimdInfo.HasAvx512F.Should().Be(Avx512F.IsSupported);
        X86SimdInfo.HasAvx512BW.Should().Be(Avx512BW.IsSupported);
    }
    
    [SkippableFactAttribute]
    [Trait("Category", TestCategories.RequiresSIMD)]
    public void X86SimdInfo_AdditionalInstructions_Detection_ReturnsCorrectValues()
    {
        Skip.IfNot(Environment.Is64BitProcess && (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Unix));
        
        // Act & Assert
        X86SimdInfo.HasFma.Should().Be(Fma.IsSupported);
        X86SimdInfo.HasBmi1.Should().Be(Bmi1.IsSupported);
        X86SimdInfo.HasBmi2.Should().Be(Bmi2.IsSupported);
        X86SimdInfo.HasPopcnt.Should().Be(Popcnt.IsSupported);
        X86SimdInfo.HasLzcnt.Should().Be(Lzcnt.IsSupported);
    }
    
    [Fact]
    public void X86SimdInfo_MaxVectorWidth_ReturnsCorrectWidth()
    {
        // Act
        var maxWidth = X86SimdInfo.MaxVectorWidth;
        
        // Assert
        if (Avx512F.IsSupported)
        {
            maxWidth.Should().Be(512);
        }
        else if (Avx.IsSupported)
        {
            maxWidth.Should().Be(256);
        }
        else if (Sse.IsSupported)
        {
            maxWidth.Should().Be(128);
        }
        else
        {
            maxWidth.Should().Be(0);
        }
    }
    
    [SkippableFactAttribute]
    [Trait("Category", TestCategories.RequiresSIMD)]
    public void ArmSimdInfo_NEON_Detection_ReturnsCorrectValues()
    {
        Skip.IfNot(Environment.Is64BitProcess && Environment.OSVersion.Platform == PlatformID.Unix);
        
        // Act & Assert
        ArmSimdInfo.HasNeon.Should().Be(AdvSimd.IsSupported);
        ArmSimdInfo.HasNeonArm64.Should().Be(AdvSimd.Arm64.IsSupported);
    }
    
    [SkippableFactAttribute]
    [Trait("Category", TestCategories.RequiresSIMD)]
    public void ArmSimdInfo_AdditionalInstructions_Detection_ReturnsCorrectValues()
    {
        Skip.IfNot(Environment.Is64BitProcess && Environment.OSVersion.Platform == PlatformID.Unix);
        
        // Act & Assert
        ArmSimdInfo.HasCrc32.Should().Be(Crc32.IsSupported);
        ArmSimdInfo.HasAes.Should().Be(global::System.Runtime.Intrinsics.Arm.Aes.IsSupported);
        ArmSimdInfo.HasSha1.Should().Be(Sha1.IsSupported);
        ArmSimdInfo.HasSha256.Should().Be(Sha256.IsSupported);
        ArmSimdInfo.HasDp.Should().Be(Dp.IsSupported);
        ArmSimdInfo.HasRdm.Should().Be(Rdm.IsSupported);
    }
    
    [Fact]
    public void ArmSimdInfo_MaxVectorWidth_ReturnsCorrectWidth()
    {
        // Act
        var maxWidth = ArmSimdInfo.MaxVectorWidth;
        
        // Assert
        if (AdvSimd.IsSupported)
        {
            maxWidth.Should().Be(128);
        }
        else
        {
            maxWidth.Should().Be(0);
        }
    }
    
    [Fact]
    public void SimdSummary_BooleanProperties_ReturnCorrectValues()
    {
        // Arrange
        var summary = SimdCapabilities.GetSummary();
        
        // Act & Assert
        summary.SupportsSse2.Should().Be(summary.SupportedInstructionSets.Contains("SSE2"));
        summary.SupportsAvx2.Should().Be(summary.SupportedInstructionSets.Contains("AVX2"));
        summary.SupportsAvx512.Should().Be(summary.SupportedInstructionSets.Contains("AVX512F"));
        summary.SupportsAdvSimd.Should().Be(summary.SupportedInstructionSets.Contains("NEON"));
    }
    
    [Fact]
    public void SimdSummary_ToString_ReturnsReadableString()
    {
        // Arrange
        var summary = SimdCapabilities.GetSummary();
        
        // Act
        var stringRepresentation = summary.ToString();
        
        // Assert
        stringRepresentation.Should().NotBeNullOrWhiteSpace();
        
        if (summary.IsHardwareAccelerated)
        {
            stringRepresentation.Should().Contain("SIMD");
            stringRepresentation.Should().Contain(summary.PreferredVectorWidth.ToString());
        }
        else
        {
            stringRepresentation.Should().Contain("No SIMD support");
        }
    }
    
    [Theory]
    [InlineData(4)]   // SSE-sized vectors
    [InlineData(8)]   // AVX-sized vectors  
    [InlineData(16)]  // AVX512-sized vectors
    public void VectorOperations_Addition_ProducesCorrectResults(int vectorSize)
    {
        // Arrange
        var a = new float[vectorSize];
        var b = new float[vectorSize];
        var expected = new float[vectorSize];
        var result = new float[vectorSize];
        
        for (int i = 0; i < vectorSize; i++)
        {
            a[i] = i + 1.0f;
            b[i] = i + 2.0f;
            expected[i] = a[i] + b[i];
        }
        
        // Act
        PerformVectorAddition(a, b, result);
        
        // Assert
        result.Should().Equal(expected);
    }
    
    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void VectorOperations_Multiplication_ProducesCorrectResults(int vectorSize)
    {
        // Arrange
        var a = new float[vectorSize];
        var b = new float[vectorSize];
        var expected = new float[vectorSize];
        var result = new float[vectorSize];
        
        for (int i = 0; i < vectorSize; i++)
        {
            a[i] = i + 1.0f;
            b[i] = 2.0f;
            expected[i] = a[i] * b[i];
        }
        
        // Act
        PerformVectorMultiplication(a, b, result);
        
        // Assert
        result.Should().Equal(expected);
    }
    
    [Fact]
    [Trait("Category", TestCategories.Performance)]
    public void VectorOperations_Performance_SimdFasterThanScalar()
    {
        // Arrange
        const int size = 1024 * 1024; // 1M elements
        var a = new float[size];
        var b = new float[size];
        var scalarResult = new float[size];
        var vectorResult = new float[size];
        
        for (int i = 0; i < size; i++)
        {
            a[i] = i;
            b[i] = i + 1;
        }
        
        // Act - Scalar operation
        var scalarStopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < size; i++)
        {
            scalarResult[i] = a[i] + b[i];
        }
        scalarStopwatch.Stop();
        
        // Act - Vector operation  
        var vectorStopwatch = System.Diagnostics.Stopwatch.StartNew();
        PerformVectorAddition(a, b, vectorResult);
        vectorStopwatch.Stop();
        
        // Assert
        vectorResult.Should().Equal(scalarResult);
        
        // Performance assertion - SIMD should be faster if hardware accelerated
        if (Vector.IsHardwareAccelerated && Vector<float>.Count > 1)
        {
            vectorStopwatch.ElapsedMilliseconds.Should().BeLessOrEqualTo(scalarStopwatch.ElapsedMilliseconds);
        }
    }
    
    [Fact]
    public void FallbackToScalar_WhenSimdUnavailable_ProducesCorrectResults()
    {
        // Arrange
        var a = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var b = new float[] { 5.0f, 6.0f, 7.0f, 8.0f };
        var expected = new float[] { 6.0f, 8.0f, 10.0f, 12.0f };
        var result = new float[4];
        
        // Act - Force scalar path
        PerformScalarAddition(a, b, result);
        
        // Assert
        result.Should().Equal(expected);
    }
    
    [Theory]
    [InlineData(new float[] { 1.0f, 2.0f, 3.0f, 4.0f }, new float[] { 1.0f, 4.0f, 9.0f, 16.0f })]
    [InlineData(new float[] { -1.0f, -2.0f, 3.0f, -4.0f }, new float[] { 1.0f, 4.0f, 9.0f, 16.0f })]
    public void VectorOperations_AbsoluteValue_ProducesCorrectResults(float[] input, float[] expected)
    {
        // Arrange
        var result = new float[input.Length];
        
        // Act
        PerformVectorAbs(input, result);
        
        // Assert
        result.Should().Equal(expected, options => options.Using<float>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 0.001f)));
    }
    
    [Theory]
    [InlineData(new float[] { 1.0f, 4.0f, 9.0f, 16.0f }, new float[] { 1.0f, 2.0f, 3.0f, 4.0f })]
    [InlineData(new float[] { 0.25f, 1.0f, 2.25f, 4.0f }, new float[] { 0.5f, 1.0f, 1.5f, 2.0f })]
    public void VectorOperations_SquareRoot_ProducesCorrectResults(float[] input, float[] expected)
    {
        // Arrange
        var result = new float[input.Length];
        
        // Act
        PerformVectorSqrt(input, result);
        
        // Assert
        result.Should().Equal(expected, options => options.Using<float>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 0.001f)));
    }
    
    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public void VectorOperations_WithNullInputs_HandlesGracefully()
    {
        // Arrange
        float[]? nullArray = null;
        var validArray = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var result = new float[4];
        
        // Act & Assert
        FluentActions.Invoking(() => PerformVectorAddition(nullArray!, validArray, result))
            .Should().Throw<ArgumentNullException>();
        
        FluentActions.Invoking(() => PerformVectorAddition(validArray, nullArray!, result))
            .Should().Throw<ArgumentNullException>();
        
        FluentActions.Invoking(() => PerformVectorAddition(validArray, validArray, nullArray!))
            .Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public void VectorOperations_WithMismatchedSizes_HandlesGracefully()
    {
        // Arrange
        var shortArray = new float[] { 1.0f, 2.0f };
        var longArray = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var result = new float[2];
        
        // Act & Assert
        FluentActions.Invoking(() => PerformVectorAddition(shortArray, longArray, result))
            .Should().Throw<ArgumentException>();
    }
    
    [Fact]
    [Trait("Category", TestCategories.Concurrency)]
    public void VectorOperations_ConcurrentExecution_ProducesConsistentResults()
    {
        // Arrange
        const int arraySize = 1024;
        var a = Enumerable.Range(0, arraySize).Select(i => (float)i).ToArray();
        var b = Enumerable.Range(0, arraySize).Select(i => (float)(i + 1)).ToArray();
        var expected = a.Zip(b, (x, y) => x + y).ToArray();
        
        var tasks = new Task<float[]>[Environment.ProcessorCount];
        
        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var result = new float[arraySize];
                PerformVectorAddition(a, b, result);
                return result;
            });
        }
        
        var results = Task.WhenAll(tasks).Result;
        
        // Assert
        foreach (var result in results)
        {
            result.Should().Equal(expected);
        }
    }
    
    // Helper methods for vector operations
    private static void PerformVectorAddition(float[] a, float[] b, float[] result)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(result);
        
        if (a.Length != b.Length || a.Length != result.Length)
            throw new ArgumentException("Array lengths must match");
        
        if (Vector.IsHardwareAccelerated && Vector<float>.Count > 1)
        {
            int vectorSize = Vector<float>.Count;
            int vectorLength = a.Length - (a.Length % vectorSize);
            
            for (int i = 0; i < vectorLength; i += vectorSize)
            {
                var vectorA = new Vector<float>(a, i);
                var vectorB = new Vector<float>(b, i);
                (vectorA + vectorB).CopyTo(result, i);
            }
            
            // Handle remaining elements
            for (int i = vectorLength; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }
        else
        {
            PerformScalarAddition(a, b, result);
        }
    }
    
    private static void PerformScalarAddition(float[] a, float[] b, float[] result)
    {
        for (int i = 0; i < a.Length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }
    
    private static void PerformVectorMultiplication(float[] a, float[] b, float[] result)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(result);
        
        if (Vector.IsHardwareAccelerated && Vector<float>.Count > 1)
        {
            int vectorSize = Vector<float>.Count;
            int vectorLength = a.Length - (a.Length % vectorSize);
            
            for (int i = 0; i < vectorLength; i += vectorSize)
            {
                var vectorA = new Vector<float>(a, i);
                var vectorB = new Vector<float>(b, i);
                (vectorA * vectorB).CopyTo(result, i);
            }
            
            for (int i = vectorLength; i < a.Length; i++)
            {
                result[i] = a[i] * b[i];
            }
        }
        else
        {
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] * b[i];
            }
        }
    }
    
    private static void PerformVectorAbs(float[] input, float[] result)
    {
        if (Vector.IsHardwareAccelerated && Vector<float>.Count > 1)
        {
            int vectorSize = Vector<float>.Count;
            int vectorLength = input.Length - (input.Length % vectorSize);
            
            for (int i = 0; i < vectorLength; i += vectorSize)
            {
                var vector = new Vector<float>(input, i);
                Vector.Abs(vector).CopyTo(result, i);
            }
            
            for (int i = vectorLength; i < input.Length; i++)
            {
                result[i] = Math.Abs(input[i]);
            }
        }
        else
        {
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = Math.Abs(input[i]);
            }
        }
    }
    
    private static void PerformVectorSqrt(float[] input, float[] result)
    {
        if (Vector.IsHardwareAccelerated && Vector<float>.Count > 1)
        {
            int vectorSize = Vector<float>.Count;
            int vectorLength = input.Length - (input.Length % vectorSize);
            
            for (int i = 0; i < vectorLength; i += vectorSize)
            {
                var vector = new Vector<float>(input, i);
                Vector.SquareRoot(vector).CopyTo(result, i);
            }
            
            for (int i = vectorLength; i < input.Length; i++)
            {
                result[i] = (float)Math.Sqrt(input[i]);
            }
        }
        else
        {
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = (float)Math.Sqrt(input[i]);
            }
        }
    }
}