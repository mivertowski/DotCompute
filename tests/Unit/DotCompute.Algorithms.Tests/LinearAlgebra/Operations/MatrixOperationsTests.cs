// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.LinearAlgebra;
using DotCompute.Algorithms.LinearAlgebra.Operations;

namespace DotCompute.Algorithms.Tests.LinearAlgebra.Operations;

/// <summary>
/// Comprehensive tests for MatrixOperations with mocked accelerators.
/// </summary>
public sealed class MatrixOperationsTests
{
    private readonly IAccelerator _mockAccelerator;

    public MatrixOperationsTests()
    {
        _mockAccelerator = Substitute.For<IAccelerator>();
        _mockAccelerator.Info.Returns(new AcceleratorInfo
        {
            DeviceType = "CPU",
            Name = "Mock CPU",
            Id = "mock-cpu-0",
            MaxComputeUnits = 8
        });
    }

    #region Matrix Multiplication Tests

    [Fact]
    public async Task MultiplyAsync_ValidMatrices_ReturnsCorrectProduct()
    {
        // Arrange
        var a = new Matrix(2, 3, [1, 2, 3, 4, 5, 6]);
        var b = new Matrix(3, 2, [7, 8, 9, 10, 11, 12]);

        // Act
        var result = await MatrixOperations.MultiplyAsync(a, b, _mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        result.Rows.Should().Be(2);
        result.Columns.Should().Be(2);
        result[0, 0].Should().BeApproximately(58, 0.001f);
        result[0, 1].Should().BeApproximately(64, 0.001f);
        result[1, 0].Should().BeApproximately(139, 0.001f);
        result[1, 1].Should().BeApproximately(154, 0.001f);
    }

    [Fact]
    public async Task MultiplyAsync_IdentityMatrix_ReturnsSameMatrix()
    {
        // Arrange
        var a = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var identity = Matrix.Identity(3);

        // Act
        var result = await MatrixOperations.MultiplyAsync(a, identity, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result[i, j].Should().BeApproximately(a[i, j], 0.001f);
            }
        }
    }

    [Fact]
    public async Task MultiplyAsync_IncompatibleDimensions_ThrowsArgumentException()
    {
        // Arrange
        var a = new Matrix(2, 3);
        var b = new Matrix(2, 2);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MatrixOperations.MultiplyAsync(a, b, _mockAccelerator));
    }

    [Fact]
    public async Task MultiplyAsync_NullMatrix_ThrowsArgumentNullException()
    {
        // Arrange
        var a = new Matrix(2, 2);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            MatrixOperations.MultiplyAsync(null!, a, _mockAccelerator));
    }

    [Fact]
    public async Task MultiplyAsync_SingleElement_ReturnsScalarProduct()
    {
        // Arrange
        var a = new Matrix(1, 1, [5]);
        var b = new Matrix(1, 1, [7]);

        // Act
        var result = await MatrixOperations.MultiplyAsync(a, b, _mockAccelerator);

        // Assert
        result[0, 0].Should().BeApproximately(35, 0.001f);
    }

    [Fact]
    public async Task MultiplyAsync_LargeMatrices_CompletesSuccessfully()
    {
        // Arrange
        var size = 100;
        var a = Matrix.Random(size, size);
        var b = Matrix.Identity(size);

        // Act
        var result = await MatrixOperations.MultiplyAsync(a, b, _mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        result.Rows.Should().Be(size);
        result.Columns.Should().Be(size);
    }

    #endregion

    #region Matrix Addition Tests

    [Fact]
    public async Task AddAsync_ValidMatrices_ReturnsCorrectSum()
    {
        // Arrange
        var a = new Matrix(2, 2, [1, 2, 3, 4]);
        var b = new Matrix(2, 2, [5, 6, 7, 8]);

        // Act
        var result = await MatrixOperations.AddAsync(a, b, _mockAccelerator);

        // Assert
        result[0, 0].Should().BeApproximately(6, 0.001f);
        result[0, 1].Should().BeApproximately(8, 0.001f);
        result[1, 0].Should().BeApproximately(10, 0.001f);
        result[1, 1].Should().BeApproximately(12, 0.001f);
    }

    [Fact]
    public async Task AddAsync_ZeroMatrix_ReturnsSameMatrix()
    {
        // Arrange
        var a = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var zero = new Matrix(3, 3);

        // Act
        var result = await MatrixOperations.AddAsync(a, zero, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result[i, j].Should().BeApproximately(a[i, j], 0.001f);
            }
        }
    }

    [Fact]
    public async Task AddAsync_IncompatibleDimensions_ThrowsArgumentException()
    {
        // Arrange
        var a = new Matrix(2, 3);
        var b = new Matrix(3, 2);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MatrixOperations.AddAsync(a, b, _mockAccelerator));
    }

    [Fact]
    public async Task AddAsync_NegativeValues_HandlesCorrectly()
    {
        // Arrange
        var a = new Matrix(2, 2, [1, -2, 3, -4]);
        var b = new Matrix(2, 2, [-1, 2, -3, 4]);

        // Act
        var result = await MatrixOperations.AddAsync(a, b, _mockAccelerator);

        // Assert
        result[0, 0].Should().BeApproximately(0, 0.001f);
        result[0, 1].Should().BeApproximately(0, 0.001f);
        result[1, 0].Should().BeApproximately(0, 0.001f);
        result[1, 1].Should().BeApproximately(0, 0.001f);
    }

    #endregion

    #region Matrix Subtraction Tests

    [Fact]
    public async Task SubtractAsync_ValidMatrices_ReturnsCorrectDifference()
    {
        // Arrange
        var a = new Matrix(2, 2, [10, 20, 30, 40]);
        var b = new Matrix(2, 2, [5, 6, 7, 8]);

        // Act
        var result = await MatrixOperations.SubtractAsync(a, b, _mockAccelerator);

        // Assert
        result[0, 0].Should().BeApproximately(5, 0.001f);
        result[0, 1].Should().BeApproximately(14, 0.001f);
        result[1, 0].Should().BeApproximately(23, 0.001f);
        result[1, 1].Should().BeApproximately(32, 0.001f);
    }

    [Fact]
    public async Task SubtractAsync_SameMatrix_ReturnsZero()
    {
        // Arrange
        var a = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);

        // Act
        var result = await MatrixOperations.SubtractAsync(a, a, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result[i, j].Should().BeApproximately(0, 0.001f);
            }
        }
    }

    [Fact]
    public async Task SubtractAsync_IncompatibleDimensions_ThrowsArgumentException()
    {
        // Arrange
        var a = new Matrix(2, 3);
        var b = new Matrix(3, 3);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MatrixOperations.SubtractAsync(a, b, _mockAccelerator));
    }

    #endregion

    #region Matrix Transpose Tests

    [Fact]
    public async Task TransposeAsync_SquareMatrix_ReturnsCorrectTranspose()
    {
        // Arrange
        var a = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);

        // Act
        var result = await MatrixOperations.TransposeAsync(a, _mockAccelerator);

        // Assert
        result[0, 0].Should().BeApproximately(1, 0.001f);
        result[0, 1].Should().BeApproximately(4, 0.001f);
        result[0, 2].Should().BeApproximately(7, 0.001f);
        result[1, 0].Should().BeApproximately(2, 0.001f);
        result[1, 1].Should().BeApproximately(5, 0.001f);
        result[1, 2].Should().BeApproximately(8, 0.001f);
    }

    [Fact]
    public async Task TransposeAsync_RectangularMatrix_ReturnsCorrectTranspose()
    {
        // Arrange
        var a = new Matrix(2, 3, [1, 2, 3, 4, 5, 6]);

        // Act
        var result = await MatrixOperations.TransposeAsync(a, _mockAccelerator);

        // Assert
        result.Rows.Should().Be(3);
        result.Columns.Should().Be(2);
        result[0, 0].Should().BeApproximately(1, 0.001f);
        result[1, 0].Should().BeApproximately(2, 0.001f);
        result[2, 0].Should().BeApproximately(3, 0.001f);
    }

    [Fact]
    public async Task TransposeAsync_DoubleTranspose_ReturnsSameMatrix()
    {
        // Arrange
        var a = new Matrix(2, 3, [1, 2, 3, 4, 5, 6]);

        // Act
        var transpose1 = await MatrixOperations.TransposeAsync(a, _mockAccelerator);
        var transpose2 = await MatrixOperations.TransposeAsync(transpose1, _mockAccelerator);

        // Assert
        for (int i = 0; i < a.Rows; i++)
        {
            for (int j = 0; j < a.Columns; j++)
            {
                transpose2[i, j].Should().BeApproximately(a[i, j], 0.001f);
            }
        }
    }

    [Fact]
    public async Task TransposeAsync_SingleRowMatrix_ReturnsSingleColumnMatrix()
    {
        // Arrange
        var a = new Matrix(1, 4, [1, 2, 3, 4]);

        // Act
        var result = await MatrixOperations.TransposeAsync(a, _mockAccelerator);

        // Assert
        result.Rows.Should().Be(4);
        result.Columns.Should().Be(1);
        result[0, 0].Should().BeApproximately(1, 0.001f);
        result[3, 0].Should().BeApproximately(4, 0.001f);
    }

    #endregion

    #region Element-wise Operations Tests

    [Fact]
    public async Task ScalarMultiplyAsync_ValidMatrix_ReturnsScaledMatrix()
    {
        // Arrange
        var a = new Matrix(2, 2, [1, 2, 3, 4]);
        var scalar = 3.5f;

        // Act
        var result = await MatrixOperations.ScalarMultiplyAsync(a, scalar, _mockAccelerator);

        // Assert
        result[0, 0].Should().BeApproximately(3.5f, 0.001f);
        result[0, 1].Should().BeApproximately(7.0f, 0.001f);
        result[1, 0].Should().BeApproximately(10.5f, 0.001f);
        result[1, 1].Should().BeApproximately(14.0f, 0.001f);
    }

    [Fact]
    public async Task ScalarMultiplyAsync_ZeroScalar_ReturnsZeroMatrix()
    {
        // Arrange
        var a = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);

        // Act
        var result = await MatrixOperations.ScalarMultiplyAsync(a, 0, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result[i, j].Should().BeApproximately(0, 0.001f);
            }
        }
    }

    [Fact]
    public async Task ScalarMultiplyAsync_NegativeScalar_ReturnsNegatedMatrix()
    {
        // Arrange
        var a = new Matrix(2, 2, [1, 2, 3, 4]);

        // Act
        var result = await MatrixOperations.ScalarMultiplyAsync(a, -1, _mockAccelerator);

        // Assert
        result[0, 0].Should().BeApproximately(-1, 0.001f);
        result[0, 1].Should().BeApproximately(-2, 0.001f);
        result[1, 0].Should().BeApproximately(-3, 0.001f);
        result[1, 1].Should().BeApproximately(-4, 0.001f);
    }

    [Fact]
    public async Task ScalarMultiplyAsync_OneScalar_ReturnsSameMatrix()
    {
        // Arrange
        var a = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);

        // Act
        var result = await MatrixOperations.ScalarMultiplyAsync(a, 1, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result[i, j].Should().BeApproximately(a[i, j], 0.001f);
            }
        }
    }

    [Fact]
    public async Task ScalarMultiplyAsync_LargeMatrix_CompletesSuccessfully()
    {
        // Arrange
        var size = 100;
        var a = new Matrix(size, size);
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                a[i, j] = i * size + j;
            }
        }
        var scalar = 2.5f;

        // Act
        var result = await MatrixOperations.ScalarMultiplyAsync(a, scalar, _mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        result.Rows.Should().Be(size);
        result.Columns.Should().Be(size);
        result[0, 0].Should().BeApproximately(0, 0.001f);
        result[1, 1].Should().BeApproximately(101 * scalar, 0.001f);
    }

    [Fact]
    public async Task ScalarMultiplyAsync_NullMatrix_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            MatrixOperations.ScalarMultiplyAsync(null!, 2.0f, _mockAccelerator));
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public async Task MultiplyAsync_EmptyDimension_HandlesCorrectly()
    {
        // Arrange
        var a = new Matrix(2, 0);
        var b = new Matrix(0, 3);

        // Act
        var result = await MatrixOperations.MultiplyAsync(a, b, _mockAccelerator);

        // Assert
        result.Rows.Should().Be(2);
        result.Columns.Should().Be(3);
    }

    [Fact]
    public async Task AddAsync_LargeMatrices_CompletesSuccessfully()
    {
        // Arrange
        var size = 1000;
        var a = Matrix.Random(size, size);
        var b = Matrix.Random(size, size);

        // Act
        var result = await MatrixOperations.AddAsync(a, b, _mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        result.Rows.Should().Be(size);
        result.Columns.Should().Be(size);
    }

    [Fact]
    public async Task TransposeAsync_NullMatrix_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            MatrixOperations.TransposeAsync(null!, _mockAccelerator));
    }

    [Fact]
    public async Task MultiplyAsync_CancellationToken_CanBeCancelled()
    {
        // Arrange
        var a = Matrix.Random(1000, 1000);
        var b = Matrix.Random(1000, 1000);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            MatrixOperations.MultiplyAsync(a, b, _mockAccelerator, cts.Token));
    }

    [Fact]
    public async Task AddAsync_FloatingPointPrecision_HandlesCorrectly()
    {
        // Arrange
        var a = new Matrix(2, 2, [1e-10f, 1e10f, 1e-5f, 1e5f]);
        var b = new Matrix(2, 2, [1e-10f, 1e10f, 1e-5f, 1e5f]);

        // Act
        var result = await MatrixOperations.AddAsync(a, b, _mockAccelerator);

        // Assert
        result[0, 0].Should().BeApproximately(2e-10f, 1e-11f);
        result[0, 1].Should().BeApproximately(2e10f, 1e9f);
    }

    #endregion

    #region Performance and Optimization Tests

    [Fact]
    public async Task MultiplyAsync_BlockedAlgorithm_ProducesCorrectResult()
    {
        // Arrange - Use size that triggers blocked algorithm
        var size = 128;
        var a = Matrix.Identity(size);
        var b = Matrix.Random(size, size);

        // Act
        var result = await MatrixOperations.MultiplyAsync(a, b, _mockAccelerator);

        // Assert
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                result[i, j].Should().BeApproximately(b[i, j], 0.01f);
            }
        }
    }

    [Fact]
    public async Task AddAsync_VectorizedPath_ProducesCorrectResult()
    {
        // Arrange - Use size that triggers vectorization
        var size = 1024;
        var a = new Matrix(size, 1);
        var b = new Matrix(size, 1);
        for (int i = 0; i < size; i++)
        {
            a[i, 0] = i;
            b[i, 0] = i * 2;
        }

        // Act
        var result = await MatrixOperations.AddAsync(a, b, _mockAccelerator);

        // Assert
        for (int i = 0; i < size; i++)
        {
            result[i, 0].Should().BeApproximately(i * 3, 0.001f);
        }
    }

    #endregion
}
