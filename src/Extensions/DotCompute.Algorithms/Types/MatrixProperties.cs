// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.LinearAlgebra;

namespace DotCompute.Algorithms.Types;


/// <summary>
/// Properties and characteristics of matrices for optimization decisions.
/// </summary>
public class MatrixProperties
{
    /// <summary>
    /// Gets or sets the number of rows.
    /// </summary>
    public int Rows { get; set; }

    /// <summary>
    /// Gets or sets the number of columns.
    /// </summary>
    public int Columns { get; set; }

    /// <summary>
    /// Gets or sets whether the matrix is square.
    /// </summary>
    public bool IsSquare { get; set; }

    /// <summary>
    /// Gets or sets whether the matrix is symmetric.
    /// </summary>
    public bool IsSymmetric { get; set; }

    /// <summary>
    /// Gets or sets whether the matrix is diagonal.
    /// </summary>
    public bool IsDiagonal { get; set; }

    /// <summary>
    /// Gets or sets whether the matrix is sparse.
    /// </summary>
    public bool IsSparse { get; set; }

    /// <summary>
    /// Gets or sets the sparsity ratio (0.0 = dense, 1.0 = completely sparse).
    /// </summary>
    public double SparsityRatio { get; set; }

    /// <summary>
    /// Gets or sets the condition number estimate.
    /// </summary>
    public double ConditionNumber { get; set; }

    /// <summary>
    /// Gets or sets the determinant (if computed).
    /// </summary>
    public double? Determinant { get; set; }

    /// <summary>
    /// Gets or sets the matrix rank (if computed).
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>
    /// Analyzes a matrix and returns its properties.
    /// </summary>
    /// <param name="matrix">The matrix to analyze.</param>
    /// <returns>Matrix properties.</returns>
    public static MatrixProperties Analyze(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        var properties = new MatrixProperties
        {
            Rows = matrix.Rows,
            Columns = matrix.Columns,
            IsSquare = matrix.IsSquare
        };

        // Basic property analysis
        if (matrix.IsSquare)
        {
            properties.IsSymmetric = CheckSymmetry(matrix);
            properties.IsDiagonal = CheckDiagonal(matrix);
        }

        properties.IsSparse = CheckSparsity(matrix, out var sparsityRatio);
        properties.SparsityRatio = sparsityRatio;

        return properties;
    }

    /// <summary>
    /// Analyzes a matrix and returns its properties (compatibility overload).
    /// </summary>
    /// <param name="matrix">The matrix to analyze.</param>
    /// <returns>Matrix properties.</returns>
    public static MatrixProperties AnalyzeLinearAlgebra(DotCompute.Algorithms.LinearAlgebra.Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        var properties = new MatrixProperties
        {
            Rows = matrix.Rows,
            Columns = matrix.Columns,
            IsSquare = matrix.IsSquare
        };

        // Basic property analysis
        if (matrix.IsSquare)
        {
            properties.IsSymmetric = CheckSymmetryLinearAlgebra(matrix);
            properties.IsDiagonal = CheckDiagonalLinearAlgebra(matrix);
        }

        properties.IsSparse = CheckSparsityLinearAlgebra(matrix, out var sparsityRatio);
        properties.SparsityRatio = sparsityRatio;

        return properties;
    }

    /// <summary>
    /// Gets the recommended algorithm based on matrix properties.
    /// </summary>
    /// <param name="operation">The operation to perform.</param>
    /// <returns>Recommended algorithm name.</returns>
    public string GetRecommendedAlgorithm(MatrixOperation operation)
    {
        return operation switch
        {
            MatrixOperation.Multiplication when IsSquare && Rows < 64 => "Direct",
            MatrixOperation.Multiplication when IsSquare => "Strassen",
            MatrixOperation.Multiplication when IsSparse => "SparseMul",
            MatrixOperation.Multiplication => "GEMM",
            MatrixOperation.Inversion when IsDiagonal => "DiagonalInverse",
            MatrixOperation.Inversion when IsSymmetric => "CholeskyInverse",
            MatrixOperation.Inversion => "LUInverse",
            MatrixOperation.Decomposition when IsSymmetric => "Cholesky",
            MatrixOperation.Decomposition => "LU",
            _ => "Default"
        };
    }

    private static bool CheckSymmetry(Matrix matrix)
    {
        if (!matrix.IsSquare)
        {
            return false;
        }

        for (var i = 0; i < matrix.Rows; i++)
        {
            for (var j = 0; j < matrix.Columns; j++)
            {
                if (Math.Abs(matrix[i, j] - matrix[j, i]) > 1e-6f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool CheckSymmetryLinearAlgebra(DotCompute.Algorithms.LinearAlgebra.Matrix matrix)
    {
        if (!matrix.IsSquare)
        {
            return false;
        }

        for (var i = 0; i < matrix.Rows; i++)
        {
            for (var j = 0; j < matrix.Columns; j++)
            {
                if (Math.Abs(matrix[i, j] - matrix[j, i]) > 1e-6f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool CheckDiagonal(Matrix matrix)
    {
        if (!matrix.IsSquare)
        {
            return false;
        }

        for (var i = 0; i < matrix.Rows; i++)
        {
            for (var j = 0; j < matrix.Columns; j++)
            {
                if (i != j && Math.Abs(matrix[i, j]) > 1e-6f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool CheckDiagonalLinearAlgebra(DotCompute.Algorithms.LinearAlgebra.Matrix matrix)
    {
        if (!matrix.IsSquare)
        {
            return false;
        }

        for (var i = 0; i < matrix.Rows; i++)
        {
            for (var j = 0; j < matrix.Columns; j++)
            {
                if (i != j && Math.Abs(matrix[i, j]) > 1e-6f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool CheckSparsity(Matrix matrix, out double sparsityRatio)
    {
        var totalElements = matrix.Rows * matrix.Columns;
        var zeroElements = 0;

        for (var i = 0; i < matrix.Rows; i++)
        {
            for (var j = 0; j < matrix.Columns; j++)
            {
                if (Math.Abs(matrix[i, j]) < 1e-10f)
                {
                    zeroElements++;
                }
            }
        }

        sparsityRatio = (double)zeroElements / totalElements;
        return sparsityRatio > 0.5; // Consider sparse if more than 50% zeros
    }

    private static bool CheckSparsityLinearAlgebra(DotCompute.Algorithms.LinearAlgebra.Matrix matrix, out double sparsityRatio)
    {
        var totalElements = matrix.Rows * matrix.Columns;
        var zeroElements = 0;

        for (var i = 0; i < matrix.Rows; i++)
        {
            for (var j = 0; j < matrix.Columns; j++)
            {
                if (Math.Abs(matrix[i, j]) < 1e-10f)
                {
                    zeroElements++;
                }
            }
        }

        sparsityRatio = (double)zeroElements / totalElements;
        return sparsityRatio > 0.5; // Consider sparse if more than 50% zeros
    }
}

/// <summary>
/// Matrix operations for algorithm selection.
/// </summary>
public enum MatrixOperation
{
    /// <summary>
    /// Matrix multiplication.
    /// </summary>
    Multiplication,

    /// <summary>
    /// Matrix addition.
    /// </summary>
    Addition,

    /// <summary>
    /// Matrix subtraction.
    /// </summary>
    Subtraction,

    /// <summary>
    /// Matrix inversion.
    /// </summary>
    Inversion,

    /// <summary>
    /// Matrix decomposition (LU, Cholesky, etc.).
    /// </summary>
    Decomposition,

    /// <summary>
    /// Matrix transpose.
    /// </summary>
    Transpose,

    /// <summary>
    /// Eigenvalue computation.
    /// </summary>
    Eigenvalues,

    /// <summary>
    /// Determinant computation.
    /// </summary>
    Determinant
}
