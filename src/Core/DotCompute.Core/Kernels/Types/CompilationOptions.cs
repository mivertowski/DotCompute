// <copyright file="CompilationOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Enums;

namespace DotCompute.Core.Kernels.Types;

/// <summary>
/// Represents compilation options for kernel compilation.
/// Provides comprehensive configuration for kernel compilation including optimization levels,
/// debug information generation, and platform-specific settings.
/// </summary>
public sealed class CompilationOptions
{
    /// <summary>
    /// Gets or sets the optimization level for compilation.
    /// Higher optimization levels may increase compilation time but improve runtime performance.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.O2;

    /// <summary>
    /// Gets or sets whether to generate debug information.
    /// Enabling debug information allows for better debugging but may impact performance.
    /// </summary>
    public bool GenerateDebugInfo { get; set; }

    /// <summary>
    /// Gets or sets whether to enable fast math operations.
    /// Fast math may improve performance but can reduce numerical accuracy.
    /// </summary>
    public bool EnableFastMath { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable finite math only.
    /// When enabled, assumes that floating-point arguments and results are not NaN or ±Infinity.
    /// </summary>
    public bool FiniteMathOnly { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable unsafe optimizations.
    /// Unsafe optimizations may break strict language semantics but can improve performance.
    /// </summary>
    public bool EnableUnsafeOptimizations { get; set; }

    /// <summary>
    /// Gets or sets the target architecture (e.g., sm_75 for CUDA).
    /// Platform-specific architecture identifier for optimized code generation.
    /// </summary>
    public string? TargetArchitecture { get; set; }

    /// <summary>
    /// Gets or sets additional compiler flags.
    /// Custom flags passed directly to the underlying compiler.
    /// </summary>
    public List<string> AdditionalFlags { get; set; } = [];

    /// <summary>
    /// Gets or sets include directories for compilation.
    /// Paths to directories containing header files or additional source files.
    /// </summary>
    public List<string> IncludeDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets preprocessor definitions.
    /// Key-value pairs defining preprocessor macros for conditional compilation.
    /// </summary>
    public Dictionary<string, string> Defines { get; set; } = [];
}