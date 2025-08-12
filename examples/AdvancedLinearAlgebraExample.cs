// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Types;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Examples;

/// <summary>
/// Example demonstrating advanced linear algebra operations in DotCompute.
/// </summary>
public class AdvancedLinearAlgebraExample
{
    private readonly IAccelerator _accelerator;
    private readonly ILogger<AdvancedLinearAlgebraExample> _logger;

    public AdvancedLinearAlgebraExample(IAccelerator accelerator, ILogger<AdvancedLinearAlgebraExample> logger)
    {
        _accelerator = accelerator;
        _logger = logger;
    }

    /// <summary>
    /// Demonstrates QR decomposition for solving least squares problems.
    /// </summary>
    public async Task QRDecompositionExample()
    {
        _logger.LogInformation("=== QR Decomposition Example ===");

        // Create an overdetermined system (more rows than columns)
        var A = Matrix.FromArray(new float[,]
        {
            { 1, 1 },
            { 1, 2 },
            { 1, 3 },
            { 1, 4 }
        });

        _logger.LogInformation($"Matrix A ({A.Rows}x{A.Columns}):");
        _logger.LogInformation(A.ToString());

        // Perform QR decomposition
        var (Q, R) = await MatrixMath.QRDecompositionAsync(A, _accelerator);

        _logger.LogInformation($"Q matrix ({Q.Rows}x{Q.Columns}):");
        _logger.LogInformation(Q.ToString());

        _logger.LogInformation($"R matrix ({R.Rows}x{R.Columns}):");
        _logger.LogInformation(R.ToString());

        // Verify Q*R = A
        var QR = await MatrixMath.MultiplyAsync(Q, R, _accelerator);
        _logger.LogInformation("Verification Q*R:");
        _logger.LogInformation(QR.ToString());
    }

    /// <summary>
    /// Demonstrates SVD for matrix analysis and low-rank approximation.
    /// </summary>
    public async Task SVDExample()
    {
        _logger.LogInformation("=== SVD Example ===");

        // Create a matrix with known structure
        var A = Matrix.FromArray(new float[,]
        {
            { 4, 0 },
            { 3, -5 }
        });

        _logger.LogInformation($"Matrix A ({A.Rows}x{A.Columns}):");
        _logger.LogInformation(A.ToString());

        // Perform SVD
        var (U, S, VT) = await MatrixMath.SVDAsync(A, _accelerator);

        _logger.LogInformation($"U matrix ({U.Rows}x{U.Columns}):");
        _logger.LogInformation(U.ToString());

        _logger.LogInformation($"Singular values:");
        for (int i = 0; i < S.Rows; i++)
        {
            _logger.LogInformation($"σ[{i}] = {S[i, i]:F6}");
        }

        _logger.LogInformation($"V^T matrix ({VT.Rows}x{VT.Columns}):");
        _logger.LogInformation(VT.ToString());

        // Compute condition number
        var conditionNumber = await MatrixMath.ConditionNumberAsync(A, _accelerator);
        _logger.LogInformation($"Condition number: {conditionNumber:F6}");
    }

    /// <summary>
    /// Demonstrates Cholesky decomposition for positive definite systems.
    /// </summary>
    public async Task CholeskyExample()
    {
        _logger.LogInformation("=== Cholesky Decomposition Example ===");

        // Create a positive definite matrix
        var A = Matrix.FromArray(new float[,]
        {
            { 4, 2, 1 },
            { 2, 3, 0.5f },
            { 1, 0.5f, 2 }
        });

        _logger.LogInformation($"Positive definite matrix A ({A.Rows}x{A.Columns}):");
        _logger.LogInformation(A.ToString());

        try
        {
            // Perform Cholesky decomposition
            var L = await MatrixMath.CholeskyDecompositionAsync(A, _accelerator);

            _logger.LogInformation($"Cholesky factor L ({L.Rows}x{L.Columns}):");
            _logger.LogInformation(L.ToString());

            // Verify L*L^T = A
            var LT = await MatrixMath.TransposeAsync(L, _accelerator);
            var LLT = await MatrixMath.MultiplyAsync(L, LT, _accelerator);
            
            _logger.LogInformation("Verification L*L^T:");
            _logger.LogInformation(LLT.ToString());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError($"Matrix is not positive definite: {ex.Message}");
        }
    }

    /// <summary>
    /// Demonstrates eigenvalue decomposition for symmetric matrices.
    /// </summary>
    public async Task EigenDecompositionExample()
    {
        _logger.LogInformation("=== Eigenvalue Decomposition Example ===");

        // Create a symmetric matrix
        var A = Matrix.FromArray(new float[,]
        {
            { 3, 1 },
            { 1, 3 }
        });

        _logger.LogInformation($"Symmetric matrix A ({A.Rows}x{A.Columns}):");
        _logger.LogInformation(A.ToString());

        // Perform eigenvalue decomposition
        var (eigenvalues, eigenvectors) = await MatrixMath.EigenDecompositionAsync(
            A, _accelerator, maxIterations: 1000, tolerance: 1e-10f);

        _logger.LogInformation("Eigenvalues:");
        for (int i = 0; i < eigenvalues.Rows; i++)
        {
            _logger.LogInformation($"λ[{i}] = {eigenvalues[i, 0]:F6}");
        }

        _logger.LogInformation($"Eigenvectors ({eigenvectors.Rows}x{eigenvectors.Columns}):");
        _logger.LogInformation(eigenvectors.ToString());
    }

    /// <summary>
    /// Demonstrates solving a linear system with iterative refinement.
    /// </summary>
    public async Task LinearSystemSolveExample()
    {
        _logger.LogInformation("=== Linear System Solve Example ===");

        // Create a linear system Ax = b
        var A = Matrix.FromArray(new float[,]
        {
            { 2, 1, 1 },
            { 1, 3, 2 },
            { 1, 0, 0 }
        });

        var b = Matrix.FromArray(new float[,]
        {
            { 4 },
            { 5 },
            { 6 }
        });

        _logger.LogInformation($"Coefficient matrix A ({A.Rows}x{A.Columns}):");
        _logger.LogInformation(A.ToString());

        _logger.LogInformation($"Right-hand side b ({b.Rows}x{b.Columns}):");
        _logger.LogInformation(b.ToString());

        // Check condition number
        var conditionNumber = await MatrixMath.ConditionNumberAsync(A, _accelerator);
        _logger.LogInformation($"Condition number: {conditionNumber:F6}");

        // Solve using basic method
        var x1 = await MatrixMath.SolveAsync(A, b, _accelerator);
        _logger.LogInformation("Basic solution:");
        _logger.LogInformation(x1.ToString());

        // Solve using iterative refinement
        var x2 = await MatrixMath.SolveWithRefinementAsync(
            A, b, _accelerator, maxRefinements: 5, tolerance: 1e-12f);
        
        _logger.LogInformation("Refined solution:");
        _logger.LogInformation(x2.ToString());

        // Verify solution
        var residual = await MatrixMath.MultiplyAsync(A, x2, _accelerator);
        var error = await MatrixMath.SubtractAsync(residual, b, _accelerator);
        
        _logger.LogInformation("Residual (should be close to zero):");
        _logger.LogInformation(error.ToString());
    }

    /// <summary>
    /// Runs all examples.
    /// </summary>
    public async Task RunAllExamples()
    {
        _logger.LogInformation("Running Advanced Linear Algebra Examples");
        _logger.LogInformation("=====================================");

        await QRDecompositionExample();
        await SVDExample();
        await CholeskyExample();
        await EigenDecompositionExample();
        await LinearSystemSolveExample();

        _logger.LogInformation("All examples completed successfully!");
    }
}