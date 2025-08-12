// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Types;

namespace DotCompute.Algorithms.Types;

/// <summary>
/// Represents analytical properties of a matrix for optimization purposes.
/// </summary>
public sealed record MatrixProperties
{
    /// <summary>
    /// Gets or sets the total size (rows * columns) of the matrix.
    /// </summary>
    public int Size { get; init; }

    /// <summary>
    /// Gets or sets the number of rows.
    /// </summary>
    public int Rows { get; init; }

    /// <summary>
    /// Gets or sets the number of columns.
    /// </summary>
    public int Columns { get; init; }

    /// <summary>
    /// Gets or sets the sparsity ratio (0.0 = dense, 1.0 = completely sparse).
    /// </summary>
    public float SparsityRatio { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the matrix is symmetric.
    /// </summary>
    public bool IsSymmetric { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the matrix is positive definite.
    /// </summary>
    public bool IsPositiveDefinite { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the matrix is sparse.
    /// </summary>
    public bool IsSparse => SparsityRatio > 0.5f;

    /// <summary>
    /// Gets or sets a value indicating whether the matrix is square.
    /// </summary>
    public bool IsSquare => Rows == Columns;

    /// <summary>
    /// Gets or sets the estimated condition number.
    /// </summary>
    public double? ConditionNumber { get; init; }

    /// <summary>
    /// Gets or sets the norm of the matrix.
    /// </summary>
    public double? Norm { get; init; }

    /// <summary>
    /// Gets or sets the data type of matrix elements.
    /// </summary>
    public Type ElementType { get; init; } = typeof(float);

    /// <summary>
    /// Creates matrix properties from a matrix.
    /// </summary>
    /// <param name="matrix">The matrix to analyze.</param>
    /// <returns>Matrix properties.</returns>
    public static MatrixProperties FromMatrix(Matrix matrix)
    {
        return new MatrixProperties
        {
            Size = matrix.Size,
            Rows = matrix.Rows,
            Columns = matrix.Columns,
            SparsityRatio = ComputeSparsityRatio(matrix),
            IsSymmetric = CheckIsSymmetric(matrix),
            IsPositiveDefinite = CheckIsPositiveDefinite(matrix)
        };
    }

    private static float ComputeSparsityRatio(Matrix matrix)
    {
        var data = matrix.ToArray();
        var zeroCount = data.Count(x => Math.Abs(x) < 1e-10f);
        return (float)zeroCount / data.Length;
    }

    private static bool CheckIsSymmetric(Matrix matrix)
    {
        if (!matrix.IsSquare) return false;

        for (var i = 0; i < matrix.Rows; i++)
        {
            for (var j = 0; j < matrix.Columns; j++)
            {
                if (Math.Abs(matrix[i, j] - matrix[j, i]) > 1e-6f)
                    return false;
            }
        }
        return true;
    }

    private static bool CheckIsPositiveDefinite(Matrix matrix)
    {
        if (!matrix.IsSquare || !CheckIsSymmetric(matrix)) return false;

        // Simplified check - full implementation would use Cholesky decomposition
        // For now, just check diagonal elements are positive
        for (var i = 0; i < matrix.Rows; i++)
        {
            if (matrix[i, i] <= 0)
                return false;
        }
        return true;
    }
}