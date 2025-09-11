// <copyright file="IKernelTemplate.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.Types;

namespace DotCompute.Linq.KernelGeneration.Templates;

/// <summary>
/// Defines the contract for kernel template implementations that generate
/// backend-specific kernel source code from high-level descriptions.
/// </summary>
public interface IKernelTemplate
{
    /// <summary>
    /// Gets the target backend type for this template.
    /// </summary>
    BackendType TargetBackend { get; }

    /// <summary>
    /// Gets the kernel language this template generates.
    /// </summary>
    KernelLanguage Language { get; }

    /// <summary>
    /// Gets the template name for identification.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the template version.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the supported kernel operation types.
    /// </summary>
    IReadOnlySet<string> SupportedOperations { get; }

    /// <summary>
    /// Generates kernel source code from the provided metadata and parameters.
    /// </summary>
    /// <param name="metadata">The kernel metadata containing compilation hints.</param>
    /// <param name="entryPoint">The kernel entry point definition.</param>
    /// <param name="options">Additional generation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the generated kernel source code.</returns>
    Task<KernelGenerationResult> GenerateAsync(
        KernelMetadata metadata,
        KernelEntryPoint entryPoint,
        KernelGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the template can generate code for the given parameters.
    /// </summary>
    /// <param name="metadata">The kernel metadata to validate.</param>
    /// <param name="entryPoint">The kernel entry point to validate.</param>
    /// <returns>A validation result indicating success or failure with details.</returns>
    KernelTemplateValidationResult Validate(KernelMetadata metadata, KernelEntryPoint entryPoint);

    /// <summary>
    /// Estimates resource usage for the kernel generation.
    /// </summary>
    /// <param name="metadata">The kernel metadata.</param>
    /// <param name="entryPoint">The kernel entry point.</param>
    /// <param name="dataSize">The expected data size for estimation.</param>
    /// <returns>A resource usage estimate.</returns>
    ResourceUsageEstimate EstimateResourceUsage(
        KernelMetadata metadata,
        KernelEntryPoint entryPoint,
        long dataSize);

    /// <summary>
    /// Gets optimization suggestions for the given kernel configuration.
    /// </summary>
    /// <param name="metadata">The kernel metadata.</param>
    /// <param name="entryPoint">The kernel entry point.</param>
    /// <returns>A collection of optimization suggestions.</returns>
    IEnumerable<KernelOptimizationSuggestion> GetOptimizationSuggestions(
        KernelMetadata metadata,
        KernelEntryPoint entryPoint);

    /// <summary>
    /// Checks if the template supports a specific operation type.
    /// </summary>
    /// <param name="operationType">The operation type to check.</param>
    /// <returns>True if the operation is supported; otherwise, false.</returns>
    bool SupportsOperation(string operationType);
}

/// <summary>
/// Contains the result of kernel generation including source code and metadata.
/// </summary>
public sealed class KernelGenerationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelGenerationResult"/> class.
    /// </summary>
    /// <param name="sourceCode">The generated kernel source code.</param>
    /// <param name="entryPointName">The name of the kernel entry point function.</param>
    public KernelGenerationResult(string sourceCode, string entryPointName)
    {
        SourceCode = sourceCode ?? throw new ArgumentNullException(nameof(sourceCode));
        EntryPointName = entryPointName ?? throw new ArgumentNullException(nameof(entryPointName));
        GeneratedAt = DateTimeOffset.UtcNow;
        Headers = new List<string>();
        CompilationFlags = new List<string>();
        Dependencies = new List<string>();
        Metadata = new Dictionary<string, object>();
    }

    /// <summary>
    /// Gets the generated kernel source code.
    /// </summary>
    public string SourceCode { get; }

    /// <summary>
    /// Gets the name of the kernel entry point function.
    /// </summary>
    public string EntryPointName { get; }

    /// <summary>
    /// Gets the timestamp when the code was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; }

    /// <summary>
    /// Gets the required header files or includes.
    /// </summary>
    public List<string> Headers { get; }

    /// <summary>
    /// Gets the recommended compilation flags.
    /// </summary>
    public List<string> CompilationFlags { get; }

    /// <summary>
    /// Gets the external dependencies required.
    /// </summary>
    public List<string> Dependencies { get; }

    /// <summary>
    /// Gets additional metadata about the generated code.
    /// </summary>
    public Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Gets or sets the estimated resource usage for the generated kernel.
    /// </summary>
    public ResourceUsageEstimate? EstimatedResourceUsage { get; set; }

    /// <summary>
    /// Gets or sets warnings generated during code generation.
    /// </summary>
    public List<string>? Warnings { get; set; }
}

/// <summary>
/// Contains validation results for kernel template validation.
/// </summary>
public sealed class KernelTemplateValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelTemplateValidationResult"/> class.
    /// </summary>
    /// <param name="isValid">Whether the validation passed.</param>
    public KernelTemplateValidationResult(bool isValid)
    {
        IsValid = isValid;
        Errors = new List<string>();
        Warnings = new List<string>();
    }

    /// <summary>
    /// Gets a value indicating whether the validation passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public List<string> Errors { get; }

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public List<string> Warnings { get; }

    /// <summary>
    /// Adds a validation error.
    /// </summary>
    /// <param name="error">The error message.</param>
    public void AddError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            Errors.Add(error);
    }

    /// <summary>
    /// Adds a validation warning.
    /// </summary>
    /// <param name="warning">The warning message.</param>
    public void AddWarning(string warning)
    {
        if (!string.IsNullOrWhiteSpace(warning))
            Warnings.Add(warning);
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A successful validation result.</returns>
    public static KernelTemplateValidationResult Success() => new(true);

    /// <summary>
    /// Creates a failed validation result with an error.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed validation result.</returns>
    public static KernelTemplateValidationResult Failure(string error)
    {
        var result = new KernelTemplateValidationResult(false);
        result.AddError(error);
        return result;
    }
}

/// <summary>
/// Contains options for kernel generation.
/// </summary>
public sealed class KernelGenerationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to generate debug information.
    /// </summary>
    public bool GenerateDebugInfo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable aggressive optimizations.
    /// </summary>
    public bool EnableAggressiveOptimizations { get; set; }

    /// <summary>
    /// Gets or sets the target compute capability for CUDA kernels.
    /// </summary>
    public string? TargetComputeCapability { get; set; }

    /// <summary>
    /// Gets or sets the maximum register count per thread.
    /// </summary>
    public int? MaxRegistersPerThread { get; set; }

    /// <summary>
    /// Gets or sets custom preprocessor definitions.
    /// </summary>
    public Dictionary<string, string>? PreprocessorDefinitions { get; set; }

    /// <summary>
    /// Gets or sets additional include directories.
    /// </summary>
    public List<string>? IncludeDirectories { get; set; }

    /// <summary>
    /// Gets or sets custom template variables.
    /// </summary>
    public Dictionary<string, object>? TemplateVariables { get; set; }
}

/// <summary>
/// Contains an optimization suggestion for kernel generation.
/// </summary>
public sealed class KernelOptimizationSuggestion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelOptimizationSuggestion"/> class.
    /// </summary>
    /// <param name="type">The type of optimization.</param>
    /// <param name="description">The description of the suggestion.</param>
    /// <param name="impact">The expected performance impact.</param>
    public KernelOptimizationSuggestion(
        OptimizationType type,
        string description,
        PerformanceImpact impact)
    {
        Type = type;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Impact = impact;
    }

    /// <summary>
    /// Gets the type of optimization.
    /// </summary>
    public OptimizationType Type { get; }

    /// <summary>
    /// Gets the description of the suggestion.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the expected performance impact.
    /// </summary>
    public PerformanceImpact Impact { get; }

    /// <summary>
    /// Gets or sets additional parameters for the optimization.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Defines optimization types for kernel generation.
/// </summary>
public enum OptimizationType
{
    /// <summary>
    /// Memory access pattern optimization.
    /// </summary>
    MemoryAccess,

    /// <summary>
    /// Work group size optimization.
    /// </summary>
    WorkGroupSize,

    /// <summary>
    /// Register usage optimization.
    /// </summary>
    RegisterUsage,

    /// <summary>
    /// Loop unrolling optimization.
    /// </summary>
    LoopUnrolling,

    /// <summary>
    /// Vectorization optimization.
    /// </summary>
    Vectorization,

    /// <summary>
    /// Shared memory usage optimization.
    /// </summary>
    SharedMemory
}

/// <summary>
/// Defines the expected performance impact levels.
/// </summary>
public enum PerformanceImpact
{
    /// <summary>
    /// Low performance impact (less than 10% improvement).
    /// </summary>
    Low,

    /// <summary>
    /// Medium performance impact (10-50% improvement).
    /// </summary>
    Medium,

    /// <summary>
    /// High performance impact (more than 50% improvement).
    /// </summary>
    High
}