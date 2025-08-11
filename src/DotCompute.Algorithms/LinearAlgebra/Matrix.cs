// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotCompute.Algorithms.LinearAlgebra;

/// <summary>
/// Represents a 2D matrix of floating-point values.
/// </summary>
public sealed class Matrix : IEquatable<Matrix>
{
    private readonly float[] _data;
    private readonly int _rows;
    private readonly int _cols;

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix"/> class.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    public Matrix(int rows, int cols)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cols);

        _rows = rows;
        _cols = cols;
        _data = new float[rows * cols];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix"/> class with data.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <param name="data">Matrix data in row-major order.</param>
    public Matrix(int rows, int cols, float[] data)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cols);
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length != rows * cols)
        {
            throw new ArgumentException($"Data array length ({data.Length}) does not match matrix dimensions ({rows}x{cols}).");
        }

        _rows = rows;
        _cols = cols;
        _data = new float[data.Length];
        Array.Copy(data, _data, data.Length);
    }

    /// <summary>
    /// Gets the number of rows in the matrix.
    /// </summary>
    public int Rows => _rows;

    /// <summary>
    /// Gets the number of columns in the matrix.
    /// </summary>
    public int Columns => _cols;

    /// <summary>
    /// Gets the total number of elements in the matrix.
    /// </summary>
    public int Size => _rows * _cols;

    /// <summary>
    /// Gets a value indicating whether this is a square matrix.
    /// </summary>
    public bool IsSquare => _rows == _cols;

    /// <summary>
    /// Gets a value indicating whether this is a vector (single row or column).
    /// </summary>
    public bool IsVector => _rows == 1 || _cols == 1;

    /// <summary>
    /// Gets or sets the element at the specified row and column.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="col">The column index.</param>
    /// <returns>The element value.</returns>
    public float this[int row, int col]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (row < 0 || row >= _rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
            if (col < 0 || col >= _cols)
            {
                throw new ArgumentOutOfRangeException(nameof(col));
            }
            return _data[row * _cols + col];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (row < 0 || row >= _rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
            if (col < 0 || col >= _cols)
            {
                throw new ArgumentOutOfRangeException(nameof(col));
            }
            _data[row * _cols + col] = value;
        }
    }

    /// <summary>
    /// Gets the internal data array (row-major order).
    /// </summary>
    /// <returns>A copy of the internal data array.</returns>
    public float[] ToArray()
    {
        var result = new float[_data.Length];
        Array.Copy(_data, result, _data.Length);
        return result;
    }

    /// <summary>
    /// Gets a span over the matrix data.
    /// </summary>
    /// <returns>A read-only span of the matrix data.</returns>
    public ReadOnlySpan<float> AsSpan() => _data.AsSpan();

    /// <summary>
    /// Creates an identity matrix of the specified size.
    /// </summary>
    /// <param name="size">The size of the square identity matrix.</param>
    /// <returns>An identity matrix.</returns>
    public static Matrix Identity(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        var matrix = new Matrix(size, size);
        for (var i = 0; i < size; i++)
        {
            matrix[i, i] = 1.0f;
        }
        return matrix;
    }

    /// <summary>
    /// Creates a matrix filled with zeros.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <returns>A zero matrix.</returns>
    public static Matrix Zeros(int rows, int cols)
    {
        return new Matrix(rows, cols);
    }

    /// <summary>
    /// Creates a matrix filled with ones.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <returns>A matrix of ones.</returns>
    public static Matrix Ones(int rows, int cols)
    {
        var matrix = new Matrix(rows, cols);
        Array.Fill(matrix._data, 1.0f);
        return matrix;
    }

    /// <summary>
    /// Creates a matrix filled with a specific value.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <param name="value">The fill value.</param>
    /// <returns>A filled matrix.</returns>
    public static Matrix Fill(int rows, int cols, float value)
    {
        var matrix = new Matrix(rows, cols);
        Array.Fill(matrix._data, value);
        return matrix;
    }

    /// <summary>
    /// Creates a matrix from a 2D array.
    /// </summary>
    /// <param name="array">The 2D array.</param>
    /// <returns>A new matrix.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Justification = "Convenience method for 2D array conversion")]
    public static Matrix FromArray(float[,] array)
    {
        ArgumentNullException.ThrowIfNull(array);

        var rows = array.GetLength(0);
        var cols = array.GetLength(1);
        var matrix = new Matrix(rows, cols);

        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                matrix[i, j] = array[i, j];
            }
        }

        return matrix;
    }

    /// <summary>
    /// Gets a row vector from the matrix.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <returns>A new matrix representing the row.</returns>
    public Matrix GetRow(int row)
    {
        if (row < 0 || row >= _rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        var result = new Matrix(1, _cols);
        Array.Copy(_data, row * _cols, result._data, 0, _cols);
        return result;
    }

    /// <summary>
    /// Gets a column vector from the matrix.
    /// </summary>
    /// <param name="col">The column index.</param>
    /// <returns>A new matrix representing the column.</returns>
    public Matrix GetColumn(int col)
    {
        if (col < 0 || col >= _cols)
        {
            throw new ArgumentOutOfRangeException(nameof(col));
        }

        var result = new Matrix(_rows, 1);
        for (var i = 0; i < _rows; i++)
        {
            result[i, 0] = this[i, col];
        }
        return result;
    }

    /// <summary>
    /// Creates a deep copy of the matrix.
    /// </summary>
    /// <returns>A new matrix with copied data.</returns>
    public Matrix Clone()
    {
        return new Matrix(_rows, _cols, _data);
    }

    /// <inheritdoc/>
    public bool Equals(Matrix? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (_rows != other._rows || _cols != other._cols)
        {
            return false;
        }

        return _data.AsSpan().SequenceEqual(other._data.AsSpan());
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return Equals(obj as Matrix);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_rows, _cols, _data.Length);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Matrix({_rows}x{_cols}):");
        
        for (var i = 0; i < Math.Min(_rows, 10); i++)
        {
            sb.Append('[');
            for (var j = 0; j < Math.Min(_cols, 10); j++)
            {
                if (j > 0)
                {
                    sb.Append(", ");
                }
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0,8:F4}", this[i, j]);
            }
            if (_cols > 10)
            {
                sb.Append(", ...");
            }
            sb.AppendLine("]");
        }
        
        if (_rows > 10)
        {
            sb.AppendLine("[...]");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Matrix? left, Matrix? right)
    {
        if (left is null)
        {
            return right is null;
        }
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Matrix? left, Matrix? right)
    {
        return !(left == right);
    }
}