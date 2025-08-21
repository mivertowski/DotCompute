// <copyright file="KernelLanguage.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Operators.Types;

/// <summary>
/// Defines supported kernel programming languages.
/// </summary>
public enum KernelLanguage
{
    /// <summary>
    /// C# expression trees and IL.
    /// </summary>
    CSharp,

    /// <summary>
    /// CUDA C/C++ for NVIDIA GPUs.
    /// </summary>
    CUDA,

    /// <summary>
    /// OpenCL for cross-platform GPU/CPU execution.
    /// </summary>
    OpenCL,

    /// <summary>
    /// HLSL for DirectCompute.
    /// </summary>
    HLSL,

    /// <summary>
    /// Metal Shading Language for Apple devices.
    /// </summary>
    Metal,

    /// <summary>
    /// SPIR-V intermediate representation.
    /// </summary>
    SPIRV
}