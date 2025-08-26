// <copyright file="BlasModels.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Wrapper.BLAS.Models;

/// <summary>
/// Matrix descriptor for BLAS operations.
/// </summary>
public sealed class MatrixDescriptor
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
    /// Gets or sets the leading dimension.
    /// </summary>
    public int LeadingDimension { get; set; }

    /// <summary>
    /// Gets or sets the data type.
    /// </summary>
    public BlasDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets whether the matrix is transposed.
    /// </summary>
    public bool IsTransposed { get; set; }
}

/// <summary>
/// BLAS operation descriptor.
/// </summary>
public sealed class BlasOperationDescriptor
{
    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public BlasOperationType OperationType { get; set; }

    /// <summary>
    /// Gets or sets the input matrices.
    /// </summary>
    public List<MatrixDescriptor> InputMatrices { get; set; } = new();

    /// <summary>
    /// Gets or sets the output matrix.
    /// </summary>
    public MatrixDescriptor? OutputMatrix { get; set; }

    /// <summary>
    /// Gets or sets the operation parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// BLAS data type enumeration.
/// </summary>
public enum BlasDataType
{
    /// <summary>32-bit floating point.</summary>
    Float32 = 0,
    /// <summary>64-bit floating point.</summary>
    Float64 = 1,
    /// <summary>32-bit complex floating point.</summary>
    Complex32 = 2,
    /// <summary>64-bit complex floating point.</summary>
    Complex64 = 3,
    /// <summary>16-bit floating point.</summary>
    Float16 = 4,
    /// <summary>Brain float 16.</summary>
    BFloat16 = 5
}

/// <summary>
/// BLAS operation type enumeration.
/// </summary>
public enum BlasOperationType
{
    /// <summary>General matrix-matrix multiply (GEMM).</summary>
    Gemm = 0,
    /// <summary>General matrix-vector multiply (GEMV).</summary>
    Gemv = 1,
    /// <summary>Vector dot product (DOT).</summary>
    Dot = 2,
    /// <summary>Symmetric matrix-matrix multiply (SYMM).</summary>
    Symm = 3,
    /// <summary>Triangular solve (TRSM).</summary>
    Trsm = 4
}