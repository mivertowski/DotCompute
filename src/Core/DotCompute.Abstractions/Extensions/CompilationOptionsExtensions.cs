// <copyright file="CompilationOptionsExtensions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Extensions;

/// <summary>
/// Extension methods for <see cref="CompilationOptions"/>.
/// </summary>
public static class CompilationOptionsExtensions
{
    /// <summary>
    /// Creates a copy of the compilation options.
    /// </summary>
    /// <param name="options">The options to copy.</param>
    /// <returns>A new instance with the same values.</returns>
    public static CompilationOptions Clone(this CompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new CompilationOptions
        {
            OptimizationLevel = options.OptimizationLevel,
            EnableDebugInfo = options.EnableDebugInfo,
            EnableFastMath = options.EnableFastMath,
            AggressiveOptimizations = options.AggressiveOptimizations,
            TargetArchitecture = options.TargetArchitecture,
            Defines = new Dictionary<string, string>(options.Defines),
            IncludePaths = [.. options.IncludePaths],
            AdditionalFlags = [.. options.AdditionalFlags],
            CompilationTimeout = options.CompilationTimeout,
            TreatWarningsAsErrors = options.TreatWarningsAsErrors,
            WarningLevel = options.WarningLevel,
            EnableMemoryCoalescing = options.EnableMemoryCoalescing,
            EnableOperatorFusion = options.EnableOperatorFusion,
            EnableParallelExecution = options.EnableParallelExecution,
            EnableLoopUnrolling = options.EnableLoopUnrolling,
            EnableVectorization = options.EnableVectorization,
            EnableInlining = options.EnableInlining,
            MaxRegisters = options.MaxRegisters,
            SharedMemoryLimit = options.SharedMemoryLimit,
            ThreadBlockSize = options.ThreadBlockSize,
            PreferredBlockSize = options.PreferredBlockSize,
            SharedMemorySize = options.SharedMemorySize,
            UnrollLoops = options.UnrollLoops,
            UseNativeMathLibrary = options.UseNativeMathLibrary,
            FloatingPointMode = options.FloatingPointMode,
            EnableProfileGuidedOptimizations = options.EnableProfileGuidedOptimizations,
            ProfileDataPath = options.ProfileDataPath,
            StrictFloatingPoint = options.StrictFloatingPoint,
            CompilerBackend = options.CompilerBackend,
            ForceInterpretedMode = options.ForceInterpretedMode
        };
    }

    /// <summary>
    /// Applies default values for Native AOT compilation.
    /// </summary>
    /// <param name="options">The compilation options.</param>
    /// <returns>The modified options for method chaining.</returns>
    public static CompilationOptions WithNativeAot(this CompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.OptimizationLevel = OptimizationLevel.O3;
        options.AggressiveOptimizations = true;
        options.EnableInlining = true;
        options.UnrollLoops = true;
        options.EnableVectorization = true;


        return options;
    }

    /// <summary>
    /// Applies debug-friendly settings.
    /// </summary>
    /// <param name="options">The compilation options.</param>
    /// <returns>The modified options for method chaining.</returns>
    public static CompilationOptions WithDebugSettings(this CompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.OptimizationLevel = OptimizationLevel.None;
        options.EnableDebugInfo = true;
        options.AggressiveOptimizations = false;
        options.EnableFastMath = false;
        options.FloatingPointMode = FloatingPointMode.Strict;


        return options;
    }

    /// <summary>
    /// Applies performance-optimized settings.
    /// </summary>
    /// <param name="options">The compilation options.</param>
    /// <returns>The modified options for method chaining.</returns>
    public static CompilationOptions WithPerformanceSettings(this CompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.OptimizationLevel = OptimizationLevel.O3;
        options.EnableFastMath = true;
        options.AggressiveOptimizations = true;
        options.EnableVectorization = true;
        options.UnrollLoops = true;
        options.EnableInlining = true;
        options.FloatingPointMode = FloatingPointMode.Fast;


        return options;
    }

    /// <summary>
    /// Validates the compilation options.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool Validate(this CompilationOptions options)
    {
        if (options == null)
        {
            return false;
        }

        // Validate register limits
        if (options.MaxRegisters.HasValue && (options.MaxRegisters < 0 || options.MaxRegisters > 255))
        {
            return false;
        }

        // Validate shared memory size
        if (options.SharedMemorySize.HasValue && options.SharedMemorySize < 0)
        {
            return false;
        }

        // Validate timeout
        if (options.CompilationTimeout < TimeSpan.Zero)
        {
            return false;
        }

        // Validate thread block size
        if (options.ThreadBlockSize.HasValue && options.ThreadBlockSize < 1)
        {
            return false;
        }

        // Validate warning level
        if (options.WarningLevel < 0 || options.WarningLevel > 4)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Merges two compilation options, with the source overriding the target.
    /// </summary>
    /// <param name="target">The target options.</param>
    /// <param name="source">The source options to merge.</param>
    /// <returns>A new merged compilation options instance.</returns>
    public static CompilationOptions Merge(this CompilationOptions target, CompilationOptions source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        var merged = target.Clone();

        // Override with non-default values from source
        if (source.OptimizationLevel != OptimizationLevel.Default)
        {
            merged.OptimizationLevel = source.OptimizationLevel;
        }

        if (source.EnableDebugInfo)
        {
            merged.EnableDebugInfo = true;
        }

        if (source.FloatingPointMode != FloatingPointMode.Default)
        {
            merged.FloatingPointMode = source.FloatingPointMode;
        }

        // Merge collections
        if (source.AdditionalFlags != null)
        {
            merged.AdditionalFlags.AddRange(source.AdditionalFlags);
        }

        if (source.Defines != null)
        {
            foreach (var kvp in source.Defines)
            {
                merged.Defines[kvp.Key] = kvp.Value;
            }
        }

        if (source.IncludePaths != null)
        {
            merged.IncludePaths.AddRange(source.IncludePaths);
        }

        return merged;
    }
}