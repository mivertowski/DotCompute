// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Types;
using DotCompute.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Tests.Unit.LinearAlgebra;

/// <summary>
/// Tests for advanced matrix algorithms including QR, SVD, Cholesky, and eigenvalue decomposition.
/// </summary>
public class AdvancedMatrixMathTests
{
    private readonly IAccelerator _mockAccelerator;

    public AdvancedMatrixMathTests()
    {
        _mockAccelerator = new MockAccelerator();
    }

    [Fact]
    public async Task QRDecomposition_SimpleMatrix_ShouldReturnCorrectDecomposition()
    {
        // Arrange
        var matrix = Matrix.FromArray(new float[,]
        {
            { 1, 1 },
            { 1, 0 },
            { 0, 1 }
        });

        // Act
        var (q, r) = await MatrixMath.QRDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        q.Should().NotBeNull();
        r.Should().NotBeNull();
        
        // Verify Q is orthogonal (Q^T * Q = I)
        var qTranspose = await MatrixMath.TransposeAsync(q, _mockAccelerator);
        var qtq = await MatrixMath.MultiplyAsync(qTranspose, q, _mockAccelerator);
        
        // Check diagonal elements are close to 1
        for (int i = 0; i < qtq.Rows; i++)
        {
            qtq[i, i].Should().BeApproximately(1.0f, 1e-5f);
        }
        
        // Verify QR = A
        var reconstructed = await MatrixMath.MultiplyAsync(q, r, _mockAccelerator);
        for (int i = 0; i < matrix.Rows; i++)
        {
            for (int j = 0; j < matrix.Columns; j++)
            {
                reconstructed[i, j].Should().BeApproximately(matrix[i, j], 1e-5f);
            }
        }
    }

    [Fact]
    public async Task CholeskyDecomposition_PositiveDefiniteMatrix_ShouldReturnCorrectDecomposition()
    {
        // Arrange - Create a positive definite matrix
        var matrix = Matrix.FromArray(new float[,]
        {
            { 4, 2, 1 },
            { 2, 3, 0.5f },
            { 1, 0.5f, 2 }
        });

        // Act
        var l = await MatrixMath.CholeskyDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        l.Should().NotBeNull();
        
        // Verify L is lower triangular
        for (int i = 0; i < l.Rows; i++)
        {
            for (int j = i + 1; j < l.Columns; j++)
            {
                l[i, j].Should().BeApproximately(0, 1e-6f);
            }
        }
        
        // Verify L * L^T = A
        var lTranspose = await MatrixMath.TransposeAsync(l, _mockAccelerator);
        var reconstructed = await MatrixMath.MultiplyAsync(l, lTranspose, _mockAccelerator);
        
        for (int i = 0; i < matrix.Rows; i++)
        {
            for (int j = 0; j < matrix.Columns; j++)
            {
                reconstructed[i, j].Should().BeApproximately(matrix[i, j], 1e-5f);
            }
        }
    }

    [Fact]
    public async Task SVDAsync_SimpleMatrix_ShouldReturnCorrectDecomposition()
    {
        // Arrange
        var matrix = Matrix.FromArray(new float[,]
        {
            { 3, 2, 2 },
            { 2, 3, -2 }
        });

        // Act
        var (u, s, vt) = await MatrixMath.SVDAsync(matrix, _mockAccelerator);

        // Assert
        u.Should().NotBeNull();
        s.Should().NotBeNull();
        vt.Should().NotBeNull();
        
        // Verify dimensions
        u.Rows.Should().Be(matrix.Rows);
        u.Columns.Should().Be(matrix.Rows);
        s.Rows.Should().Be(Math.Min(matrix.Rows, matrix.Columns));
        s.Columns.Should().Be(Math.Min(matrix.Rows, matrix.Columns));
        vt.Rows.Should().Be(matrix.Columns);
        vt.Columns.Should().Be(matrix.Columns);
        
        // Verify singular values are non-negative and sorted
        for (int i = 0; i < s.Rows; i++)
        {
            s[i, i].Should().BeGreaterOrEqualTo(0);
            if (i > 0)
            {
                s[i - 1, i - 1].Should().BeGreaterOrEqualTo(s[i, i]);
            }
        }
    }

    [Fact]
    public async Task EigenDecomposition_SymmetricMatrix_ShouldReturnRealEigenvalues()
    {
        // Arrange - Create a simple symmetric matrix
        var matrix = Matrix.FromArray(new float[,]
        {
            { 2, 1 },
            { 1, 2 }
        });

        // Act
        var (eigenvalues, eigenvectors) = await MatrixMath.EigenDecompositionAsync(matrix, _mockAccelerator);

        // Assert
        eigenvalues.Should().NotBeNull();
        eigenvectors.Should().NotBeNull();
        
        // Verify dimensions
        eigenvalues.Rows.Should().Be(matrix.Rows);
        eigenvalues.Columns.Should().Be(1);
        eigenvectors.Rows.Should().Be(matrix.Rows);
        eigenvectors.Columns.Should().Be(matrix.Columns);
        
        // For this specific matrix, eigenvalues should be 3 and 1
        var sortedEigenvalues = new List<float>();
        for (int i = 0; i < eigenvalues.Rows; i++)
        {
            sortedEigenvalues.Add(eigenvalues[i, 0]);
        }
        sortedEigenvalues.Sort();
        sortedEigenvalues.Reverse();
        
        sortedEigenvalues[0].Should().BeApproximately(3.0f, 1e-3f);
        sortedEigenvalues[1].Should().BeApproximately(1.0f, 1e-3f);
    }

    [Fact]
    public async Task ConditionNumber_WellConditionedMatrix_ShouldReturnSmallValue()
    {
        // Arrange - Identity matrix is perfectly conditioned
        var matrix = Matrix.Identity(3);

        // Act
        var conditionNumber = await MatrixMath.ConditionNumberAsync(matrix, _mockAccelerator);

        // Assert
        conditionNumber.Should().BeApproximately(1.0f, 1e-5f);
    }

    [Fact]
    public async Task SolveWithRefinement_LinearSystem_ShouldImproveAccuracy()
    {
        // Arrange
        var a = Matrix.FromArray(new float[,]
        {
            { 2, 1 },
            { 1, 3 }
        });
        var b = Matrix.FromArray(new float[,] { { 3 }, { 4 } });

        // Act
        var x = await MatrixMath.SolveWithRefinementAsync(a, b, _mockAccelerator);

        // Assert
        x.Should().NotBeNull();
        x.Rows.Should().Be(2);
        x.Columns.Should().Be(1);
        
        // Verify Ax ≈ b
        var result = await MatrixMath.MultiplyAsync(a, x, _mockAccelerator);
        for (int i = 0; i < b.Rows; i++)
        {
            result[i, 0].Should().BeApproximately(b[i, 0], 1e-6f);
        }
    }

    [Fact]
    public void CholeskyDecomposition_NotPositiveDefinite_ShouldThrowException()
    {
        // Arrange - Create a non-positive definite matrix
        var matrix = Matrix.FromArray(new float[,]
        {
            { 1, 2 },
            { 2, 1 }
        });

        // Act & Assert
        var act = async () => await MatrixMath.CholeskyDecompositionAsync(matrix, _mockAccelerator);
        act.Should().ThrowAsync<InvalidOperationException>()
           .WithMessage("Matrix is not positive definite.");
    }

    /// <summary>
    /// Mock accelerator for testing that always reports as CPU.
    /// </summary>
    private class MockAccelerator : IAccelerator
    {
        public AcceleratorInfo Info => new(AcceleratorType.CPU, "Mock CPU", "1.0", 1024 * 1024);

        public IMemoryManager Memory => new MockMemoryManager();

        public ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Mock memory manager for testing.
    /// </summary>
    private class MockMemoryManager : IMemoryManager
    {
        public ValueTask<IMemoryBuffer> AllocateAsync(long sizeInBytes, MemoryOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<MemoryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new MemoryStatistics
            {
                TotalMemory = 1024 * 1024,
                FreeMemory = 512 * 1024,
                UsedMemory = 512 * 1024
            });
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}