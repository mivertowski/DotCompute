// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using DotCompute.Generators.Kernel.Enums;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Analyzes kernel attributes to extract configuration and backend specifications.
/// Processes [Kernel] attribute data to determine supported backends, optimization
/// settings, and execution parameters.
/// </summary>
/// <remarks>
/// This class specializes in extracting and interpreting kernel attribute data,
/// including backend flags, vector sizes, parallel execution settings, and
/// custom optimization parameters. It provides type-safe access to attribute
/// configuration data for code generation.
/// </remarks>
public sealed class KernelAttributeAnalyzer
{
    /// <summary>
    /// Analyzes a kernel attribute and extracts configuration data.
    /// </summary>
    /// <param name="kernelAttribute">The kernel attribute to analyze.</param>
    /// <returns>A configuration object containing extracted settings.</returns>
    /// <remarks>
    /// This method processes the kernel attribute to extract:
    /// - Supported backend accelerators
    /// - Vector size for SIMD operations
    /// - Parallel execution settings
    /// - Custom optimization flags
    /// - Memory access patterns
    /// </remarks>
    public static KernelConfiguration AnalyzeKernelConfiguration(AttributeData kernelAttribute)
    {
        var backends = ExtractSupportedBackends(kernelAttribute);
        var vectorSize = ExtractVectorSize(kernelAttribute);
        var isParallel = ExtractIsParallel(kernelAttribute);
        var optimizationLevel = ExtractOptimizationLevel(kernelAttribute);
        var memoryPattern = ExtractMemoryPattern(kernelAttribute);

        var configuration = new KernelConfiguration
        {
            VectorSize = vectorSize,
            IsParallel = isParallel,
            OptimizationLevel = optimizationLevel,
            MemoryPattern = memoryPattern
        };

        // Populate read-only collection
        foreach (var backend in backends)
        {
            configuration.SupportedBackends.Add(backend);
        }

        return configuration;
    }

    /// <summary>
    /// Extracts the list of supported backends from the kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>A list of supported backend names.</returns>
    /// <remarks>
    /// Backend support is determined by:
    /// - Explicit Backends flag parameter
    /// - Default backend selection (CPU always included)
    /// - Platform-specific availability checks
    /// </remarks>
    private static List<string> ExtractSupportedBackends(AttributeData attribute)
    {
        var backends = new List<string> { "CPU" }; // CPU is always supported

        // Look for Backends named argument
        var backendsArgument = GetNamedArgument(attribute, "Backends");
        if (backendsArgument.HasValue && backendsArgument.Value.Value is int backendsValue)
        {
            // Process backend flags (assuming bitwise flags)
            if ((backendsValue & 2) != 0) // CUDA flag
            {
                backends.Add("CUDA");
            }
            if ((backendsValue & 4) != 0) // Metal flag
            {
                backends.Add("Metal");
            }
            if ((backendsValue & 8) != 0) // OpenCL flag
            {
                backends.Add("OpenCL");
            }
            if ((backendsValue & 16) != 0) // Vulkan flag
            {
                backends.Add("Vulkan");
            }
            if ((backendsValue & 32) != 0) // ROCm flag
            {
                backends.Add("ROCm");
            }
        }

        return backends;
    }

    /// <summary>
    /// Extracts the vector size configuration from the kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The vector size for SIMD operations.</returns>
    /// <remarks>
    /// Vector size determines the width of SIMD operations:
    /// - 4: 128-bit vectors (SSE, NEON)
    /// - 8: 256-bit vectors (AVX2)
    /// - 16: 512-bit vectors (AVX-512)
    /// - 0: Auto-detect based on hardware
    /// </remarks>
    private static int ExtractVectorSize(AttributeData attribute)
    {
        var vectorSizeArgument = GetNamedArgument(attribute, "VectorSize");
        if (vectorSizeArgument.HasValue && vectorSizeArgument.Value.Value is int vectorSize)
        {
            return vectorSize;
        }
        return 8; // Default to 256-bit vectors (AVX2 compatible)
    }

    /// <summary>
    /// Extracts the parallel execution setting from the kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>True if parallel execution is enabled; otherwise, false.</returns>
    /// <remarks>
    /// Parallel execution settings control:
    /// - Multi-threading on CPU backend
    /// - Grid-stride loops on GPU backends
    /// - Work-group coordination strategies
    /// </remarks>
    private static bool ExtractIsParallel(AttributeData attribute)
    {
        var isParallelArgument = GetNamedArgument(attribute, "IsParallel");
        if (isParallelArgument.HasValue && isParallelArgument.Value.Value is bool isParallel)
        {
            return isParallel;
        }
        return true; // Default to parallel execution
    }

    /// <summary>
    /// Extracts the optimization level from the kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The optimization level setting.</returns>
    private static OptimizationLevel ExtractOptimizationLevel(AttributeData attribute)
    {
        var optimizationArgument = GetNamedArgument(attribute, "OptimizationLevel");
        if (optimizationArgument.HasValue && optimizationArgument.Value.Value is int optimization)
        {
            return optimization switch
            {
                0 => OptimizationLevel.None,
                1 => OptimizationLevel.Size,
                2 => OptimizationLevel.Speed,
                3 => OptimizationLevel.Maximum,
                _ => OptimizationLevel.Speed
            };
        }
        return OptimizationLevel.Speed; // Default optimization level
    }

    /// <summary>
    /// Extracts the memory access pattern from the kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The memory access pattern setting.</returns>
    private static MemoryAccessPattern ExtractMemoryPattern(AttributeData attribute)
    {
        var memoryPatternArgument = GetNamedArgument(attribute, "MemoryPattern");
        if (memoryPatternArgument.HasValue && memoryPatternArgument.Value.Value is int pattern)
        {
            return pattern switch
            {
                0 => MemoryAccessPattern.Sequential,
                1 => MemoryAccessPattern.Random,
                2 => MemoryAccessPattern.Sequential, // Map streaming to sequential
                3 => MemoryAccessPattern.Gather,
                4 => MemoryAccessPattern.Scatter,
                _ => MemoryAccessPattern.Sequential
            };
        }
        return MemoryAccessPattern.Sequential; // Default pattern
    }

    /// <summary>
    /// Extracts custom compilation options from the kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>A dictionary of custom compilation options.</returns>
    public static Dictionary<string, object> ExtractCustomOptions(AttributeData attribute)
    {
        var options = new Dictionary<string, object>();

        // Extract additional named arguments
        foreach (var namedArg in attribute.NamedArguments)
        {
            var key = namedArg.Key;
            var value = namedArg.Value.Value;

            if (value != null && !IsStandardOption(key))
            {
                options[key] = value;
            }
        }

        return options;
    }

    /// <summary>
    /// Validates that the kernel configuration is coherent and supported.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>A list of validation errors, or empty if valid.</returns>
    public static IReadOnlyList<string> ValidateConfiguration(KernelConfiguration configuration)
    {
        var errors = new List<string>();

        // Validate vector size
        if (configuration.VectorSize is < 0 or > 64)
        {
            errors.Add($"Invalid vector size: {configuration.VectorSize}. Must be between 0 and 64.");
        }

        // Validate backend compatibility
        if (configuration.SupportedBackends.Count == 0)
        {
            errors.Add("At least one backend must be supported.");
        }

        // Validate conflicting settings
        if (configuration.OptimizationLevel == OptimizationLevel.None && configuration.IsParallel)
        {
            errors.Add("Parallel execution is not compatible with optimization level 'None'.");
        }

        return errors;
    }

    /// <summary>
    /// Gets a named argument from an attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <param name="argumentName">The name of the argument to find.</param>
    /// <returns>The typed constant value if found; otherwise, null.</returns>
    private static TypedConstant? GetNamedArgument(AttributeData attribute, string argumentName)
    {
        var namedArg = attribute.NamedArguments.FirstOrDefault(a => a.Key == argumentName);
        return namedArg.Value;
    }

    /// <summary>
    /// Determines if an option name is a standard kernel option.
    /// </summary>
    /// <param name="optionName">The option name to check.</param>
    /// <returns>True if it's a standard option; otherwise, false.</returns>
    private static bool IsStandardOption(string optionName)
    {
        return optionName switch
        {
            "Backends" or "VectorSize" or "IsParallel" or
            "OptimizationLevel" or "MemoryPattern" => true,
            _ => false
        };
    }
}

/// <summary>
/// Contains kernel configuration extracted from attributes.
/// </summary>
public sealed class KernelConfiguration
{
    /// <summary>
    /// Gets or sets the list of supported backend accelerators.
    /// </summary>
    public IList<string> SupportedBackends { get; } = [];

    /// <summary>
    /// Gets or sets the vector size for SIMD operations.
    /// </summary>
    public int VectorSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether parallel execution is enabled.
    /// </summary>
    public bool IsParallel { get; set; }

    /// <summary>
    /// Gets or sets the optimization level.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; }

    /// <summary>
    /// Gets or sets the memory access pattern.
    /// </summary>
    public MemoryAccessPattern MemoryPattern { get; set; }
}

/// <summary>
/// Defines optimization levels for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization - fastest compilation.
    /// </summary>
    None = 0,

    /// <summary>
    /// Optimize for size.
    /// </summary>
    Size = 1,

    /// <summary>
    /// Optimize for speed (default).
    /// </summary>
    Speed = 2,

    /// <summary>
    /// Maximum optimization - may increase compilation time.
    /// </summary>
    Maximum = 3
}

