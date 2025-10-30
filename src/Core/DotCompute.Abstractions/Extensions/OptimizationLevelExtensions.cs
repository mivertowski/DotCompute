// <copyright file="OptimizationLevelExtensions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Extensions;

/// <summary>
/// Extension methods for OptimizationLevel enum providing compiler-specific mappings.
/// </summary>
public static class OptimizationLevelExtensions
{
    /// <summary>
    /// Converts OptimizationLevel to GCC/Clang compiler flags.
    /// </summary>
    /// <param name="level">The optimization level to convert.</param>
    /// <returns>The corresponding GCC/Clang optimization flag.</returns>
    public static string ToGccFlag(this OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None => "-O0",
            OptimizationLevel.O1 => "-O1",
            OptimizationLevel.O2 or OptimizationLevel.Default => "-O2",
            OptimizationLevel.O3 => "-O3",
            OptimizationLevel.Size => "-Os",
            _ => "-O2" // Default fallback
        };
    }

    /// <summary>
    /// Converts OptimizationLevel to MSVC compiler flags.
    /// </summary>
    /// <param name="level">The optimization level to convert.</param>
    /// <returns>The corresponding MSVC optimization flag.</returns>
    public static string ToMsvcFlag(this OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None => "/Od",
            OptimizationLevel.O1 => "/O1",
            OptimizationLevel.O2 or OptimizationLevel.Default => "/O2",
            OptimizationLevel.O3 => "/Ox",
            OptimizationLevel.Size => "/O1", // MSVC uses /O1 for size optimization
            _ => "/O2" // Default fallback
        };
    }

    /// <summary>
    /// Converts OptimizationLevel to NVCC compiler flags.
    /// </summary>
    /// <param name="level">The optimization level to convert.</param>
    /// <returns>The corresponding NVCC optimization flag.</returns>
    public static string ToNvccFlag(this OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None => "-O0",
            OptimizationLevel.O1 => "-O1",
            OptimizationLevel.O2 or OptimizationLevel.Default => "-O2",
            OptimizationLevel.O3 => "-O3",
            OptimizationLevel.Size => "-O2", // NVCC doesn't have -Os, use -O2
            _ => "-O2" // Default fallback
        };
    }

    /// <summary>
    /// Converts OptimizationLevel to LLVM opt passes.
    /// </summary>
    /// <param name="level">The optimization level to convert.</param>
    /// <returns>The corresponding LLVM optimization pass specification.</returns>
    public static string ToLlvmPasses(this OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None => "-O0",
            OptimizationLevel.O1 => "-O1",
            OptimizationLevel.O2 or OptimizationLevel.Default => "-O2",
            OptimizationLevel.O3 => "-O3",
            OptimizationLevel.Size => "-Os",
            _ => "-O2" // Default fallback
        };
    }

    /// <summary>
    /// Gets the relative compilation time cost for the optimization level.
    /// </summary>
    /// <param name="level">The optimization level.</param>
    /// <returns>A relative compilation time multiplier (1.0 = baseline).</returns>
    public static double GetCompilationTimeCost(this OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None => 1.0,
            OptimizationLevel.O1 => 1.2,
            OptimizationLevel.O2 or OptimizationLevel.Default => 1.8,
            OptimizationLevel.O3 => 3.5,
            OptimizationLevel.Size => 1.5,
            _ => 1.8 // Default fallback
        };
    }

    /// <summary>
    /// Gets the expected performance improvement factor for the optimization level.
    /// </summary>
    /// <param name="level">The optimization level.</param>
    /// <returns>A relative performance multiplier (1.0 = baseline unoptimized performance).</returns>
    public static double GetPerformanceImprovement(this OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None => 1.0,
            OptimizationLevel.O1 => 1.3,
            OptimizationLevel.O2 or OptimizationLevel.Default => 2.1,
            OptimizationLevel.O3 => 2.8,
            OptimizationLevel.Size => 1.2, // Size optimization may reduce performance slightly
            _ => 2.1 // Default fallback
        };
    }

    /// <summary>
    /// Determines if the optimization level is suitable for debugging.
    /// </summary>
    /// <param name="level">The optimization level.</param>
    /// <returns>True if the level maintains good debuggability, false otherwise.</returns>
    public static bool IsDebuggingFriendly(this OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None => true,
            OptimizationLevel.O1 => true,
            OptimizationLevel.O2 or OptimizationLevel.Default => false,
            OptimizationLevel.O3 => false,
            OptimizationLevel.Size => false,
            _ => false
        };
    }

    /// <summary>
    /// Gets a human-readable description of the optimization level.
    /// </summary>
    /// <param name="level">The optimization level.</param>
    /// <returns>A descriptive string explaining the optimization level's characteristics.</returns>
    public static string GetDescription(this OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None =>
                "No optimization - fastest compilation, best for debugging",
            OptimizationLevel.O1 =>
                "Basic optimization - minimal performance improvements with fast compilation",
            OptimizationLevel.O2 or OptimizationLevel.Default =>
                "Standard optimization - good performance with reasonable compilation time (recommended)",
            OptimizationLevel.O3 =>
                "Maximum optimization - best performance, longest compilation time",
            OptimizationLevel.Size =>
                "Size optimization - smallest code size, moderate performance",
            _ => "Unknown optimization level"
        };
    }
}
