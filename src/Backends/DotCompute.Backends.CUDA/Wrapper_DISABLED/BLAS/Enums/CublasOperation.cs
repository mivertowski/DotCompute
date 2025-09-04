// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Wrapper.BLAS.Enums;

/// <summary>
/// cuBLAS operation types
/// </summary>
public enum CublasOperation
{
    NonTranspose = 0,
    Transpose = 1,
    ConjugateTranspose = 2
}