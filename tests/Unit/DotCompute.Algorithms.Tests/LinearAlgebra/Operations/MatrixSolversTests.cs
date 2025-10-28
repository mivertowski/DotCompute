// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.LinearAlgebra;
using DotCompute.Algorithms.LinearAlgebra.Operations;

namespace DotCompute.Algorithms.Tests.LinearAlgebra.Operations;

/// <summary>
/// Comprehensive tests for MatrixSolvers operations.
/// </summary>
public sealed class MatrixSolversTests
{
    private readonly IAccelerator _mockAccelerator;

    public MatrixSolversTests()
    {
        _mockAccelerator = Substitute.For<IAccelerator>();
        _mockAccelerator.Info.Returns(new AcceleratorInfo
        {
            DeviceType = "CPU",
            Name = "Mock CPU",
            Id = "mock-cpu-0"
        });
    }

    #region Basic Solve Tests

    [Fact]
    public async Task SolveAsync_SimpleSystem_ReturnsSolution()
    {
        // Arrange - System: 2x = 4, 3y = 9
        var a = new Matrix(2, 2, [2, 0, 0, 3]);
        var b = new Matrix(2, 1, [4, 9]);

        // Act
        var x = await MatrixSolvers.SolveAsync(a, b, _mockAccelerator);

        // Assert
        x[0, 0].Should().BeApproximately(2, 0.01f);
        x[1, 0].Should().BeApproximately(3, 0.01f);
    }

    [Fact]
    public async Task SolveAsync_IdentityMatrix_ReturnsRHS()
    {
        // Arrange
        var identity = Matrix.Identity(3);
        var b = new Matrix(3, 1, [1, 2, 3]);

        // Act
        var x = await MatrixSolvers.SolveAsync(identity, b, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            x[i, 0].Should().BeApproximately(b[i, 0], 0.001f);
        }
    }

    [Fact]
    public async Task SolveAsync_3x3System_ReturnsSolution()
    {
        // Arrange
        var a = new Matrix(3, 3, [2, 1, 1, 1, 3, 2, 1, 0, 0]);
        var b = new Matrix(3, 1, [4, 5, 6]);

        // Act
        var x = await MatrixSolvers.SolveAsync(a, b, _mockAccelerator);

        // Verify: A * x = b
        var result = await MatrixOperations.MultiplyAsync(a, x, _mockAccelerator);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            result[i, 0].Should().BeApproximately(b[i, 0], 0.1f);
        }
    }

    [Fact]
    public async Task SolveAsync_NonSquareMatrix_ThrowsArgumentException()
    {
        // Arrange
        var a = new Matrix(3, 4);
        var b = new Matrix(3, 1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MatrixSolvers.SolveAsync(a, b, _mockAccelerator));
    }

    [Fact]
    public async Task SolveAsync_IncompatibleDimensions_ThrowsArgumentException()
    {
        // Arrange
        var a = new Matrix(3, 3);
        var b = new Matrix(4, 1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MatrixSolvers.SolveAsync(a, b, _mockAccelerator));
    }

    [Fact]
    public async Task SolveAsync_NullMatrix_ThrowsArgumentNullException()
    {
        // Arrange
        var b = new Matrix(3, 1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            MatrixSolvers.SolveAsync(null!, b, _mockAccelerator));
    }

    #endregion

    #region Iterative Refinement Tests

    [Fact]
    public async Task SolveWithRefinementAsync_SimpleSystem_ImprovesAccuracy()
    {
        // Arrange
        var a = new Matrix(2, 2, [2, 1, 1, 2]);
        var b = new Matrix(2, 1, [1, 1]);

        // Act
        var x = await MatrixSolvers.SolveWithRefinementAsync(a, b, _mockAccelerator);

        // Verify solution
        var result = await MatrixOperations.MultiplyAsync(a, x, _mockAccelerator);

        // Assert
        for (int i = 0; i < 2; i++)
        {
            result[i, 0].Should().BeApproximately(b[i, 0], 0.001f);
        }
    }

    [Fact]
    public async Task SolveWithRefinementAsync_IllConditioned_ConvergesWithRefinement()
    {
        // Arrange - Slightly ill-conditioned system
        var a = new Matrix(3, 3, [10, 7, 8, 7, 5, 6, 8, 6, 10]);
        var b = new Matrix(3, 1, [32, 23, 33]);

        // Act
        var x = await MatrixSolvers.SolveWithRefinementAsync(a, b, _mockAccelerator, maxRefinements: 10);

        // Verify
        var result = await MatrixOperations.MultiplyAsync(a, x, _mockAccelerator);

        // Assert - Should be accurate after refinement
        for (int i = 0; i < 3; i++)
        {
            result[i, 0].Should().BeApproximately(b[i, 0], 0.01f);
        }
    }

    [Fact]
    public async Task SolveWithRefinementAsync_CustomTolerance_RespectsParameter()
    {
        // Arrange
        var a = new Matrix(2, 2, [2, 1, 1, 2]);
        var b = new Matrix(2, 1, [3, 3]);

        // Act
        var x = await MatrixSolvers.SolveWithRefinementAsync(
            a, b, _mockAccelerator, maxRefinements: 2, tolerance: 1e-6f);

        // Assert - Should complete without error
        x.Should().NotBeNull();
    }

    [Fact]
    public async Task SolveWithRefinementAsync_MaxRefinements_LimitsIterations()
    {
        // Arrange
        var a = new Matrix(2, 2, [1, 1, 1, 1.00001f]);
        var b = new Matrix(2, 1, [2, 2]);

        // Act
        var x = await MatrixSolvers.SolveWithRefinementAsync(
            a, b, _mockAccelerator, maxRefinements: 1);

        // Assert
        x.Should().NotBeNull();
    }

    #endregion

    // TODO: Re-enable when missing MatrixSolvers methods are implemented
//     #region Least Squares Tests
// 
//     [Fact]
//     public async Task SolveLeastSquaresAsync_OverdeterminedSystem_FindsBestFit()
//     {
//         // Arrange - More equations than unknowns
//         var a = new Matrix(4, 2, [1, 1, 1, 2, 1, 3, 1, 4]);
//         var b = new Matrix(4, 1, [1, 2, 2, 3]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveLeastSquaresAsync(a, b, _mockAccelerator);
// 
//         // Assert
//         x.Should().NotBeNull();
//         x.Rows.Should().Be(2);
//         x.Columns.Should().Be(1);
//     }
// 
//     [Fact]
//     public async Task SolveLeastSquaresAsync_ExactSystem_ReturnsPreciseSolution()
//     {
//         // Arrange - Exactly determined system
//         var a = new Matrix(2, 2, [2, 1, 1, 2]);
//         var b = new Matrix(2, 1, [3, 3]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveLeastSquaresAsync(a, b, _mockAccelerator);
// 
//         // Verify
//         var result = await MatrixOperations.MultiplyAsync(a, x, _mockAccelerator);
// 
//         // Assert
//         for (int i = 0; i < 2; i++)
//         {
//             result[i, 0].Should().BeApproximately(b[i, 0], 0.1f);
//         }
//     }
// 
//     [Fact]
//     public async Task SolveLeastSquaresAsync_RankDeficient_HandlesGracefully()
//     {
//         // Arrange - Rank deficient matrix
//         var a = new Matrix(3, 2, [1, 2, 2, 4, 3, 6]);
//         var b = new Matrix(3, 1, [1, 2, 3]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveLeastSquaresAsync(a, b, _mockAccelerator);
// 
//         // Assert - Should complete without error
//         x.Should().NotBeNull();
//     }
// 
//     #endregion

//     #region Triangular Solvers Tests
// 
//     [Fact]
//     public async Task SolveUpperTriangularAsync_SimpleSystem_ReturnsSolution()
//     {
//         // Arrange
//         var u = new Matrix(3, 3, [2, 1, 1, 0, 3, 2, 0, 0, 4]);
//         var b = new Matrix(3, 1, [4, 5, 4]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveUpperTriangularAsync(u, b, _mockAccelerator);
// 
//         // Verify
//         var result = await MatrixOperations.MultiplyAsync(u, x, _mockAccelerator);
// 
//         // Assert
//         for (int i = 0; i < 3; i++)
//         {
//             result[i, 0].Should().BeApproximately(b[i, 0], 0.01f);
//         }
//     }
// 
//     [Fact]
//     public async Task SolveLowerTriangularAsync_SimpleSystem_ReturnsSolution()
//     {
//         // Arrange
//         var l = new Matrix(3, 3, [2, 0, 0, 1, 3, 0, 1, 2, 4]);
//         var b = new Matrix(3, 1, [2, 5, 10]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveLowerTriangularAsync(l, b, _mockAccelerator);
// 
//         // Verify
//         var result = await MatrixOperations.MultiplyAsync(l, x, _mockAccelerator);
// 
//         // Assert
//         for (int i = 0; i < 3; i++)
//         {
//             result[i, 0].Should().BeApproximately(b[i, 0], 0.01f);
//         }
//     }
// 
//     [Fact]
//     public async Task SolveUpperTriangularAsync_ZeroDiagonal_ThrowsArgumentException()
//     {
//         // Arrange - Zero on diagonal
//         var u = new Matrix(2, 2, [1, 1, 0, 0]);
//         var b = new Matrix(2, 1, [1, 1]);
// 
//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentException>(() =>
//             MatrixSolvers.SolveUpperTriangularAsync(u, b, _mockAccelerator));
//     }
// 
//     #endregion
// 
//     #region Specialized Solvers Tests
// 
//     [Fact]
//     public async Task SolveCholeskyAsync_PositiveDefinite_ReturnsSolution()
//     {
//         // Arrange - Positive definite system
//         var a = new Matrix(2, 2, [4, 2, 2, 3]);
//         var b = new Matrix(2, 1, [2, 1]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveCholeskyAsync(a, b, _mockAccelerator);
// 
//         // Verify
//         var result = await MatrixOperations.MultiplyAsync(a, x, _mockAccelerator);
// 
//         // Assert
//         for (int i = 0; i < 2; i++)
//         {
//             result[i, 0].Should().BeApproximately(b[i, 0], 0.1f);
//         }
//     }
// 
//     [Fact]
//     public async Task SolveTridiagonalAsync_TridiagonalSystem_ReturnsSolution()
//     {
//         // Arrange - Tridiagonal matrix
//         var main = new[] { 2f, 2f, 2f };
//         var lower = new[] { 1f, 1f };
//         var upper = new[] { 1f, 1f };
//         var b = new Matrix(3, 1, [1, 2, 3]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveTridiagonalAsync(
//             lower, main, upper, b, _mockAccelerator);
// 
//         // Assert
//         x.Should().NotBeNull();
//         x.Rows.Should().Be(3);
//     }
// 
//     [Fact]
//     public async Task SolveBandedAsync_BandedMatrix_ReturnsSolution()
//     {
//         // Arrange - Banded matrix (bandwidth = 1)
//         var a = new Matrix(4, 4, [
//             2, 1, 0, 0,
//             1, 2, 1, 0,
//             0, 1, 2, 1,
//             0, 0, 1, 2
//         ]);
//         var b = new Matrix(4, 1, [1, 2, 2, 1]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveBandedAsync(a, b, bandwidth: 1, _mockAccelerator);
// 
//         // Assert
//         x.Should().NotBeNull();
//     }
// 
//     #endregion
// 
//     #region Iterative Solvers Tests
// 
//     [Fact]
//     public async Task SolveJacobiAsync_DiagonallyDominant_Converges()
//     {
//         // Arrange - Diagonally dominant
//         var a = new Matrix(3, 3, [10, 1, 1, 1, 10, 1, 1, 1, 10]);
//         var b = new Matrix(3, 1, [12, 12, 12]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveJacobiAsync(
//             a, b, _mockAccelerator, maxIterations: 100, tolerance: 1e-6f);
// 
//         // Verify
//         var result = await MatrixOperations.MultiplyAsync(a, x, _mockAccelerator);
// 
//         // Assert
//         for (int i = 0; i < 3; i++)
//         {
//             result[i, 0].Should().BeApproximately(b[i, 0], 0.1f);
//         }
//     }
// 
//     [Fact]
//     public async Task SolveGaussSeidelAsync_DiagonallyDominant_ConvergesFaster()
//     {
//         // Arrange
//         var a = new Matrix(3, 3, [10, 1, 1, 1, 10, 1, 1, 1, 10]);
//         var b = new Matrix(3, 1, [12, 12, 12]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveGaussSeidelAsync(
//             a, b, _mockAccelerator, maxIterations: 50, tolerance: 1e-6f);
// 
//         // Assert
//         x.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task SolveConjugateGradientAsync_SymmetricPositiveDefinite_Converges()
//     {
//         // Arrange - Symmetric positive definite
//         var a = new Matrix(3, 3, [4, 1, 0, 1, 4, 1, 0, 1, 4]);
//         var b = new Matrix(3, 1, [1, 2, 3]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveConjugateGradientAsync(
//             a, b, _mockAccelerator, maxIterations: 100, tolerance: 1e-6f);
// 
//         // Verify
//         var result = await MatrixOperations.MultiplyAsync(a, x, _mockAccelerator);
// 
//         // Assert
//         for (int i = 0; i < 3; i++)
//         {
//             result[i, 0].Should().BeApproximately(b[i, 0], 0.1f);
//         }
//     }
// 
//     #endregion
// 
//     #region Edge Cases and Numerical Stability Tests
// 
//     [Fact]
//     public async Task SolveAsync_SingleVariable_ReturnsScalarSolution()
//     {
//         // Arrange
//         var a = new Matrix(1, 1, [5]);
//         var b = new Matrix(1, 1, [15]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveAsync(a, b, _mockAccelerator);
// 
//         // Assert
//         x[0, 0].Should().BeApproximately(3, 0.001f);
//     }
// 
//     // TODO: Re-enable when Matrix.Random is implemented
//     // [Fact]
//     // public async Task SolveAsync_LargeSystem_CompletesSuccessfully()
//     // {
//     //     // Arrange
//     //     var size = 100;
//     //     var a = Matrix.Identity(size);
//     //     var b = Matrix.Random(size, 1);
//     //
//     //     // Act
//     //     var x = await MatrixSolvers.SolveAsync(a, b, _mockAccelerator);
//     //
//     //     // Assert
//     //     x.Should().NotBeNull();
//     //     x.Rows.Should().Be(size);
//     // }
// 
//     // TODO: Re-enable when Matrix.Random is implemented
//     // [Fact]
//     // public void SolveAsync_CancellationToken_CanBeCancelled()
//     // {
//     //     // Arrange
//     //     var a = Matrix.Random(1000, 1000);
//     //     var b = Matrix.Random(1000, 1);
//     //     var cts = new CancellationTokenSource();
//     //     cts.Cancel();
//     //
//     //     // Act & Assert
//     //     Assert.ThrowsAsync<TaskCanceledException>(() =>
//     //         MatrixSolvers.SolveAsync(a, b, _mockAccelerator, cts.Token));
//     // }
// 
//     [Fact]
//     public async Task SolveWithRefinementAsync_AlreadyAccurate_ConvergesQuickly()
//     {
//         // Arrange - Well-conditioned system
//         var a = Matrix.Identity(3);
//         var b = new Matrix(3, 1, [1, 2, 3]);
// 
//         // Act
//         var x = await MatrixSolvers.SolveWithRefinementAsync(
//             a, b, _mockAccelerator, maxRefinements: 10, tolerance: 1e-10f);
// 
//         // Assert
//         for (int i = 0; i < 3; i++)
//         {
//             x[i, 0].Should().BeApproximately(b[i, 0], 0.0001f);
//         }
//     }
// 
//     #endregion
}
