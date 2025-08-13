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

    // Logger message delegates for structured logging
    private static readonly Action<ILogger, int, int, Exception?> LogMatrixDimensions =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogMatrixDimensions)),
            "Matrix A ({Rows}x{Columns}):");

    private static readonly Action<ILogger, int, int, Exception?> LogQMatrixDimensions =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(2, nameof(LogQMatrixDimensions)),
            "Q matrix ({Rows}x{Columns}):");

    private static readonly Action<ILogger, int, int, Exception?> LogRMatrixDimensions =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(3, nameof(LogRMatrixDimensions)),
            "R matrix ({Rows}x{Columns}):");

    private static readonly Action<ILogger, int, int, Exception?> LogUMatrixDimensions =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(4, nameof(LogUMatrixDimensions)),
            "U matrix ({Rows}x{Columns}):");

    private static readonly Action<ILogger, int, int, Exception?> LogVTMatrixDimensions =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(5, nameof(LogVTMatrixDimensions)),
            "V^T matrix ({Rows}x{Columns}):");

    private static readonly Action<ILogger, int, float, Exception?> LogSingularValue =
        LoggerMessage.Define<int, float>(
            LogLevel.Information,
            new EventId(6, nameof(LogSingularValue)),
            "σ[{Index}] = {Value:F6}");

    private static readonly Action<ILogger, float, Exception?> LogConditionNumber =
        LoggerMessage.Define<float>(
            LogLevel.Information,
            new EventId(7, nameof(LogConditionNumber)),
            "Condition number: {ConditionNumber:F6}");

    private static readonly Action<ILogger, int, int, Exception?> LogPositiveDefiniteMatrix =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(8, nameof(LogPositiveDefiniteMatrix)),
            "Positive definite matrix A ({Rows}x{Columns}):");

    private static readonly Action<ILogger, int, int, Exception?> LogCholeskyFactor =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(9, nameof(LogCholeskyFactor)),
            "Cholesky factor L ({Rows}x{Columns}):");

    private static readonly Action<ILogger, string, Exception?> LogCholeskyError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(10, nameof(LogCholeskyError)),
            "Matrix is not positive definite: {ErrorMessage}");

    private static readonly Action<ILogger, int, int, Exception?> LogSymmetricMatrix =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(11, nameof(LogSymmetricMatrix)),
            "Symmetric matrix A ({Rows}x{Columns}):");

    private static readonly Action<ILogger, int, float, Exception?> LogEigenvalue =
        LoggerMessage.Define<int, float>(
            LogLevel.Information,
            new EventId(12, nameof(LogEigenvalue)),
            "λ[{Index}] = {Value:F6}");

    private static readonly Action<ILogger, int, int, Exception?> LogEigenvectors =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(13, nameof(LogEigenvectors)),
            "Eigenvectors ({Rows}x{Columns}):");

    private static readonly Action<ILogger, int, int, Exception?> LogCoefficientMatrix =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(14, nameof(LogCoefficientMatrix)),
            "Coefficient matrix A ({Rows}x{Columns}):");

    private static readonly Action<ILogger, int, int, Exception?> LogRightHandSide =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(15, nameof(LogRightHandSide)),
            "Right-hand side b ({Rows}x{Columns}):");

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

        LogMatrixDimensions(_logger, A.Rows, A.Columns, null);
        _logger.LogInformation(A.ToString());

        // Perform QR decomposition
        var (Q, R) = await MatrixMath.QRDecompositionAsync(A, _accelerator);

        LogQMatrixDimensions(_logger, Q.Rows, Q.Columns, null);
        _logger.LogInformation(Q.ToString());

        LogRMatrixDimensions(_logger, R.Rows, R.Columns, null);
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

        LogMatrixDimensions(_logger, A.Rows, A.Columns, null);
        _logger.LogInformation(A.ToString());

        // Perform SVD
        var (U, S, VT) = await MatrixMath.SVDAsync(A, _accelerator);

        LogUMatrixDimensions(_logger, U.Rows, U.Columns, null);
        _logger.LogInformation(U.ToString());

        _logger.LogInformation("Singular values:");
        for (int i = 0; i < S.Rows; i++)
        {
            LogSingularValue(_logger, i, S[i, i], null);
        }

        LogVTMatrixDimensions(_logger, VT.Rows, VT.Columns, null);
        _logger.LogInformation(VT.ToString());

        // Compute condition number
        var conditionNumber = await MatrixMath.ConditionNumberAsync(A, _accelerator);
        LogConditionNumber(_logger, conditionNumber, null);
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

        LogPositiveDefiniteMatrix(_logger, A.Rows, A.Columns, null);
        _logger.LogInformation(A.ToString());

        try
        {
            // Perform Cholesky decomposition
            var L = await MatrixMath.CholeskyDecompositionAsync(A, _accelerator);

            LogCholeskyFactor(_logger, L.Rows, L.Columns, null);
            _logger.LogInformation(L.ToString());

            // Verify L*L^T = A
            var LT = await MatrixMath.TransposeAsync(L, _accelerator);
            var LLT = await MatrixMath.MultiplyAsync(L, LT, _accelerator);
            
            _logger.LogInformation("Verification L*L^T:");
            _logger.LogInformation(LLT.ToString());
        }
        catch (InvalidOperationException ex)
        {
            LogCholeskyError(_logger, ex.Message, null);
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

        LogSymmetricMatrix(_logger, A.Rows, A.Columns, null);
        _logger.LogInformation(A.ToString());

        // Perform eigenvalue decomposition
        var (eigenvalues, eigenvectors) = await MatrixMath.EigenDecompositionAsync(
            A, _accelerator, maxIterations: 1000, tolerance: 1e-10f);

        _logger.LogInformation("Eigenvalues:");
        for (int i = 0; i < eigenvalues.Rows; i++)
        {
            LogEigenvalue(_logger, i, eigenvalues[i, 0], null);
        }

        LogEigenvectors(_logger, eigenvectors.Rows, eigenvectors.Columns, null);
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

        LogCoefficientMatrix(_logger, A.Rows, A.Columns, null);
        _logger.LogInformation(A.ToString());

        LogRightHandSide(_logger, b.Rows, b.Columns, null);
        _logger.LogInformation(b.ToString());

        // Check condition number
        var conditionNumber = await MatrixMath.ConditionNumberAsync(A, _accelerator);
        LogConditionNumber(_logger, conditionNumber, null);

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