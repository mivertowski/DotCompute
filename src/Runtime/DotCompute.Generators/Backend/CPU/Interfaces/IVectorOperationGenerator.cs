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
    /// Generates SIMD operations using System.Numerics.Vector.
    /// </summary>
    void GenerateSimdOperations(StringBuilder sb, VectorizationInfo vectorizationInfo);
    
    /// <summary>
    /// Generates AVX2 intrinsic operations.
    /// </summary>
    void GenerateAvx2Operations(StringBuilder sb, VectorizationInfo vectorizationInfo);
    
    /// <summary>
    /// Generates AVX-512 intrinsic operations.
    /// </summary>
    void GenerateAvx512Operations(StringBuilder sb, VectorizationInfo vectorizationInfo);
}