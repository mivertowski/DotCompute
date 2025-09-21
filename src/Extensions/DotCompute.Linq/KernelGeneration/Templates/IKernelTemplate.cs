// <copyright file="IKernelTemplate.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using DotCompute.Abstractions.Kernels.Types;
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
    /// Gets the kernel language this template generates.
    DotCompute.Abstractions.Kernels.Types.KernelLanguage Language { get; }
    /// Gets the template name for identification.
    string Name { get; }
    /// Gets the template version.
    string Version { get; }
    /// Gets the supported kernel operation types.
    IReadOnlySet<string> SupportedOperations { get; }
    /// Generates kernel source code from the provided metadata and parameters.
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
    /// Validates whether the template can generate code for the given parameters.
    /// <param name="metadata">The kernel metadata to validate.</param>
    /// <param name="entryPoint">The kernel entry point to validate.</param>
    /// <returns>A validation result indicating success or failure with details.</returns>
    KernelTemplateValidationResult Validate(KernelMetadata metadata, KernelEntryPoint entryPoint);
    /// Estimates resource usage for the kernel generation.
    /// <param name="metadata">The kernel metadata.</param>
    /// <param name="entryPoint">The kernel entry point.</param>
    /// <param name="dataSize">The expected data size for estimation.</param>
    /// <returns>A resource usage estimate.</returns>
    ResourceUsageEstimate EstimateResourceUsage(
        long dataSize);
    /// Gets optimization suggestions for the given kernel configuration.
    /// <returns>A collection of optimization suggestions.</returns>
    IEnumerable<KernelOptimizationSuggestion> GetOptimizationSuggestions(
        KernelEntryPoint entryPoint);
    /// Checks if the template supports a specific operation type.
    /// <param name="operationType">The operation type to check.</param>
    /// <returns>True if the operation is supported; otherwise, false.</returns>
    bool SupportsOperation(string operationType);
}
/// Contains the result of kernel generation including source code and metadata.
public sealed class KernelGenerationResult
    /// Initializes a new instance of the <see cref="KernelGenerationResult"/> class.
    /// <param name="sourceCode">The generated kernel source code.</param>
    /// <param name="entryPointName">The name of the kernel entry point function.</param>
    public KernelGenerationResult(string sourceCode, string entryPointName)
    {
        SourceCode = sourceCode ?? throw new ArgumentNullException(nameof(sourceCode));
        EntryPointName = entryPointName ?? throw new ArgumentNullException(nameof(entryPointName));
        GeneratedAt = DateTimeOffset.UtcNow;
        Headers = [];
        CompilationFlags = [];
        Dependencies = [];
        Metadata = [];
    }
    /// Gets the generated kernel source code.
    public string SourceCode { get; }
    /// Gets the name of the kernel entry point function.
    public string EntryPointName { get; }
    /// Gets the timestamp when the code was generated.
    public DateTimeOffset GeneratedAt { get; }
    /// Gets the required header files or includes.
    public List<string> Headers { get; }
    /// Gets the recommended compilation flags.
    public List<string> CompilationFlags { get; }
    /// Gets the external dependencies required.
    public List<string> Dependencies { get; }
    /// Gets additional metadata about the generated code.
    public Dictionary<string, object> Metadata { get; }
    /// Gets or sets the estimated resource usage for the generated kernel.
    public ResourceUsageEstimate? EstimatedResourceUsage { get; set; }
    /// Gets or sets warnings generated during code generation.
    public List<string>? Warnings { get; set; }
/// Contains validation results for kernel template validation.
public sealed class KernelTemplateValidationResult
    /// Initializes a new instance of the <see cref="KernelTemplateValidationResult"/> class.
    /// <param name="isValid">Whether the validation passed.</param>
    public KernelTemplateValidationResult(bool isValid)
        IsValid = isValid;
        Errors = [];
        Warnings = [];
    /// Gets a value indicating whether the validation passed.
    public bool IsValid { get; }
    /// Gets the validation errors.
    public List<string> Errors { get; }
    /// Gets the validation warnings.
    public List<string> Warnings { get; }
    /// Adds a validation error.
    /// <param name="error">The error message.</param>
    public void AddError(string error)
        if (!string.IsNullOrWhiteSpace(error))
        {
            Errors.Add(error);
        }
    /// Adds a validation warning.
    /// <param name="warning">The warning message.</param>
    public void AddWarning(string warning)
        if (!string.IsNullOrWhiteSpace(warning))
            Warnings.Add(warning);
    /// Creates a successful validation result.
    /// <returns>A successful validation result.</returns>
    public static KernelTemplateValidationResult Success() => new(true);
    /// Creates a failed validation result with an error.
    /// <returns>A failed validation result.</returns>
    public static KernelTemplateValidationResult Failure(string error)
        var result = new KernelTemplateValidationResult(false);
        result.AddError(error);
        return result;
/// Contains options for kernel generation.
public sealed class KernelGenerationOptions
    /// Gets or sets a value indicating whether to generate debug information.
    public bool GenerateDebugInfo { get; set; }
    /// Gets or sets a value indicating whether to enable aggressive optimizations.
    public bool EnableAggressiveOptimizations { get; set; }
    /// Gets or sets the target compute capability for CUDA kernels.
    public string? TargetComputeCapability { get; set; }
    /// Gets or sets the maximum register count per thread.
    public int? MaxRegistersPerThread { get; set; }
    /// Gets or sets custom preprocessor definitions.
    public Dictionary<string, string>? PreprocessorDefinitions { get; set; }
    /// Gets or sets additional include directories.
    public List<string>? IncludeDirectories { get; set; }
    /// Gets or sets custom template variables.
    public Dictionary<string, object>? TemplateVariables { get; set; }
    /// Gets or sets a value indicating whether to enable warp shuffle operations.
    public bool EnableWarpShuffle { get; set; }
    /// Gets or sets a value indicating whether to enable atomic operations.
    public bool EnableAtomics { get; set; }
    /// Gets or sets a value indicating whether to use shared memory optimizations.
    public bool UseSharedMemory { get; set; }
/// Contains an optimization suggestion for kernel generation.
public sealed class KernelOptimizationSuggestion
    /// Initializes a new instance of the <see cref="KernelOptimizationSuggestion"/> class.
    /// <param name="type">The type of optimization.</param>
    /// <param name="description">The description of the suggestion.</param>
    /// <param name="impact">The expected performance impact.</param>
    public KernelOptimizationSuggestion(
        OptimizationType type,
        string description,
        PerformanceImpact impact)
        Type = type;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Impact = impact;
    /// Gets the type of optimization.
    public OptimizationType Type { get; }
    /// Gets the description of the suggestion.
    public string Description { get; }
    /// Gets the expected performance impact.
    public PerformanceImpact Impact { get; }
    /// Gets or sets additional parameters for the optimization.
    public Dictionary<string, object>? Parameters { get; set; }
/// Defines optimization types for kernel generation.
public enum OptimizationType
    /// Memory access pattern optimization.
    MemoryAccess,
    /// Work group size optimization.
    WorkGroupSize,
    /// Register usage optimization.
    RegisterUsage,
    /// Loop unrolling optimization.
    LoopUnrolling,
    /// Vectorization optimization.
    Vectorization,
    /// Shared memory usage optimization.
    SharedMemory
/// Defines the expected performance impact levels.
public enum PerformanceImpact
    /// Low performance impact (less than 10% improvement).
    Low,
    /// Medium performance impact (10-50% improvement).
    Medium,
    /// High performance impact (more than 50% improvement).
    High
