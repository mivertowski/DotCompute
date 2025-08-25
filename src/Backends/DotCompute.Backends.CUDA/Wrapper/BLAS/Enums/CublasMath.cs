// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Wrapper.BLAS.Enums;

/// <summary>
/// cuBLAS math mode
/// </summary>
public enum CublasMath
{
    DefaultMath = 0,
    TensorOpMath = 1,
    PedanticMath = 2,
    TF32TensorOpMath = 3
}