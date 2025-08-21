// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Extension methods for the <see cref="CompilationOptions"/> class to support
/// recovery operations and fallback strategy implementations.
/// </summary>
/// <remarks>
/// These extensions provide utility methods for working with compilation options
/// in recovery scenarios, particularly for creating modified copies of options
/// during fallback strategy execution.
/// </remarks>
public static class CompilationOptionsExtensions
{
    /// <summary>
    /// Creates a deep copy of the compilation options instance.
    /// This is useful for creating modified versions during fallback strategies
    /// without affecting the original options.
    /// </summary>
    /// <param name="options">The compilation options to clone.</param>
    /// <returns>A new <see cref="CompilationOptions"/> instance with the same values.</returns>
    /// <remarks>
    /// The clone operation creates a completely independent copy of the options,
    /// allowing fallback strategies to modify compilation parameters without
    /// affecting the original configuration. This ensures that multiple fallback
    /// attempts can be made with different option combinations.
    /// </remarks>
    public static CompilationOptions Clone(this CompilationOptions options)
    {
        return new CompilationOptions
        {
            OptimizationLevel = options.OptimizationLevel,
            FastMath = options.FastMath,
            AggressiveOptimizations = options.AggressiveOptimizations,
            StrictFloatingPoint = options.StrictFloatingPoint,
            CompilerBackend = options.CompilerBackend,
            ForceInterpretedMode = options.ForceInterpretedMode
        };
    }
}