// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This is a redirect to the main Matrix implementation
// Re-export the Matrix type for compatibility
namespace DotCompute.Algorithms.Types.LinearAlgebra;

/// <summary>
/// Matrix type alias for compatibility.
/// </summary>
public class Matrix
{
    /// <summary>
    /// Gets the actual matrix implementation.
    /// </summary>
    public Algorithms.LinearAlgebra.Matrix Implementation { get; }

    /// <summary>
    /// Initializes a new instance of the Matrix class.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    public Matrix(int rows, int cols)

    {
        Implementation = new Algorithms.LinearAlgebra.Matrix(rows, cols);
    }

    /// <summary>
    /// Initializes a new instance of the Matrix class with data.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <param name="data">Matrix data in row-major order.</param>
    public Matrix(int rows, int cols, float[] data)

    {
        Implementation = new Algorithms.LinearAlgebra.Matrix(rows, cols, data);
    }

    /// <summary>
    /// Converts from the main Matrix type.
    /// </summary>
    /// <param name="matrix">The matrix to convert.</param>
    /// <returns>A new Matrix wrapper instance.</returns>
    public static Matrix ToMatrix(Algorithms.LinearAlgebra.Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return new Matrix(matrix.Rows, matrix.Columns, matrix.ToArray());
    }

    /// <summary>
    /// Converts to the main Matrix type.
    /// </summary>
    /// <param name="matrix">The matrix to convert.</param>
    /// <returns>The underlying implementation.</returns>
    public static Algorithms.LinearAlgebra.Matrix FromMatrix(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return matrix.Implementation;
    }

    /// <summary>
    /// Implicit conversion from the main Matrix type.
    /// </summary>
    public static implicit operator Matrix(Algorithms.LinearAlgebra.Matrix matrix) => ToMatrix(matrix);

    /// <summary>
    /// Implicit conversion to the main Matrix type.
    /// </summary>
    public static implicit operator Algorithms.LinearAlgebra.Matrix(Matrix matrix) => FromMatrix(matrix);

    /// <summary>
    /// Gets the number of rows.
    /// </summary>
    public int Rows => Implementation.Rows;

    /// <summary>
    /// Gets the number of columns.
    /// </summary>
    public int Columns => Implementation.Columns;

    /// <summary>
    /// Gets a value indicating whether this is a square matrix.
    /// </summary>
    public bool IsSquare => Implementation.IsSquare;

    /// <summary>
    /// Gets the element at the specified position.
    /// </summary>
    public float this[int row, int col] => Implementation[row, col];
}