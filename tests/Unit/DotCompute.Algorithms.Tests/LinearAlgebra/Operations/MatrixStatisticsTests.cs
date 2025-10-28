// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.LinearAlgebra;
using DotCompute.Algorithms.LinearAlgebra.Operations;

namespace DotCompute.Algorithms.Tests.LinearAlgebra.Operations;

/// <summary>
/// Comprehensive tests for MatrixStatistics operations.
/// </summary>
public sealed class MatrixStatisticsTests
{
    private readonly IAccelerator _mockAccelerator;

    public MatrixStatisticsTests()
    {
        _mockAccelerator = Substitute.For<IAccelerator>();
        _mockAccelerator.Info.Returns(new AcceleratorInfo
        {
            DeviceType = "CPU",
            Name = "Mock CPU",
            Id = "mock-cpu-0"
        });
    }

    #region Frobenius Norm Tests

    [Fact]
    public void FrobeniusNorm_IdentityMatrix_ReturnsSquareRootOfDimension()
    {
        // Arrange
        var identity = Matrix.Identity(3);

        // Act
        var result = MatrixStatistics.FrobeniusNorm(identity);

        // Assert
        result.Should().BeApproximately(MathF.Sqrt(3), 0.0001f);
    }

    [Fact]
    public void FrobeniusNorm_ZeroMatrix_ReturnsZero()
    {
        // Arrange
        var zero = new Matrix(3, 3);

        // Act
        var result = MatrixStatistics.FrobeniusNorm(zero);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void FrobeniusNorm_SimpleMatrix_ReturnsCorrectValue()
    {
        // Arrange
        var matrix = new Matrix(2, 2, [3, 4, 0, 0]);
        var expected = 5.0f; // sqrt(3^2 + 4^2) = 5

        // Act
        var result = MatrixStatistics.FrobeniusNorm(matrix);

        // Assert
        result.Should().BeApproximately(expected, 0.0001f);
    }

    [Fact]
    public void FrobeniusNorm_NullMatrix_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            MatrixStatistics.FrobeniusNorm(null!));
    }

    #endregion

    #region One Norm Tests

    [Fact]
    public void OneNorm_IdentityMatrix_ReturnsOne()
    {
        // Arrange
        var identity = Matrix.Identity(3);

        // Act
        var result = MatrixStatistics.OneNorm(identity);

        // Assert
        result.Should().Be(1.0f);
    }

    [Fact]
    public void OneNorm_SimpleMatrix_ReturnsMaxColumnSum()
    {
        // Arrange
        var matrix = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var expected = 18.0f; // Column 3: 3 + 6 + 9 = 18

        // Act
        var result = MatrixStatistics.OneNorm(matrix);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void OneNorm_ZeroMatrix_ReturnsZero()
    {
        // Arrange
        var zero = new Matrix(3, 3);

        // Act
        var result = MatrixStatistics.OneNorm(zero);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void OneNorm_NegativeValues_UsesAbsoluteValues()
    {
        // Arrange
        var matrix = new Matrix(2, 2, [-1, 2, -3, 4]);

        // Act
        var result = MatrixStatistics.OneNorm(matrix);

        // Assert
        result.Should().Be(6.0f); // Column 2: |2| + |4| = 6
    }

    #endregion

    #region Infinity Norm Tests

    [Fact]
    public void InfinityNorm_IdentityMatrix_ReturnsOne()
    {
        // Arrange
        var identity = Matrix.Identity(4);

        // Act
        var result = MatrixStatistics.InfinityNorm(identity);

        // Assert
        result.Should().Be(1.0f);
    }

    [Fact]
    public void InfinityNorm_SimpleMatrix_ReturnsMaxRowSum()
    {
        // Arrange
        var matrix = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var expected = 24.0f; // Row 3: 7 + 8 + 9 = 24

        // Act
        var result = MatrixStatistics.InfinityNorm(matrix);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void InfinityNorm_NegativeValues_UsesAbsoluteValues()
    {
        // Arrange
        var matrix = new Matrix(2, 2, [1, -2, 3, -4]);

        // Act
        var result = MatrixStatistics.InfinityNorm(matrix);

        // Assert
        result.Should().Be(7.0f); // Row 2: |3| + |-4| = 7
    }

    #endregion

    #region Condition Number Tests

    [Fact]
    public async Task ConditionNumberAsync_IdentityMatrix_ReturnsOne()
    {
        // Arrange
        var identity = Matrix.Identity(3);

        // Act
        var result = await MatrixStatistics.ConditionNumberAsync(identity, _mockAccelerator);

        // Assert
        result.Should().BeApproximately(1.0f, 0.1f);
    }

    [Fact]
    public async Task ConditionNumberAsync_WellConditioned_ReturnsLowValue()
    {
        // Arrange
        var matrix = new Matrix(2, 2, [2, 1, 1, 2]);

        // Act
        var result = await MatrixStatistics.ConditionNumberAsync(matrix, _mockAccelerator);

        // Assert
        result.Should().BeLessThan(10.0f);
    }

    [Fact]
    public async Task ConditionNumberAsync_IllConditioned_ReturnsHighValue()
    {
        // Arrange - Nearly singular matrix
        var matrix = new Matrix(2, 2, [1, 1, 1, 1.0001f]);

        // Act
        var result = await MatrixStatistics.ConditionNumberAsync(matrix, _mockAccelerator);

        // Assert
        result.Should().BeGreaterThan(1000.0f);
    }

    [Fact]
    public async Task ConditionNumberAsync_SingularMatrix_ReturnsInfinity()
    {
        // Arrange
        var singular = new Matrix(2, 2, [1, 2, 2, 4]);

        // Act
        var result = await MatrixStatistics.ConditionNumberAsync(singular, _mockAccelerator);

        // Assert
        result.Should().Be(float.PositiveInfinity);
    }

    #endregion

    #region Trace Tests

    [Fact]
    public void Trace_IdentityMatrix_ReturnsDimension()
    {
        // Arrange
        var identity = Matrix.Identity(5);

        // Act
        var result = MatrixStatistics.Trace(identity);

        // Assert
        result.Should().Be(5.0f);
    }

    [Fact]
    public void Trace_DiagonalMatrix_ReturnsSumOfDiagonal()
    {
        // Arrange
        var matrix = new Matrix(3, 3);
        matrix[0, 0] = 1;
        matrix[1, 1] = 2;
        matrix[2, 2] = 3;

        // Act
        var result = MatrixStatistics.Trace(matrix);

        // Assert
        result.Should().Be(6.0f);
    }

    [Fact]
    public void Trace_NonSquareMatrix_ThrowsArgumentException()
    {
        // Arrange
        var matrix = new Matrix(3, 4);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MatrixStatistics.Trace(matrix));
    }

    [Fact]
    public void Trace_ZeroMatrix_ReturnsZero()
    {
        // Arrange
        var zero = new Matrix(4, 4);

        // Act
        var result = MatrixStatistics.Trace(zero);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    // TODO: Re-enable when MatrixStatistics.DeterminantAsync is implemented
    // #region Determinant Tests
    //
    // [Fact]
    // public async Task DeterminantAsync_IdentityMatrix_ReturnsOne()
    // {
    //     // Arrange
    //     var identity = Matrix.Identity(3);
    //
    //     // Act
    //     var result = await MatrixStatistics.DeterminantAsync(identity, _mockAccelerator);
    //
    //     // Assert
    //     result.Should().BeApproximately(1.0f, 0.0001f);
    // }
    //
    // [Fact]
    // public async Task DeterminantAsync_2x2Matrix_ReturnsCorrectValue()
    // {
    //     // Arrange
    //     var matrix = new Matrix(2, 2, [3, 8, 4, 6]);
    //     var expected = (3 * 6) - (8 * 4); // -14
    //
    //     // Act
    //     var result = await MatrixStatistics.DeterminantAsync(matrix, _mockAccelerator);
    //
    //     // Assert
    //     result.Should().BeApproximately(expected, 0.0001f);
    // }
    //
    // [Fact]
    // public async Task DeterminantAsync_SingularMatrix_ReturnsZero()
    // {
    //     // Arrange
    //     var singular = new Matrix(2, 2, [1, 2, 2, 4]);
    //
    //     // Act
    //     var result = await MatrixStatistics.DeterminantAsync(singular, _mockAccelerator);
    //
    //     // Assert
    //     result.Should().BeApproximately(0, 0.0001f);
    // }
    //
    // [Fact]
    // public async Task DeterminantAsync_NonSquareMatrix_ThrowsArgumentException()
    // {
    //     // Arrange
    //     var matrix = new Matrix(3, 4);
    //
    //     // Act & Assert
    //     await Assert.ThrowsAsync<ArgumentException>(() =>
    //         MatrixStatistics.DeterminantAsync(matrix, _mockAccelerator));
    // }
    //
    // #endregion

    #region Rank Tests

    [Fact]
    public async Task RankAsync_IdentityMatrix_ReturnsFullRank()
    {
        // Arrange
        var identity = Matrix.Identity(4);

        // Act
        var result = await MatrixStatistics.RankAsync(identity, _mockAccelerator);

        // Assert
        result.Should().Be(4);
    }

    [Fact]
    public async Task RankAsync_ZeroMatrix_ReturnsZero()
    {
        // Arrange
        var zero = new Matrix(3, 3);

        // Act
        var result = await MatrixStatistics.RankAsync(zero, _mockAccelerator);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task RankAsync_RankDeficientMatrix_ReturnsCorrectRank()
    {
        // Arrange - Rank 2 matrix
        var matrix = new Matrix(3, 3, [1, 2, 3, 2, 4, 6, 3, 6, 9]);

        // Act
        var result = await MatrixStatistics.RankAsync(matrix, _mockAccelerator);

        // Assert
        result.Should().Be(1); // All rows are multiples of first row
    }

    #endregion

    #region Edge Cases and Numerical Stability Tests

    [Fact]
    public void FrobeniusNorm_LargeValues_HandlesCorrectly()
    {
        // Arrange
        var matrix = new Matrix(2, 2, [1e10f, 1e10f, 1e10f, 1e10f]);

        // Act
        var result = MatrixStatistics.FrobeniusNorm(matrix);

        // Assert
        result.Should().BeGreaterThan(1e10f);
    }

    [Fact]
    public void OneNorm_SingleElement_ReturnsElementValue()
    {
        // Arrange
        var matrix = new Matrix(1, 1, [42]);

        // Act
        var result = MatrixStatistics.OneNorm(matrix);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task ConditionNumberAsync_NullMatrix_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            MatrixStatistics.ConditionNumberAsync(null!, _mockAccelerator));
    }

    [Fact]
    public void Trace_SingleElement_ReturnsElement()
    {
        // Arrange
        var matrix = new Matrix(1, 1, [7]);

        // Act
        var result = MatrixStatistics.Trace(matrix);

        // Assert
        result.Should().Be(7);
    }

    #endregion
}
