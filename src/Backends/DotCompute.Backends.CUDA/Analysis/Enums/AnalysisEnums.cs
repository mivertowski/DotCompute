// <copyright file="AnalysisEnums.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Analysis.Enums;

/// <summary>
/// Additional analysis enumerations for CUDA memory coalescing analyzer.
/// </summary>
public enum AnalysisResult
{
    /// <summary>
    /// Analysis completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Analysis completed with warnings.
    /// </summary>
    Warning,

    /// <summary>
    /// Analysis failed with errors.
    /// </summary>
    Error
}

/// <summary>
/// Memory optimization strategies.
/// </summary>
public enum OptimizationStrategy
{
    /// <summary>
    /// No optimization needed.
    /// </summary>
    None,

    /// <summary>
    /// Coalescing optimization recommended.
    /// </summary>
    Coalescing,

    /// <summary>
    /// Shared memory optimization recommended.
    /// </summary>
    SharedMemory,

    /// <summary>
    /// Texture memory optimization recommended.
    /// </summary>
    TextureMemory,

    /// <summary>
    /// Data layout reorganization recommended.
    /// </summary>
    DataLayout
}