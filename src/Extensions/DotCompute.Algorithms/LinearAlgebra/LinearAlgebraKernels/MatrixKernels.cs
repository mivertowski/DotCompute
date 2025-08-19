// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Types.Kernels;

namespace DotCompute.Algorithms.LinearAlgebra.LinearAlgebraKernels;


/// <summary>
/// GPU kernels for matrix operations.
/// </summary>
public sealed class MatrixMultiplyKernel : AlgorithmKernel
{
public override string Name => "MatrixMultiply";
public override string Description => "GPU-accelerated matrix multiplication";
public override bool IsVectorized => true;
}

/// <summary>
/// GPU kernels for matrix factorization.
/// </summary>
public sealed class MatrixFactorizationKernel : AlgorithmKernel
{
public override string Name => "MatrixFactorization";
public override string Description => "GPU-accelerated matrix factorization";
public override bool IsVectorized => true;
}

/// <summary>
/// GPU kernels for eigenvalue computation.
/// </summary>
public sealed class EigenvalueKernel : AlgorithmKernel
{
public override string Name => "Eigenvalue";
public override string Description => "GPU-accelerated eigenvalue computation";
public override bool IsVectorized => true;
}
