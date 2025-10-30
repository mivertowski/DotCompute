// <copyright file="CompilationFallbackStrategy.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Types;

/// <summary>
/// Defines compilation fallback strategies for kernel compilation failures.
/// Strategies are applied progressively to recover from compilation errors.
/// </summary>
public enum CompilationFallbackStrategy
{
    /// <summary>
    /// Reduce optimization level to improve compilation success rate.
    /// Trades performance for compilation reliability.
    /// </summary>
    ReduceOptimizations,

    /// <summary>
    /// Disable fast math optimizations that may cause compilation issues.
    /// Improves numerical accuracy and compilation compatibility.
    /// </summary>
    DisableFastMath,

    /// <summary>
    /// Simplify complex kernel code to basic operations.
    /// Reduces kernel complexity to improve compilation success.
    /// </summary>
    SimplifyKernel,

    /// <summary>
    /// Switch to an alternative compiler backend.
    /// Uses a different compilation toolchain when the primary fails.
    /// </summary>
    AlternativeCompiler,

    /// <summary>
    /// Fall back to interpreter mode for kernel execution.
    /// Provides slow but guaranteed execution without compilation.
    /// </summary>
    InterpreterMode,

    /// <summary>
    /// Use a previously cached compiled version.
    /// Retrieves a working version from the compilation cache.
    /// </summary>
    CachedVersion,

    /// <summary>
    /// Roll back to a previous kernel version.
    /// Reverts to an earlier, known-working kernel implementation.
    /// </summary>
    RollbackVersion
}
