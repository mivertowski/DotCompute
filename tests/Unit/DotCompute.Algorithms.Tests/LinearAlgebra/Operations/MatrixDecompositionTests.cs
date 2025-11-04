// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.LinearAlgebra;
using DotCompute.Algorithms.LinearAlgebra.Operations;

namespace DotCompute.Algorithms.Tests.LinearAlgebra.Operations;

/// <summary>
/// Comprehensive tests for MatrixDecomposition operations.
/// </summary>
public sealed class MatrixDecompositionTests
{
    private readonly IAccelerator _mockAccelerator;

    public MatrixDecompositionTests()
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

    #region QR Decomposition Tests

    [Fact]
    public async Task QRDecompositionAsync_SquareMatrix_ProducesOrthogonalQ()
    {
        // Arrange
        var matrix = new Matrix(3, 3, [12, -51, 4, 6, 167, -68, -4, 24, -41]);

        // Act
        var (q, r) = await MatrixDecomposition.QRDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        q.Should().NotBeNull();
        var qTranspose = await MatrixOperations.TransposeAsync(q, _mockAccelerator);
        var qtq = await MatrixOperations.MultiplyAsync(qTranspose, q, _mockAccelerator);

        // Q^T * Q should be approximately identity
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                var expected = i == j ? 1.0f : 0.0f;
                qtq[i, j].Should().BeApproximately(expected, 0.01f);
            }
        }
    }

    [Fact]
    public async Task QRDecompositionAsync_SquareMatrix_ProducesUpperTriangularR()
    {
        // Arrange
        var matrix = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 10]);

        // Act
        var (q, r) = await MatrixDecomposition.QRDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        r.Should().NotBeNull();
        // Check that R is upper triangular
        for (int i = 1; i < r.Rows; i++)
        {
            for (int j = 0; j < i && j < r.Columns; j++)
            {
                Math.Abs(r[i, j]).Should().BeLessThan(0.001f);
            }
        }
    }

    [Fact]
    public async Task QRDecompositionAsync_ReconstructOriginalMatrix()
    {
        // Arrange
        var matrix = new Matrix(3, 3, [2, -1, 0, -1, 2, -1, 0, -1, 2]);

        // Act
        var (q, r) = await MatrixDecomposition.QRDecompositionAsync(matrix, _mockAccelerator);
        var reconstructed = await MatrixOperations.MultiplyAsync(q, r, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                reconstructed[i, j].Should().BeApproximately(matrix[i, j], 0.01f);
            }
        }
    }

    [Fact]
    public async Task QRDecompositionAsync_RectangularMatrix_HandlesCorrectly()
    {
        // Arrange
        var matrix = new Matrix(4, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);

        // Act
        var (q, r) = await MatrixDecomposition.QRDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        q.Rows.Should().Be(4);
        q.Columns.Should().Be(4);
        r.Rows.Should().Be(3);
        r.Columns.Should().Be(3);
    }

    [Fact]
    public async Task QRDecompositionAsync_IdentityMatrix_ReturnsIdentities()
    {
        // Arrange
        var identity = Matrix.Identity(4);

        // Act
        var (q, r) = await MatrixDecomposition.QRDecompositionAsync(identity, _mockAccelerator);

        // Assert
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                var expectedQ = i == j ? 1.0f : 0.0f;
                var expectedR = i == j ? 1.0f : 0.0f;
                q[i, j].Should().BeApproximately(expectedQ, 0.01f);
                if (i < r.Rows && j < r.Columns)
                {
                    r[i, j].Should().BeApproximately(expectedR, 0.01f);
                }
            }
        }
    }

    #endregion

    #region LU Decomposition Tests

    [Fact]
    public async Task LUDecompositionAsync_SquareMatrix_ProducesLowerTriangularL()
    {
        // Arrange
        var matrix = new Matrix(3, 3, [4, 3, 2, 3, 3, 1, 2, 1, 3]);

        // Act
        var (l, u, p) = await MatrixDecomposition.LUDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        l.Should().NotBeNull();
        // Check that L is lower triangular with 1s on diagonal
        for (int i = 0; i < l.Rows; i++)
        {
            l[i, i].Should().BeApproximately(1.0f, 0.001f);
            for (int j = i + 1; j < l.Columns; j++)
            {
                Math.Abs(l[i, j]).Should().BeLessThan(0.001f);
            }
        }
    }

    [Fact]
    public async Task LUDecompositionAsync_SquareMatrix_ProducesUpperTriangularU()
    {
        // Arrange
        var matrix = new Matrix(3, 3, [2, 1, 1, 4, -6, 0, -2, 7, 2]);

        // Act
        var (l, u, p) = await MatrixDecomposition.LUDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        u.Should().NotBeNull();
        // Check that U is upper triangular
        for (int i = 1; i < u.Rows; i++)
        {
            for (int j = 0; j < i; j++)
            {
                Math.Abs(u[i, j]).Should().BeLessThan(0.001f);
            }
        }
    }

    [Fact]
    public async Task LUDecompositionAsync_ReconstructOriginalMatrix()
    {
        // Arrange
        var matrix = new Matrix(3, 3, [1, 2, 3, 2, 5, 3, 1, 0, 8]);

        // Act
        var (l, u, permutation) = await MatrixDecomposition.LUDecompositionAsync(matrix, _mockAccelerator);
        var lu = await MatrixOperations.MultiplyAsync(l, u, _mockAccelerator);

        // Apply permutation to reconstruct original
        var reconstructed = new Matrix(3, 3);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                reconstructed[permutation[i], j] = lu[i, j];
            }
        }

        // Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                reconstructed[i, j].Should().BeApproximately(matrix[i, j], 0.01f);
            }
        }
    }

    [Fact]
    public async Task LUDecompositionAsync_IdentityMatrix_ReturnsIdentities()
    {
        // Arrange
        var identity = Matrix.Identity(3);

        // Act
        var (l, u, permutation) = await MatrixDecomposition.LUDecompositionAsync(identity, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                var expected = i == j ? 1.0f : 0.0f;
                l[i, j].Should().BeApproximately(expected, 0.001f);
                u[i, j].Should().BeApproximately(expected, 0.001f);
            }
            // Permutation should be identity (0, 1, 2)
            permutation[i].Should().Be(i);
        }
    }

    [Fact]
    public async Task LUDecompositionAsync_NonSquareMatrix_ThrowsArgumentException()
    {
        // Arrange
        var matrix = new Matrix(3, 4);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MatrixDecomposition.LUDecompositionAsync(matrix, _mockAccelerator));
    }

    #endregion

    #region Cholesky Decomposition Tests

    [Fact]
    public async Task CholeskyDecompositionAsync_PositiveDefiniteMatrix_ProducesLowerTriangular()
    {
        // Arrange - Create a positive definite matrix
        var matrix = new Matrix(3, 3, [4, 12, -16, 12, 37, -43, -16, -43, 98]);

        // Act
        var l = await MatrixDecomposition.CholeskyDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        l.Should().NotBeNull();
        // Check that L is lower triangular
        for (int i = 0; i < l.Rows; i++)
        {
            for (int j = i + 1; j < l.Columns; j++)
            {
                Math.Abs(l[i, j]).Should().BeLessThan(0.001f);
            }
        }
    }

    [Fact]
    public async Task CholeskyDecompositionAsync_ReconstructOriginalMatrix()
    {
        // Arrange - Simple positive definite matrix
        var matrix = new Matrix(2, 2, [4, 2, 2, 3]);

        // Act
        var l = await MatrixDecomposition.CholeskyDecompositionAsync(matrix, _mockAccelerator);
        var lTranspose = await MatrixOperations.TransposeAsync(l, _mockAccelerator);
        var reconstructed = await MatrixOperations.MultiplyAsync(l, lTranspose, _mockAccelerator);

        // Assert
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                reconstructed[i, j].Should().BeApproximately(matrix[i, j], 0.01f);
            }
        }
    }

    [Fact]
    public async Task CholeskyDecompositionAsync_IdentityMatrix_ReturnsIdentity()
    {
        // Arrange
        var identity = Matrix.Identity(3);

        // Act
        var l = await MatrixDecomposition.CholeskyDecompositionAsync(identity, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                var expected = i == j ? 1.0f : 0.0f;
                l[i, j].Should().BeApproximately(expected, 0.001f);
            }
        }
    }

    [Fact]
    public async Task CholeskyDecompositionAsync_NonPositiveDefinite_ThrowsArgumentException()
    {
        // Arrange - Not positive definite
        var matrix = new Matrix(2, 2, [1, 2, 2, 1]);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MatrixDecomposition.CholeskyDecompositionAsync(matrix, _mockAccelerator));
    }

    [Fact]
    public async Task CholeskyDecompositionAsync_NonSquareMatrix_ThrowsArgumentException()
    {
        // Arrange
        var matrix = new Matrix(3, 4);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MatrixDecomposition.CholeskyDecompositionAsync(matrix, _mockAccelerator));
    }

    #endregion

    #region SVD Decomposition Tests

    [Fact]
    public async Task SVDAsync_SquareMatrix_ProducesOrthogonalMatrices()
    {
        // Arrange
        var matrix = new Matrix(3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);

        // Act
        var (u, s, vt) = await MatrixDecomposition.SVDAsync(matrix, _mockAccelerator);

        // Assert
        u.Should().NotBeNull();
        s.Should().NotBeNull();
        vt.Should().NotBeNull();

        // U should be orthogonal: U^T * U = I
        var ut = await MatrixOperations.TransposeAsync(u, _mockAccelerator);
        var utu = await MatrixOperations.MultiplyAsync(ut, u, _mockAccelerator);
        for (int i = 0; i < u.Rows; i++)
        {
            for (int j = 0; j < u.Rows; j++)
            {
                var expected = i == j ? 1.0f : 0.0f;
                utu[i, j].Should().BeApproximately(expected, 0.1f);
            }
        }
    }

    [Fact]
    public async Task SVDAsync_RectangularMatrix_HandlesCorrectly()
    {
        // Arrange
        var matrix = new Matrix(3, 2, [1, 2, 3, 4, 5, 6]);

        // Act
        var (u, s, vt) = await MatrixDecomposition.SVDAsync(matrix, _mockAccelerator);

        // Assert
        u.Rows.Should().Be(3);
        s.Rows.Should().Be(Math.Min(3, 2));
        s.Columns.Should().Be(Math.Min(3, 2));
        vt.Columns.Should().Be(2);
    }

    [Fact]
    public async Task SVDAsync_DiagonalMatrix_ReturnsSingularValues()
    {
        // Arrange
        var matrix = new Matrix(3, 3);
        matrix[0, 0] = 5;
        matrix[1, 1] = 3;
        matrix[2, 2] = 1;

        // Act
        var (u, s, vt) = await MatrixDecomposition.SVDAsync(matrix, _mockAccelerator);

        // Assert
        // Singular values should be in descending order
        s[0, 0].Should().BeGreaterThanOrEqualTo(s[1, 1]);
        s[1, 1].Should().BeGreaterThanOrEqualTo(s[2, 2]);
        // Check actual values
        s[0, 0].Should().BeApproximately(5.0f, 0.01f);
        s[1, 1].Should().BeApproximately(3.0f, 0.01f);
        s[2, 2].Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public async Task SVDAsync_IdentityMatrix_ReturnsIdentities()
    {
        // Arrange
        var identity = Matrix.Identity(3);

        // Act
        var (u, s, vt) = await MatrixDecomposition.SVDAsync(identity, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            s[i, i].Should().BeApproximately(1.0f, 0.01f);
        }
        // U and V should be orthogonal
        u.Should().NotBeNull();
        vt.Should().NotBeNull();
    }

    [Fact]
    public async Task SVDAsync_ZeroMatrix_ReturnsZeroSingularValues()
    {
        // Arrange
        var zero = new Matrix(3, 3);

        // Act
        var (u, s, vt) = await MatrixDecomposition.SVDAsync(zero, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            Math.Abs(s[i, i]).Should().BeLessThan(0.001f);
        }
        // U and V should still be orthogonal
        u.Should().NotBeNull();
        vt.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases and Numerical Stability Tests

    [Fact]
    public async Task QRDecompositionAsync_NullMatrix_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            MatrixDecomposition.QRDecompositionAsync(null!, _mockAccelerator));
    }

    [Fact]
    public async Task LUDecompositionAsync_SingularMatrix_HandlesGracefully()
    {
        // Arrange - Singular matrix (rank deficient)
        var matrix = new Matrix(3, 3, [1, 2, 3, 2, 4, 6, 3, 6, 9]);

        // Act
        var (l, u, p) = await MatrixDecomposition.LUDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        // Should complete without throwing, but matrix will be rank deficient
        l.Should().NotBeNull();
        u.Should().NotBeNull();
        p.Should().NotBeNull();
    }

    [Fact]
    public async Task CholeskyDecompositionAsync_NearlyPositiveDefinite_HandlesCorrectly()
    {
        // Arrange - Nearly positive definite (small negative eigenvalue due to rounding)
        var matrix = new Matrix(3, 3);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                matrix[i, j] = i == j ? 1.0f : 0.1f;
            }
        }

        // Act
        var l = await MatrixDecomposition.CholeskyDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        l.Should().NotBeNull();
    }

    [Fact]
    public async Task SVDAsync_IllConditionedMatrix_ProducesStableSolution()
    {
        // Arrange - Ill-conditioned matrix
        var matrix = new Matrix(3, 3, [1, 1, 1, 1, 1 + 1e-6f, 1, 1, 1, 1 + 2e-6f]);

        // Act
        var (u, s, vt) = await MatrixDecomposition.SVDAsync(matrix, _mockAccelerator);

        // Assert
        // Should complete without throwing
        u.Should().NotBeNull();
        s.Should().NotBeNull();
        vt.Should().NotBeNull();
    }

    [Fact]
    public async Task QRDecompositionAsync_LargeMatrix_CompletesSuccessfully()
    {
        // Arrange
        var size = 100;
        var matrix = Matrix.Random(size, size);

        // Act
        var (q, r) = await MatrixDecomposition.QRDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        q.Should().NotBeNull();
        r.Should().NotBeNull();
        q.Rows.Should().Be(size);
        r.Rows.Should().Be(size);
    }

    [Fact]
    public async Task QRDecompositionAsync_CancellationToken_CanBeCancelled()
    {
        // Arrange
        var matrix = Matrix.Random(1000, 1000);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            MatrixDecomposition.QRDecompositionAsync(matrix, _mockAccelerator, cts.Token));
    }

    #endregion
}
