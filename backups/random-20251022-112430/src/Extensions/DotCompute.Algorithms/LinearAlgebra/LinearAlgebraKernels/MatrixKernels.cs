#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Types.Kernels;

namespace DotCompute.Algorithms.LinearAlgebra.LinearAlgebraKernels;


/// <summary>
/// GPU kernels for matrix operations.
/// </summary>
public sealed class MatrixMultiplyKernel : AlgorithmKernel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public override string Name => "MatrixMultiply";
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public override string Description => "GPU-accelerated matrix multiplication";
    /// <summary>
    /// Gets or sets a value indicating whether vectorized.
    /// </summary>
    /// <value>The is vectorized.</value>
    public override bool IsVectorized => true;
}

/// <summary>
/// GPU kernels for matrix factorization.
/// </summary>
public sealed class MatrixFactorizationKernel : AlgorithmKernel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public override string Name => "MatrixFactorization";
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public override string Description => "GPU-accelerated matrix factorization";
    /// <summary>
    /// Gets or sets a value indicating whether vectorized.
    /// </summary>
    /// <value>The is vectorized.</value>
    public override bool IsVectorized => true;
}

/// <summary>
/// GPU kernels for eigenvalue computation.
/// </summary>
public sealed class EigenvalueKernel : AlgorithmKernel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public override string Name => "Eigenvalue";
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public override string Description => "GPU-accelerated eigenvalue computation";
    /// <summary>
    /// Gets or sets a value indicating whether vectorized.
    /// </summary>
    /// <value>The is vectorized.</value>
    public override bool IsVectorized => true;
}
