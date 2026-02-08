// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;

#pragma warning disable CA1000 // Do not declare static members on generic types - valid pattern for sparse matrix factory methods
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional - multidimensional arrays are the correct dense matrix representation

namespace DotCompute.Algorithms.Sparse;

/// <summary>
/// Sparse matrix storage format.
/// </summary>
public enum SparseFormat
{
    /// <summary>
    /// Compressed Sparse Row format.
    /// Efficient for row-wise access and matrix-vector multiplication.
    /// </summary>
    CSR,

    /// <summary>
    /// Compressed Sparse Column format.
    /// Efficient for column-wise access and transpose operations.
    /// </summary>
    CSC,

    /// <summary>
    /// Coordinate format (triplet format).
    /// Efficient for incremental construction and format conversion.
    /// </summary>
    COO,

    /// <summary>
    /// Block Sparse Row format.
    /// Efficient for matrices with dense sub-blocks.
    /// </summary>
    BSR,

    /// <summary>
    /// Diagonal format.
    /// Efficient for banded matrices.
    /// </summary>
    DIA
}

/// <summary>
/// Compressed Sparse Row (CSR) matrix format.
/// </summary>
/// <remarks>
/// <para>
/// CSR is the most common sparse matrix format, storing:
/// <list type="bullet">
/// <item><b>Values</b>: Non-zero elements in row-major order</item>
/// <item><b>ColumnIndices</b>: Column index for each non-zero</item>
/// <item><b>RowPointers</b>: Starting index in Values for each row</item>
/// </list>
/// </para>
/// <para>
/// <strong>Memory:</strong> O(nnz + rows + 1) where nnz = number of non-zeros
/// </para>
/// <para>
/// <strong>Best for:</strong> SpMV, row slicing, most GPU operations
/// </para>
/// </remarks>
/// <typeparam name="T">The element type.</typeparam>
public sealed class CsrMatrix<T> where T : unmanaged, INumber<T>
{
    /// <summary>
    /// Creates a CSR matrix.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="columns">Number of columns.</param>
    /// <param name="values">Non-zero values.</param>
    /// <param name="columnIndices">Column indices for each value.</param>
    /// <param name="rowPointers">Row pointer array (length = rows + 1).</param>
    public CsrMatrix(int rows, int columns, T[] values, int[] columnIndices, int[] rowPointers)
    {
        if (rowPointers.Length != rows + 1)
        {
            throw new ArgumentException($"RowPointers length must be {rows + 1}, got {rowPointers.Length}");
        }
        if (values.Length != columnIndices.Length)
        {
            throw new ArgumentException("Values and ColumnIndices must have same length");
        }

        Rows = rows;
        Columns = columns;
        Values = values;
        ColumnIndices = columnIndices;
        RowPointers = rowPointers;
    }

    /// <summary>
    /// Gets the number of rows.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Gets the number of columns.
    /// </summary>
    public int Columns { get; }

    /// <summary>
    /// Gets the non-zero values.
    /// </summary>
    public T[] Values { get; }

    /// <summary>
    /// Gets the column indices.
    /// </summary>
    public int[] ColumnIndices { get; }

    /// <summary>
    /// Gets the row pointers.
    /// </summary>
    public int[] RowPointers { get; }

    /// <summary>
    /// Gets the number of non-zero elements.
    /// </summary>
    public int NonZeroCount => Values.Length;

    /// <summary>
    /// Gets the sparsity ratio (fraction of zeros).
    /// </summary>
    public double Sparsity => 1.0 - (double)NonZeroCount / (Rows * Columns);

    /// <summary>
    /// Gets an element at the specified position.
    /// </summary>
    public T this[int row, int col]
    {
        get
        {
            ValidateIndices(row, col);
            var start = RowPointers[row];
            var end = RowPointers[row + 1];

            for (var i = start; i < end; i++)
            {
                if (ColumnIndices[i] == col)
                {
                    return Values[i];
                }
                if (ColumnIndices[i] > col)
                {
                    break; // Column indices are sorted
                }
            }
            return T.Zero;
        }
    }

    /// <summary>
    /// Gets non-zero elements in a row.
    /// </summary>
    public IEnumerable<(int Column, T Value)> GetRow(int row)
    {
        ValidateRowIndex(row);
        var start = RowPointers[row];
        var end = RowPointers[row + 1];

        for (var i = start; i < end; i++)
        {
            yield return (ColumnIndices[i], Values[i]);
        }
    }

    /// <summary>
    /// Converts to COO format.
    /// </summary>
    public CooMatrix<T> ToCoo()
    {
        var rowIndices = new int[NonZeroCount];
        for (var row = 0; row < Rows; row++)
        {
            for (var i = RowPointers[row]; i < RowPointers[row + 1]; i++)
            {
                rowIndices[i] = row;
            }
        }
        return new CooMatrix<T>(Rows, Columns, Values.ToArray(), rowIndices, ColumnIndices.ToArray());
    }

    /// <summary>
    /// Converts to CSC format.
    /// </summary>
    public CscMatrix<T> ToCsc()
    {
        var coo = ToCoo();
        return coo.ToCsc();
    }

    /// <summary>
    /// Creates a CSR matrix from a dense matrix.
    /// </summary>
    public static CsrMatrix<T> FromDense(T[,] dense, T? threshold = null)
    {
        var rows = dense.GetLength(0);
        var columns = dense.GetLength(1);
        var values = new List<T>();
        var columnIndices = new List<int>();
        var rowPointers = new int[rows + 1];

        for (var i = 0; i < rows; i++)
        {
            rowPointers[i] = values.Count;
            for (var j = 0; j < columns; j++)
            {
                var value = dense[i, j];
                var isNonZero = threshold.HasValue
                    ? T.Abs(value) > threshold.Value
                    : value != T.Zero;

                if (isNonZero)
                {
                    values.Add(value);
                    columnIndices.Add(j);
                }
            }
        }
        rowPointers[rows] = values.Count;

        return new CsrMatrix<T>(rows, columns, values.ToArray(), columnIndices.ToArray(), rowPointers);
    }

    /// <summary>
    /// Converts to dense matrix.
    /// </summary>
    public T[,] ToDense()
    {
        var dense = new T[Rows, Columns];
        for (var row = 0; row < Rows; row++)
        {
            for (var i = RowPointers[row]; i < RowPointers[row + 1]; i++)
            {
                dense[row, ColumnIndices[i]] = Values[i];
            }
        }
        return dense;
    }

    private void ValidateIndices(int row, int col)
    {
        if (row < 0 || row >= Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row), $"Row {row} out of range [0, {Rows})");
        }
        if (col < 0 || col >= Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(col), $"Column {col} out of range [0, {Columns})");
        }
    }

    private void ValidateRowIndex(int row)
    {
        if (row < 0 || row >= Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row), $"Row {row} out of range [0, {Rows})");
        }
    }
}

/// <summary>
/// Compressed Sparse Column (CSC) matrix format.
/// </summary>
/// <remarks>
/// <para>
/// CSC stores the transpose structure of CSR:
/// <list type="bullet">
/// <item><b>Values</b>: Non-zero elements in column-major order</item>
/// <item><b>RowIndices</b>: Row index for each non-zero</item>
/// <item><b>ColumnPointers</b>: Starting index in Values for each column</item>
/// </list>
/// </para>
/// <para>
/// <strong>Best for:</strong> Column slicing, solving triangular systems, sparse direct methods
/// </para>
/// </remarks>
public sealed class CscMatrix<T> where T : unmanaged, INumber<T>
{
    /// <summary>
    /// Creates a CSC matrix.
    /// </summary>
    public CscMatrix(int rows, int columns, T[] values, int[] rowIndices, int[] columnPointers)
    {
        if (columnPointers.Length != columns + 1)
        {
            throw new ArgumentException($"ColumnPointers length must be {columns + 1}, got {columnPointers.Length}");
        }
        if (values.Length != rowIndices.Length)
        {
            throw new ArgumentException("Values and RowIndices must have same length");
        }

        Rows = rows;
        Columns = columns;
        Values = values;
        RowIndices = rowIndices;
        ColumnPointers = columnPointers;
    }

    /// <summary>
    /// Gets the number of rows.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Gets the number of columns.
    /// </summary>
    public int Columns { get; }

    /// <summary>
    /// Gets the non-zero values.
    /// </summary>
    public T[] Values { get; }

    /// <summary>
    /// Gets the row indices.
    /// </summary>
    public int[] RowIndices { get; }

    /// <summary>
    /// Gets the column pointers.
    /// </summary>
    public int[] ColumnPointers { get; }

    /// <summary>
    /// Gets the number of non-zero elements.
    /// </summary>
    public int NonZeroCount => Values.Length;

    /// <summary>
    /// Gets the sparsity ratio.
    /// </summary>
    public double Sparsity => 1.0 - (double)NonZeroCount / (Rows * Columns);

    /// <summary>
    /// Gets an element at the specified position.
    /// </summary>
    public T this[int row, int col]
    {
        get
        {
            var start = ColumnPointers[col];
            var end = ColumnPointers[col + 1];

            for (var i = start; i < end; i++)
            {
                if (RowIndices[i] == row)
                {
                    return Values[i];
                }
                if (RowIndices[i] > row)
                {
                    break;
                }
            }
            return T.Zero;
        }
    }

    /// <summary>
    /// Gets non-zero elements in a column.
    /// </summary>
    public IEnumerable<(int Row, T Value)> GetColumn(int col)
    {
        var start = ColumnPointers[col];
        var end = ColumnPointers[col + 1];

        for (var i = start; i < end; i++)
        {
            yield return (RowIndices[i], Values[i]);
        }
    }

    /// <summary>
    /// Converts to CSR format.
    /// </summary>
    public CsrMatrix<T> ToCsr()
    {
        var coo = ToCoo();
        return coo.ToCsr();
    }

    /// <summary>
    /// Converts to COO format.
    /// </summary>
    public CooMatrix<T> ToCoo()
    {
        var colIndices = new int[NonZeroCount];
        for (var col = 0; col < Columns; col++)
        {
            for (var i = ColumnPointers[col]; i < ColumnPointers[col + 1]; i++)
            {
                colIndices[i] = col;
            }
        }
        return new CooMatrix<T>(Rows, Columns, Values.ToArray(), RowIndices.ToArray(), colIndices);
    }
}

/// <summary>
/// Coordinate (COO) sparse matrix format.
/// </summary>
/// <remarks>
/// <para>
/// COO format stores triplets (row, column, value) for each non-zero:
/// <list type="bullet">
/// <item><b>Values</b>: Non-zero element values</item>
/// <item><b>RowIndices</b>: Row index for each element</item>
/// <item><b>ColumnIndices</b>: Column index for each element</item>
/// </list>
/// </para>
/// <para>
/// <strong>Best for:</strong> Incremental construction, format conversion, simple operations
/// </para>
/// </remarks>
public sealed class CooMatrix<T> where T : unmanaged, INumber<T>
{
    /// <summary>
    /// Creates a COO matrix.
    /// </summary>
    public CooMatrix(int rows, int columns, T[] values, int[] rowIndices, int[] columnIndices)
    {
        if (values.Length != rowIndices.Length || values.Length != columnIndices.Length)
        {
            throw new ArgumentException("Values, RowIndices, and ColumnIndices must have same length");
        }

        Rows = rows;
        Columns = columns;
        Values = values;
        RowIndices = rowIndices;
        ColumnIndices = columnIndices;
    }

    /// <summary>
    /// Gets the number of rows.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Gets the number of columns.
    /// </summary>
    public int Columns { get; }

    /// <summary>
    /// Gets the non-zero values.
    /// </summary>
    public T[] Values { get; }

    /// <summary>
    /// Gets the row indices.
    /// </summary>
    public int[] RowIndices { get; }

    /// <summary>
    /// Gets the column indices.
    /// </summary>
    public int[] ColumnIndices { get; }

    /// <summary>
    /// Gets the number of non-zero elements.
    /// </summary>
    public int NonZeroCount => Values.Length;

    /// <summary>
    /// Gets the sparsity ratio.
    /// </summary>
    public double Sparsity => 1.0 - (double)NonZeroCount / (Rows * Columns);

    /// <summary>
    /// Converts to CSR format.
    /// </summary>
    public CsrMatrix<T> ToCsr()
    {
        // Sort by row, then column
        var indices = Enumerable.Range(0, NonZeroCount)
            .OrderBy(i => RowIndices[i])
            .ThenBy(i => ColumnIndices[i])
            .ToArray();

        var sortedValues = indices.Select(i => Values[i]).ToArray();
        var sortedCols = indices.Select(i => ColumnIndices[i]).ToArray();

        // Build row pointers
        var rowPointers = new int[Rows + 1];
        for (var i = 0; i < NonZeroCount; i++)
        {
            rowPointers[RowIndices[indices[i]] + 1]++;
        }
        for (var i = 0; i < Rows; i++)
        {
            rowPointers[i + 1] += rowPointers[i];
        }

        return new CsrMatrix<T>(Rows, Columns, sortedValues, sortedCols, rowPointers);
    }

    /// <summary>
    /// Converts to CSC format.
    /// </summary>
    public CscMatrix<T> ToCsc()
    {
        // Sort by column, then row
        var indices = Enumerable.Range(0, NonZeroCount)
            .OrderBy(i => ColumnIndices[i])
            .ThenBy(i => RowIndices[i])
            .ToArray();

        var sortedValues = indices.Select(i => Values[i]).ToArray();
        var sortedRows = indices.Select(i => RowIndices[i]).ToArray();

        // Build column pointers
        var colPointers = new int[Columns + 1];
        for (var i = 0; i < NonZeroCount; i++)
        {
            colPointers[ColumnIndices[indices[i]] + 1]++;
        }
        for (var i = 0; i < Columns; i++)
        {
            colPointers[i + 1] += colPointers[i];
        }

        return new CscMatrix<T>(Rows, Columns, sortedValues, sortedRows, colPointers);
    }

    /// <summary>
    /// Creates a COO matrix from a dense matrix.
    /// </summary>
    public static CooMatrix<T> FromDense(T[,] dense, T? threshold = null)
    {
        var rows = dense.GetLength(0);
        var columns = dense.GetLength(1);
        var values = new List<T>();
        var rowIndices = new List<int>();
        var colIndices = new List<int>();

        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < columns; j++)
            {
                var value = dense[i, j];
                var isNonZero = threshold.HasValue
                    ? T.Abs(value) > threshold.Value
                    : value != T.Zero;

                if (isNonZero)
                {
                    values.Add(value);
                    rowIndices.Add(i);
                    colIndices.Add(j);
                }
            }
        }

        return new CooMatrix<T>(rows, columns, values.ToArray(), rowIndices.ToArray(), colIndices.ToArray());
    }
}

/// <summary>
/// Builder for constructing sparse matrices incrementally.
/// </summary>
public sealed class SparseMatrixBuilder<T> where T : unmanaged, INumber<T>
{
    private readonly List<(int Row, int Col, T Value)> _entries = new();
    private int _maxRow;
    private int _maxCol;

    /// <summary>
    /// Creates a builder for a matrix with specified dimensions.
    /// </summary>
    public SparseMatrixBuilder(int rows = 0, int columns = 0)
    {
        _maxRow = rows > 0 ? rows - 1 : 0;
        _maxCol = columns > 0 ? columns - 1 : 0;
    }

    /// <summary>
    /// Adds a non-zero entry.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SparseMatrixBuilder<T> Add(int row, int col, T value)
    {
        if (value != T.Zero)
        {
            _entries.Add((row, col, value));
            _maxRow = Math.Max(_maxRow, row);
            _maxCol = Math.Max(_maxCol, col);
        }
        return this;
    }

    /// <summary>
    /// Gets the current number of entries.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Builds a CSR matrix.
    /// </summary>
    public CsrMatrix<T> ToCsr()
    {
        var coo = ToCoo();
        return coo.ToCsr();
    }

    /// <summary>
    /// Builds a CSC matrix.
    /// </summary>
    public CscMatrix<T> ToCsc()
    {
        var coo = ToCoo();
        return coo.ToCsc();
    }

    /// <summary>
    /// Builds a COO matrix.
    /// </summary>
    public CooMatrix<T> ToCoo()
    {
        // Combine duplicates
        var combined = _entries
            .GroupBy(e => (e.Row, e.Col))
            .ToDictionary(g => g.Key, g => g.Aggregate(T.Zero, (acc, e) => acc + e.Value));

        var values = new T[combined.Count];
        var rowIndices = new int[combined.Count];
        var colIndices = new int[combined.Count];

        var idx = 0;
        foreach (var ((row, col), value) in combined)
        {
            values[idx] = value;
            rowIndices[idx] = row;
            colIndices[idx] = col;
            idx++;
        }

        return new CooMatrix<T>(_maxRow + 1, _maxCol + 1, values, rowIndices, colIndices);
    }
}

/// <summary>
/// Sparse matrix operations.
/// </summary>
public static class SparseOps
{
    /// <summary>
    /// Sparse matrix-vector multiplication: y = A * x.
    /// </summary>
    public static T[] SpMV<T>(CsrMatrix<T> a, T[] x) where T : unmanaged, INumber<T>
    {
        if (a.Columns != x.Length)
        {
            throw new ArgumentException($"Matrix columns ({a.Columns}) must match vector length ({x.Length})");
        }

        var y = new T[a.Rows];
        for (var row = 0; row < a.Rows; row++)
        {
            T sum = T.Zero;
            var start = a.RowPointers[row];
            var end = a.RowPointers[row + 1];

            for (var i = start; i < end; i++)
            {
                sum += a.Values[i] * x[a.ColumnIndices[i]];
            }
            y[row] = sum;
        }
        return y;
    }

    /// <summary>
    /// Sparse matrix-matrix multiplication: C = A * B.
    /// </summary>
    public static CsrMatrix<T> SpMM<T>(CsrMatrix<T> a, CsrMatrix<T> b) where T : unmanaged, INumber<T>
    {
        if (a.Columns != b.Rows)
        {
            throw new ArgumentException($"Matrix dimensions don't match: ({a.Rows}×{a.Columns}) × ({b.Rows}×{b.Columns})");
        }

        var builder = new SparseMatrixBuilder<T>(a.Rows, b.Columns);

        for (var i = 0; i < a.Rows; i++)
        {
            // Accumulator for row i of result
            var rowAccum = new Dictionary<int, T>();

            // For each non-zero in row i of A
            for (var ia = a.RowPointers[i]; ia < a.RowPointers[i + 1]; ia++)
            {
                var k = a.ColumnIndices[ia];
                T aik = a.Values[ia];

                // Multiply with row k of B
                for (var ib = b.RowPointers[k]; ib < b.RowPointers[k + 1]; ib++)
                {
                    var j = b.ColumnIndices[ib];
                    T bkj = b.Values[ib];

                    if (!rowAccum.TryGetValue(j, out var existing))
                    {
                        existing = T.Zero;
                    }
                    rowAccum[j] = existing + aik * bkj;
                }
            }

            // Add accumulated values to builder
            foreach (var (j, value) in rowAccum)
            {
                builder.Add(i, j, value);
            }
        }

        return builder.ToCsr();
    }

    /// <summary>
    /// Sparse matrix addition: C = A + B.
    /// </summary>
    public static CsrMatrix<T> Add<T>(CsrMatrix<T> a, CsrMatrix<T> b) where T : unmanaged, INumber<T>
    {
        if (a.Rows != b.Rows || a.Columns != b.Columns)
        {
            throw new ArgumentException($"Matrix dimensions must match: ({a.Rows}×{a.Columns}) vs ({b.Rows}×{b.Columns})");
        }

        var builder = new SparseMatrixBuilder<T>(a.Rows, a.Columns);

        for (var row = 0; row < a.Rows; row++)
        {
            var rowValues = new Dictionary<int, T>();

            // Add entries from A
            for (var i = a.RowPointers[row]; i < a.RowPointers[row + 1]; i++)
            {
                rowValues[a.ColumnIndices[i]] = a.Values[i];
            }

            // Add entries from B
            for (var i = b.RowPointers[row]; i < b.RowPointers[row + 1]; i++)
            {
                var col = b.ColumnIndices[i];
                if (rowValues.TryGetValue(col, out var existing))
                {
                    rowValues[col] = existing + b.Values[i];
                }
                else
                {
                    rowValues[col] = b.Values[i];
                }
            }

            foreach (var (col, value) in rowValues)
            {
                builder.Add(row, col, value);
            }
        }

        return builder.ToCsr();
    }

    /// <summary>
    /// Sparse matrix transpose.
    /// </summary>
    public static CsrMatrix<T> Transpose<T>(CsrMatrix<T> a) where T : unmanaged, INumber<T>
    {
        var csc = a.ToCsc();
        // CSC of A is CSR of A^T
        return new CsrMatrix<T>(a.Columns, a.Rows, csc.Values, csc.RowIndices, csc.ColumnPointers);
    }

    /// <summary>
    /// Computes the Frobenius norm: ||A||_F = sqrt(sum(a_ij^2)).
    /// </summary>
    public static double FrobeniusNorm<T>(CsrMatrix<T> a) where T : unmanaged, INumber<T>
    {
        double sumSquares = 0;
        foreach (var value in a.Values)
        {
            var v = double.CreateChecked(value);
            sumSquares += v * v;
        }
        return Math.Sqrt(sumSquares);
    }

    /// <summary>
    /// Scales a sparse matrix: B = alpha * A.
    /// </summary>
    public static CsrMatrix<T> Scale<T>(CsrMatrix<T> a, T alpha) where T : unmanaged, INumber<T>
    {
        var scaledValues = a.Values.Select(v => v * alpha).ToArray();
        return new CsrMatrix<T>(a.Rows, a.Columns, scaledValues, a.ColumnIndices.ToArray(), a.RowPointers.ToArray());
    }
}
