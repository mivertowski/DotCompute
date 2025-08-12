// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.


using System.Numerics;

using System.Runtime.Intrinsics.X86;
using DotCompute.Backends.CPU.Intrinsics;
using Xunit.Abstractions;

#pragma warning disable CA1515 // Make types internal
namespace DotCompute.Backends.CPU;

/// <summary>
/// Tests for SIMD capabilities detection and reporting.
/// </summary>
public sealed class SimdCapabilitiesTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void DetectCapabilitiesReturnsValidSummary()
    {
        // Act
        var summary = SimdCapabilities.GetSummary();

        // Assert
        Assert.NotNull(summary);
        Assert.True(summary.PreferredVectorWidth > 0);
        Assert.NotNull(summary.SupportedInstructionSets);

        _output.WriteLine($"Hardware Accelerated: {summary.IsHardwareAccelerated}");
        _output.WriteLine($"Preferred Vector Width: {summary.PreferredVectorWidth}");
        _output.WriteLine($"Supported Instruction Sets: {string.Join(", ", summary.SupportedInstructionSets)}");
    }

    [Fact]
    public void VectorSizesAreConsistent()
    {
        // Act
        var vectorSizeBits = Vector<float>.Count * sizeof(float) * 8;
        var preferredWidth = SimdCapabilities.PreferredVectorWidth;

        // Assert
        _output.WriteLine($"Vector<float>.Count: {Vector<float>.Count}");
        _output.WriteLine($"Vector size in bits: {vectorSizeBits}");
        _output.WriteLine($"Preferred width: {preferredWidth}");

        // The preferred width should be at least as large as the vector size
        Assert.True(preferredWidth >= vectorSizeBits || preferredWidth == 0);
    }

    [Fact]
    public void InstructionSetSupportIsCorrect()
    {
        // Arrange
        var summary = SimdCapabilities.GetSummary();

        // Act & Assert
        if (Sse.IsSupported)
        {
            Assert.Contains("SSE", summary.SupportedInstructionSets);
            _output.WriteLine("SSE is supported");
        }

        if (Sse2.IsSupported)
        {
            Assert.Contains("SSE2", summary.SupportedInstructionSets);
            _output.WriteLine("SSE2 is supported");
        }

        if (Avx.IsSupported)
        {
            Assert.Contains("AVX", summary.SupportedInstructionSets);
            _output.WriteLine("AVX is supported");
        }

        if (Avx2.IsSupported)
        {
            Assert.Contains("AVX2", summary.SupportedInstructionSets);
            _output.WriteLine("AVX2 is supported");
        }

        if (Avx512F.IsSupported)
        {
            Assert.Contains("AVX512F", summary.SupportedInstructionSets);
            _output.WriteLine("AVX512F is supported");
        }
    }

    [Fact]
    public void SimdSummaryPropertiesWork()
    {
        // Arrange
        var summary = new SimdSummary
        {
            IsHardwareAccelerated = true,
            PreferredVectorWidth = 256,
            SupportedInstructionSets = new HashSet<string> { "SSE", "SSE2", "AVX", "AVX2" }
        };

        // Assert
        Assert.True(summary.IsHardwareAccelerated);
        Assert.Equal(256, summary.PreferredVectorWidth);
        Assert.True(summary.SupportsAvx2);
        Assert.False(summary.SupportsAvx512);
    }


    [Theory]
    [InlineData(128, 4)]  // SSE: 128-bit = 4 floats
    [InlineData(256, 8)]  // AVX2: 256-bit = 8 floats
    [InlineData(512, 16)] // AVX512: 512-bit = 16 floats
    public void VectorWidthCorrespondsToFloatCount(int vectorWidth, int expectedFloatCount)
    {
        // Act
        var floatCount = vectorWidth / (sizeof(float) * 8);

        // Assert
        Assert.Equal(expectedFloatCount, floatCount);
        _output.WriteLine($"{vectorWidth}-bit vector holds {floatCount} floats");
    }

    [Fact]
    public void ProcessorCountMatchesEnvironment()
    {
        // Act
        var processorCount = Environment.ProcessorCount;

        // Assert
        Assert.True(processorCount > 0);
        _output.WriteLine($"Processor count: {processorCount}");
    }

    [Fact]
    public void SimdOperationsProduceCorrectResults()
    {
        // Test basic SIMD operations to ensure they work correctly
        const int count = 8;
        var a = new float[count] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var b = new float[count] { 8, 7, 6, 5, 4, 3, 2, 1 };
        var result = new float[count];

        // Test with Vector<T>
        if (Vector.IsHardwareAccelerated)
        {
            var vectorSize = Vector<float>.Count;
            for (var i = 0; i < count; i += vectorSize)
            {
                var va = new Vector<float>(a, i);
                var vb = new Vector<float>(b, i);
                var vr = va + vb;
                vr.CopyTo(result, i);
            }

            // All results should be 9
            for (var i = 0; i < Math.Min(count, vectorSize); i++)
            {
                Assert.Equal(9.0f, result[i], 0.0001f);
            }

            _output.WriteLine($"Vector<T> operations work correctly with size {vectorSize}");
        }
    }
}
