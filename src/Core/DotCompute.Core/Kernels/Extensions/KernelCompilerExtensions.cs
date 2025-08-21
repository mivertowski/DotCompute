// <copyright file="KernelCompilerExtensions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Kernels.Extensions;

/// <summary>
/// Extension methods for IKernelCompiler to provide compatibility with Core types.
/// Provides conversion utilities between Abstractions and Core compilation options.
/// </summary>
public static class KernelCompilerExtensions
{
    /// <summary>
    /// Converts Abstractions.CompilationOptions to Core.CompilationOptions.
    /// This method bridges the gap between the abstract interface and concrete implementation.
    /// </summary>
    /// <param name="options">The abstractions compilation options to convert.</param>
    /// <returns>Core compilation options with equivalent settings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public static Types.CompilationOptions ToCoreOptions(this DotCompute.Abstractions.CompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new Types.CompilationOptions
        {
            OptimizationLevel = ConvertOptimizationLevel(options.OptimizationLevel),
            GenerateDebugInfo = options.EnableDebugInfo,
            EnableFastMath = options.FastMath,
            EnableUnsafeOptimizations = false, // Default to safe optimizations
            AdditionalFlags = options.AdditionalFlags?.ToList() ?? [],
            Defines = options.Defines?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? []
        };
    }

    /// <summary>
    /// Converts the abstraction optimization level to core optimization level.
    /// Maps various optimization strategies to compiler-specific optimization flags.
    /// </summary>
    /// <param name="level">The abstraction optimization level.</param>
    /// <returns>The corresponding core optimization level.</returns>
    private static Types.OptimizationLevel ConvertOptimizationLevel(DotCompute.Abstractions.OptimizationLevel level)
    {
        return level switch
        {
            DotCompute.Abstractions.OptimizationLevel.None => Types.OptimizationLevel.O0,
            DotCompute.Abstractions.OptimizationLevel.Debug => Types.OptimizationLevel.O1,
            DotCompute.Abstractions.OptimizationLevel.Default => Types.OptimizationLevel.O2,
            DotCompute.Abstractions.OptimizationLevel.Release => Types.OptimizationLevel.O2,
            DotCompute.Abstractions.OptimizationLevel.Maximum => Types.OptimizationLevel.O3,
            DotCompute.Abstractions.OptimizationLevel.Aggressive => Types.OptimizationLevel.O3,
            _ => Types.OptimizationLevel.O2 // Default to standard optimization
        };
    }
}