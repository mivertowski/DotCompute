// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Optimized.Simd;

namespace DotCompute.Algorithms.Tests.Optimized.Simd;

/// <summary>
/// Comprehensive tests for SimdReductionOperations.
/// </summary>
public sealed class SimdReductionOperationsTests
{
    #region DotProduct Tests

    [Fact]
    public void DotProduct_SimpleVectors_ReturnsCorrectResult()
    {
        // Arrange
        var left = new float[] { 1, 2, 3, 4 };
        var right = new float[] { 5, 6, 7, 8 };
        var expected = 1 * 5 + 2 * 6 + 3 * 7 + 4 * 8; // 70

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeApproximately(expected, 0.0001f);
    }

    [Fact]
    public void DotProduct_OrthogonalVectors_ReturnsZero()
    {
        // Arrange
        var left = new float[] { 1, 0, 0 };
        var right = new float[] { 0, 1, 0 };

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeApproximately(0, 0.0001f);
    }

    [Fact]
    public void DotProduct_EmptyArrays_ReturnsZero()
    {
        // Arrange
        var left = Array.Empty<float>();
        var right = Array.Empty<float>();

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void DotProduct_SingleElement_ReturnsProduct()
    {
        // Arrange
        var left = new float[] { 7 };
        var right = new float[] { 3 };

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeApproximately(21, 0.0001f);
    }

    [Fact]
    public void DotProduct_LargeArrays_HandlesCorrectly()
    {
        // Arrange
        var size = 10000;
        var left = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var right = Enumerable.Range(0, size).Select(i => 1.0f).ToArray();
        var expected = left.Sum(); // Sum of 0 to 9999

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeApproximately(expected, 1.0f);
    }

    [Fact]
    public void DotProduct_NegativeValues_HandlesCorrectly()
    {
        // Arrange
        var left = new float[] { -1, -2, -3 };
        var right = new float[] { 4, 5, 6 };
        var expected = -1 * 4 + -2 * 5 + -3 * 6; // -32

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeApproximately(expected, 0.0001f);
    }

    [Fact]
    public void DotProduct_DifferentLengths_ThrowsArgumentException()
    {
        // Arrange
        var left = new float[] { 1, 2, 3 };
        var right = new float[] { 1, 2 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SimdReductionOperations.DotProduct(left, right));
    }

    [Fact]
    public void DotProduct_VectorizedSize_UsesOptimizedPath()
    {
        // Arrange - Size that triggers vectorization
        var size = 256;
        var left = Enumerable.Repeat(1.0f, size).ToArray();
        var right = Enumerable.Repeat(2.0f, size).ToArray();
        var expected = size * 2.0f;

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeApproximately(expected, 0.1f);
    }

    #endregion

    #region HorizontalSum Tests

    [Fact]
    public void HorizontalSum_SimpleArray_ReturnsSum()
    {
        // Arrange
        var values = new float[] { 1, 2, 3, 4, 5 };
        var expected = 15.0f;

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().BeApproximately(expected, 0.0001f);
    }

    [Fact]
    public void HorizontalSum_EmptyArray_ReturnsZero()
    {
        // Arrange
        var values = Array.Empty<float>();

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void HorizontalSum_SingleElement_ReturnsElement()
    {
        // Arrange
        var values = new float[] { 42 };

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void HorizontalSum_AllZeros_ReturnsZero()
    {
        // Arrange
        var values = new float[100];

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void HorizontalSum_NegativeValues_HandlesCorrectly()
    {
        // Arrange
        var values = new float[] { -1, -2, -3, -4 };
        var expected = -10.0f;

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().BeApproximately(expected, 0.0001f);
    }

    [Fact]
    public void HorizontalSum_MixedValues_ReturnsCorrectSum()
    {
        // Arrange
        var values = new float[] { 10, -5, 3, -2, 7 };
        var expected = 13.0f;

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().BeApproximately(expected, 0.0001f);
    }

    [Fact]
    public void HorizontalSum_LargeArray_HandlesCorrectly()
    {
        // Arrange
        var size = 10000;
        var values = Enumerable.Range(0, size).Select(i => 1.0f).ToArray();
        var expected = size;

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().BeApproximately(expected, 0.1f);
    }

    [Fact]
    public void HorizontalSum_VectorizedSize_UsesOptimizedPath()
    {
        // Arrange - Size that triggers AVX512/AVX2 vectorization
        var size = 512;
        var values = Enumerable.Repeat(0.5f, size).ToArray();
        var expected = size * 0.5f;

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().BeApproximately(expected, 0.5f);
    }

    #endregion

    #region Min/Max Operations Tests

    [Fact]
    public void Min_SimpleArray_ReturnsMinimum()
    {
        // Arrange
        var values = new float[] { 5, 2, 9, 1, 7 };

        // Act
        var result = SimdReductionOperations.Min(values);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void Min_SingleElement_ReturnsElement()
    {
        // Arrange
        var values = new float[] { 42 };

        // Act
        var result = SimdReductionOperations.Min(values);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void Min_NegativeValues_ReturnsLowest()
    {
        // Arrange
        var values = new float[] { -1, -5, -2, -10, -3 };

        // Act
        var result = SimdReductionOperations.Min(values);

        // Assert
        result.Should().Be(-10);
    }

    [Fact]
    public void Max_SimpleArray_ReturnsMaximum()
    {
        // Arrange
        var values = new float[] { 5, 2, 9, 1, 7 };

        // Act
        var result = SimdReductionOperations.Max(values);

        // Assert
        result.Should().Be(9);
    }

    [Fact]
    public void Max_SingleElement_ReturnsElement()
    {
        // Arrange
        var values = new float[] { 42 };

        // Act
        var result = SimdReductionOperations.Max(values);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void Max_NegativeValues_ReturnsHighest()
    {
        // Arrange
        var values = new float[] { -5, -2, -10, -1, -3 };

        // Act
        var result = SimdReductionOperations.Max(values);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void MinMax_LargeArray_HandlesCorrectly()
    {
        // Arrange
        var size = 10000;
        var values = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        // Act
        var min = SimdReductionOperations.Min(values);
        var max = SimdReductionOperations.Max(values);

        // Assert
        min.Should().Be(0);
        max.Should().Be(size - 1);
    }

    #endregion

    #region Cross-Platform SIMD Path Tests

    [Fact]
    public void DotProduct_FallbackPath_ProducesCorrectResult()
    {
        // Arrange - Small size that may use fallback
        var left = new float[] { 1, 2 };
        var right = new float[] { 3, 4 };
        var expected = 11.0f;

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeApproximately(expected, 0.0001f);
    }

    [Fact]
    public void HorizontalSum_Avx512Size_HandlesCorrectly()
    {
        // Arrange - Size aligned to AVX-512 (16 floats)
        var size = 512;
        var values = Enumerable.Range(0, size).Select(i => 1.0f).ToArray();

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().BeApproximately(size, 0.5f);
    }

    [Fact]
    public void DotProduct_Avx2Size_HandlesCorrectly()
    {
        // Arrange - Size aligned to AVX2 (8 floats)
        var size = 256;
        var left = Enumerable.Repeat(2.0f, size).ToArray();
        var right = Enumerable.Repeat(3.0f, size).ToArray();
        var expected = size * 6.0f;

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeApproximately(expected, 1.0f);
    }

    #endregion

    #region Numerical Stability Tests

    [Fact]
    public void DotProduct_VerySmallValues_MaintainsPrecision()
    {
        // Arrange
        var left = new float[] { 1e-6f, 1e-6f, 1e-6f };
        var right = new float[] { 1e-6f, 1e-6f, 1e-6f };
        var expected = 3e-12f;

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeApproximately(expected, 1e-13f);
    }

    [Fact]
    public void HorizontalSum_VeryLargeValues_HandlesOverflow()
    {
        // Arrange
        var values = new float[] { 1e20f, 1e20f, 1e20f };

        // Act
        var result = SimdReductionOperations.HorizontalSum(values);

        // Assert
        result.Should().BeGreaterThan(1e20f);
    }

    [Fact]
    public void DotProduct_MixedMagnitudes_HandlesCorrectly()
    {
        // Arrange
        var left = new float[] { 1e10f, 1e-10f, 1e10f };
        var right = new float[] { 1, 1, 1 };

        // Act
        var result = SimdReductionOperations.DotProduct(left, right);

        // Assert
        result.Should().BeGreaterThan(1e10f);
    }

    #endregion
}
