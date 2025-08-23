// <copyright file="KernelCompilerExtensions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Enums;

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
    public static Types.CompilationOptions ToCoreOptions(this DotCompute.Abstractions.Configuration.CompilationOptions options)
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
    private static OptimizationLevel ConvertOptimizationLevel(DotCompute.Abstractions.Enums.OptimizationLevel level)
    {
        return level switch
        {
            DotCompute.Abstractions.Enums.OptimizationLevel.None => OptimizationLevel.None,
            DotCompute.Abstractions.Enums.OptimizationLevel.Minimal => OptimizationLevel.Minimal,
            DotCompute.Abstractions.Enums.OptimizationLevel.Default => OptimizationLevel.Default,
            DotCompute.Abstractions.Enums.OptimizationLevel.High => OptimizationLevel.High,
            DotCompute.Abstractions.Enums.OptimizationLevel.Maximum => OptimizationLevel.Maximum,
            DotCompute.Abstractions.Enums.OptimizationLevel.Size => OptimizationLevel.Size,
            DotCompute.Abstractions.Enums.OptimizationLevel.Aggressive => OptimizationLevel.Aggressive,
            DotCompute.Abstractions.Enums.OptimizationLevel.Custom => OptimizationLevel.Custom,
            _ => OptimizationLevel.Default // Default to standard optimization
        };
    }
}