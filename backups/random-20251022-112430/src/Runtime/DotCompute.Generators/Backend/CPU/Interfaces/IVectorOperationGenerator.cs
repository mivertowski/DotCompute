// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;

namespace DotCompute.Generators.Backend.CPU.Interfaces;

/// <summary>
/// Interface for generating vector operations for SIMD implementations.
/// </summary>
public interface IVectorOperationGenerator
{
    /// <summary>
    /// Generates SIMD operations using global::System.Numerics.Vector.
    /// </summary>
    public void GenerateSimdOperations(StringBuilder sb, VectorizationInfo vectorizationInfo);

    /// <summary>
    /// Generates AVX2 intrinsic operations.
    /// </summary>
    public void GenerateAvx2Operations(StringBuilder sb, VectorizationInfo vectorizationInfo);

    /// <summary>
    /// Generates AVX-512 intrinsic operations.
    /// </summary>
    public void GenerateAvx512Operations(StringBuilder sb, VectorizationInfo vectorizationInfo);
}