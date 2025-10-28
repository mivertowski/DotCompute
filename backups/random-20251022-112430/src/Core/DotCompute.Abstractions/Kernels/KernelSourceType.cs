// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Specifies the source type of a kernel.
/// </summary>
public enum KernelSourceType
{
    /// <summary>
    /// C# source code.
    /// </summary>
    CSharp,

    /// <summary>
    /// CUDA C source code.
    /// </summary>
    Cuda,

    /// <summary>
    /// OpenCL C source code.
    /// </summary>
    OpenCL,

    /// <summary>
    /// Metal Shading Language source code.
    /// </summary>
    Metal,

    /// <summary>
    /// HLSL shader code.
    /// </summary>
    HLSL,

    /// <summary>
    /// SPIR-V bytecode.
    /// </summary>
    SpirV,

    /// <summary>
    /// PTX assembly code.
    /// </summary>
    PTX,

    /// <summary>
    /// LLVM IR code.
    /// </summary>
    LLVM,

    /// <summary>
    /// Native machine code.
    /// </summary>
    Native
}
