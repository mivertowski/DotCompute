// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;

namespace DotCompute.Consolidation;

/// <summary>
/// Mapping of duplicate classes to their canonical locations for consolidation purposes.
/// This file serves as documentation and reference for the duplicate consolidation process.
/// </summary>
public static class DuplicateClassMapping
{
    /// <summary>
    /// Maps old namespace.class to new canonical namespace.class
    /// Key: Old location, Value: Canonical location
    /// </summary>
    public static Dictionary<string, string> ClassMappings = new()
    {
        // CompilationOptions consolidation
        ["DotCompute.Backends.CUDA.Configuration.CompilationOptions"] = "DotCompute.Abstractions.CompilationOptions",

        // MemoryUsageStats consolidation
        ["DotCompute.Core.Pipelines.Optimization.Models.FusedKernelStage.MemoryUsageStats"] = "DotCompute.Abstractions.Pipelines.Results.MemoryUsageStats",

        // AcceleratorCompilationStats consolidation
        ["DotCompute.Abstractions.Interfaces.Services.AcceleratorCompilationStats"] = "DotCompute.Runtime.Services.Statistics.AcceleratorCompilationStats",
        ["DotCompute.Runtime.Services.AcceleratorCompilationStats"] = "DotCompute.Runtime.Services.Statistics.AcceleratorCompilationStats",

        // KernelValidationResult consolidation
        ["DotCompute.Abstractions.Debugging.KernelValidationResult"] = "DotCompute.Abstractions.Validation.KernelValidationResult",
        ["DotCompute.Runtime.Services.Statistics.KernelValidationResult"] = "DotCompute.Abstractions.Validation.KernelValidationResult",
        ["DotCompute.Runtime.Services.Types.KernelValidationResult"] = "DotCompute.Abstractions.Validation.KernelValidationResult",
        ["DotCompute.Runtime.Services.KernelValidationResult"] = "DotCompute.Abstractions.Validation.KernelValidationResult",
        ["DotCompute.Core.Kernels.Validation.KernelValidationResult"] = "DotCompute.Abstractions.Validation.KernelValidationResult"
    };

    /// <summary>
    /// Files to be deleted after consolidation
    /// </summary>
    public static List<string> FilesToDelete = new()
    {
        // Remove obsolete compilation options file (only obsolete alias remains)
        // Note: Keep CudaCompilationOptions, only remove obsolete CompilationOptions alias

        // Remove duplicate MemoryUsageStats from FusedKernelStage (will be inlined)

        // Remove duplicate AcceleratorCompilationStats files
        "/src/Core/DotCompute.Abstractions/Interfaces/Services/IKernelServices.cs:AcceleratorCompilationStats",
        "/src/Runtime/DotCompute.Runtime/Services/IKernelServices.cs:AcceleratorCompilationStats",

        // Remove duplicate KernelValidationResult files
        "/src/Runtime/DotCompute.Runtime/Services/Statistics/KernelValidationResult.cs",
        "/src/Runtime/DotCompute.Runtime/Services/Types/KernelValidationResult.cs"
    };

    /// <summary>
    /// Property name mappings for classes that have inconsistent property names
    /// Key: OldClassName.PropertyName, Value: NewPropertyName
    /// </summary>
    public static Dictionary<string, string> PropertyMappings = new()
    {
        // MemoryUsageStats property standardization
        ["MemoryUsageStats.AllocatedBytes"] = "TotalAllocatedBytes",
        ["MemoryUsageStats.PeakBytes"] = "PeakMemoryUsageBytes"
    };

    /// <summary>
    /// Namespace mappings for using directives
    /// Key: Old namespace, Value: New namespace
    /// </summary>
    public static Dictionary<string, string> NamespaceMappings = new()
    {
        ["DotCompute.Backends.CUDA.Configuration"] = "DotCompute.Abstractions",
        ["DotCompute.Core.Pipelines.Optimization.Models"] = "DotCompute.Abstractions.Pipelines.Results",
        ["DotCompute.Core.Kernels.Validation"] = "DotCompute.Abstractions.Validation",
        ["DotCompute.Runtime.Services.Statistics"] = "DotCompute.Abstractions.Validation",  // For KernelValidationResult
        ["DotCompute.Runtime.Services.Types"] = "DotCompute.Abstractions.Validation"       // For KernelValidationResult
    };

    /// <summary>
    /// Consolidation priority order (1 = highest priority)
    /// </summary>
    public static Dictionary<string, int> ConsolidationPriority = new()
    {
        ["CompilationOptions"] = 1,        // Critical for compilation
        ["MemoryUsageStats"] = 2,          // Used in multiple pipeline components
        ["AcceleratorCompilationStats"] = 3, // Runtime statistics
        ["KernelValidationResult"] = 4,    // Debugging and validation
        ["CompiledKernel"] = 5            // Interface consolidation (lowest priority)
    };
}

/// <summary>
/// Consolidation status tracking
/// </summary>
public static class ConsolidationStatus
{
    public static Dictionary<string, ConsolidationState> ClassStatus = new()
    {
        ["CompilationOptions"] = ConsolidationState.Pending,
        ["MemoryUsageStats"] = ConsolidationState.Pending,
        ["AcceleratorCompilationStats"] = ConsolidationState.Pending,
        ["KernelValidationResult"] = ConsolidationState.Pending,
        ["CompiledKernel"] = ConsolidationState.NotRequired
    };
}

/// <summary>
/// Consolidation state enumeration
/// </summary>
public enum ConsolidationState
{
    Pending,
    InProgress,
    Completed,
    NotRequired,
    Failed
}