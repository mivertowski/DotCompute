// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Execution;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Validation;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Provides CPU-specific validation and verification for kernel execution.
/// Ensures kernel correctness, safety, and compatibility with CPU architecture.
/// </summary>
internal sealed class CpuKernelValidator : IDisposable
{
    private readonly ILogger _logger;
    private readonly CpuCapabilities _cpuCapabilities;
    private bool _disposed;

    // CPU validation constraints
    private const int MaxStackDepth = 1000;
    private const long MaxMemoryAllocation = 1L << 30; // 1GB
    private const int MaxThreadCount = 1024;
    private const int MinVectorWidth = 2;
    private const int MaxVectorWidth = 64;

    public CpuKernelValidator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cpuCapabilities = DetectCpuCapabilities();

        _logger.LogDebug("CpuKernelValidator initialized with CPU capabilities: {capabilities}",
            string.Join(", ", _cpuCapabilities.SupportedInstructionSets));
    }

    /// <summary>
    /// Validates a kernel definition for CPU execution compatibility.
    /// </summary>
    public async Task<CpuValidationResult> ValidateKernelAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(definition);

        _logger.LogDebug("Validating kernel {kernelName} for CPU execution", definition.Name);

        var result = new CpuValidationResult
        {
            KernelName = definition.Name,
            ValidationTime = DateTimeOffset.UtcNow,
            IsValid = true
        };

        try
        {
            // Basic kernel validation
            await ValidateKernelDefinitionAsync(definition, result);

            // Work dimensions validation
            await ValidateWorkDimensionsAsync(workDimensions, result);

            // CPU-specific constraints validation
            await ValidateCpuConstraintsAsync(definition, workDimensions, result);

            // Memory access validation
            await ValidateMemoryAccessAsync(definition, result);

            // Vectorization compatibility validation
            await ValidateVectorizationCompatibilityAsync(definition, result);

            // Thread safety validation
            await ValidateThreadSafetyAsync(definition, result);

            // Resource usage validation
            await ValidateResourceUsageAsync(definition, workDimensions, result);

            _logger.LogDebug("Kernel validation completed for {kernelName}: Valid={isValid}, Issues={issueCount}",
                definition.Name, result.IsValid, result.Issues.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kernel validation failed for {kernelName}", definition.Name);
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = "ValidationError",
                Message = $"Validation failed: {ex.Message}",
                Details = ex.StackTrace
            });
            return result;
        }
    }

    /// <summary>
    /// Validates an execution plan for CPU-specific requirements.
    /// </summary>
    public async Task<ExecutionPlanValidationResult> ValidateExecutionPlanAsync(
        KernelExecutionPlan executionPlan,
        KernelDefinition definition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(executionPlan);
        ArgumentNullException.ThrowIfNull(definition);

        _logger.LogDebug("Validating execution plan for kernel {kernelName}", definition.Name);

        var result = new ExecutionPlanValidationResult
        {
            KernelName = definition.Name,
            ValidationTime = DateTimeOffset.UtcNow,
            IsValid = true
        };

        try
        {
            // Validate vectorization settings
            await ValidateVectorizationSettingsAsync(executionPlan, result);

            // Validate parallelization settings
            await ValidateParallelizationSettingsAsync(executionPlan, result);

            // Validate memory optimization settings
            await ValidateMemoryOptimizationSettingsAsync(executionPlan, result);

            // Validate cache optimization settings
            await ValidateCacheOptimizationSettingsAsync(executionPlan, result);

            // Validate resource constraints
            await ValidateResourceConstraintsAsync(executionPlan, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execution plan validation failed for {kernelName}", definition.Name);
            result.IsValid = false;
            result.Issues.Add($"Execution plan validation failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Validates kernel arguments for type safety and constraints.
    /// </summary>
    public async Task<ArgumentValidationResult> ValidateKernelArgumentsAsync(
        KernelArguments arguments,
        KernelDefinition definition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(definition);

        _logger.LogDebug("Validating kernel arguments for {kernelName}", definition.Name);

        var result = new ArgumentValidationResult
        {
            KernelName = definition.Name,
            ValidationTime = DateTimeOffset.UtcNow,
            IsValid = true
        };

        try
        {
            // Validate argument count
            await ValidateArgumentCountAsync(arguments, definition, result);

            // Validate argument types
            await ValidateArgumentTypesAsync(arguments, definition, result);

            // Validate argument sizes and constraints
            await ValidateArgumentConstraintsAsync(arguments, definition, result);

            // Validate memory alignment requirements
            await ValidateMemoryAlignmentAsync(arguments, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Argument validation failed for {kernelName}", definition.Name);
            result.IsValid = false;
            result.Issues.Add($"Argument validation failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Performs runtime validation during kernel execution.
    /// </summary>
    public async Task<RuntimeValidationResult> ValidateRuntimeExecution(
        KernelExecutionContext context,
        KernelDefinition definition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(definition);

        var result = new RuntimeValidationResult
        {
            KernelName = definition.Name,
            ValidationTime = DateTimeOffset.UtcNow,
            IsValid = true
        };

        try
        {
            // Validate execution context
            await ValidateExecutionContextAsync(context, result);

            // Check for potential race conditions
            await ValidateRaceConditionsAsync(context, definition, result);

            // Validate memory bounds
            await ValidateMemoryBoundsAsync(context, result);

            // Check for stack overflow potential
            await ValidateStackUsageAsync(context, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Runtime validation failed for {kernelName}", definition.Name);
            result.IsValid = false;
            result.Issues.Add($"Runtime validation failed: {ex.Message}");
            return result;
        }
    }

    // Private validation methods

    private async Task ValidateKernelDefinitionAsync(KernelDefinition definition, CpuValidationResult result)
    {
        // Validate kernel name
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = "KernelDefinition",
                Message = "Kernel name cannot be null or empty"
            });
        }

        // Validate entry point
        if (string.IsNullOrWhiteSpace(definition.EntryPoint))
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = "KernelDefinition",
                Message = "Kernel entry point cannot be null or empty"
            });
        }

        // Validate kernel code or delegate
        if (definition.Code == null && definition.CompiledDelegate == null)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = "KernelDefinition",
                Message = "Kernel must have either source code or compiled delegate"
            });
        }
    }

    private async Task ValidateWorkDimensionsAsync(WorkDimensions workDimensions, CpuValidationResult result)
    {
        // Validate dimension values
        if (workDimensions.X <= 0 || workDimensions.Y <= 0 || workDimensions.Z <= 0)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = "WorkDimensions",
                Message = "Work dimensions must be positive values"
            });
        }

        // Check for excessive work items
        var totalWorkItems = workDimensions.X * workDimensions.Y * workDimensions.Z;
        if (totalWorkItems > int.MaxValue)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = "WorkDimensions",
                Message = "Total work items exceeds maximum supported value"
            });
        }

        // Warn about very large workloads
        if (totalWorkItems > 100_000_000)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Category = "Performance",
                Message = "Very large workload detected - consider chunking for better performance",
                Impact = "May cause memory pressure or excessive execution time"
            });
        }
    }

    private async Task ValidateCpuConstraintsAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        CpuValidationResult result)
    {
        // Validate against CPU capabilities
        if (definition.RequiredFeatures != null)
        {
            foreach (var feature in definition.RequiredFeatures)
            {
                if (!_cpuCapabilities.SupportedInstructionSets.Contains(feature))
                {
                    result.IsValid = false;
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Category = "CpuCompatibility",
                        Message = $"Required CPU feature '{feature}' is not supported",
                        Details = $"Available features: {string.Join(", ", _cpuCapabilities.SupportedInstructionSets)}"
                    });
                }
            }
        }

        // Validate thread requirements
        var estimatedThreads = Math.Min(Environment.ProcessorCount, (int)Math.Ceiling(workDimensions.X / 100.0));
        if (estimatedThreads > MaxThreadCount)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Category = "Threading",
                Message = $"Estimated thread count ({estimatedThreads}) exceeds recommended maximum ({MaxThreadCount})",
                Impact = "May cause thread pool exhaustion"
            });
        }
    }

    private async Task ValidateMemoryAccessAsync(KernelDefinition definition, CpuValidationResult result)
    {
        // Validate memory access patterns
        if (definition.Parameters != null)
        {
            foreach (var param in definition.Parameters)
            {
                if (param.IsPointer && !param.IsReadOnly)
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        Category = "MemoryAccess",
                        Message = $"Parameter '{param.Name}' allows mutable pointer access",
                        Impact = "Potential for data races in multi-threaded execution"
                    });
                }

                // Check for very large arrays
                if (param.Size > MaxMemoryAllocation)
                {
                    result.IsValid = false;
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Category = "MemoryAccess",
                        Message = $"Parameter '{param.Name}' size exceeds maximum allowed ({MaxMemoryAllocation} bytes)"
                    });
                }
            }
        }
    }

    private async Task ValidateVectorizationCompatibilityAsync(KernelDefinition definition, CpuValidationResult result)
    {
        // Check if vectorization is requested but not supported
        if (definition.Metadata?.ContainsKey("UseVectorization") == true &&
            !Vector.IsHardwareAccelerated)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Category = "Vectorization",
                Message = "Vectorization requested but hardware acceleration is not available",
                Impact = "Will fall back to scalar execution"
            });
        }

        // Validate vector width compatibility
        if (definition.Metadata?.TryGetValue("VectorWidth", out var vectorWidthObj) == true &&
            vectorWidthObj is int vectorWidth)
        {
            if (vectorWidth < MinVectorWidth || vectorWidth > MaxVectorWidth)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Category = "Vectorization",
                    Message = $"Vector width {vectorWidth} is outside supported range ({MinVectorWidth}-{MaxVectorWidth})"
                });
            }

            if (vectorWidth > Vector<float>.Count)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Category = "Vectorization",
                    Message = $"Requested vector width ({vectorWidth}) exceeds platform capacity ({Vector<float>.Count})",
                    Impact = "Will use platform maximum vector width"
                });
            }
        }
    }

    private static async Task ValidateThreadSafetyAsync(KernelDefinition definition, CpuValidationResult result)
    {
        // Check for potential thread safety issues
        if (definition.HasSharedState)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Category = "ThreadSafety",
                Message = "Kernel uses shared state which may cause race conditions",
                Impact = "Ensure proper synchronization in multi-threaded execution"
            });
        }

        // Check for non-reentrant operations
        if (definition.Metadata?.ContainsKey("IsReentrant") == true &&
            definition.Metadata["IsReentrant"] is bool isReentrant && !isReentrant)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Category = "ThreadSafety",
                Message = "Kernel is not reentrant - ensure serialized execution",
                Impact = "May require single-threaded execution mode"
            });
        }
    }

    private async Task ValidateResourceUsageAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        CpuValidationResult result)
    {
        // Estimate memory usage
        var estimatedMemoryUsage = EstimateMemoryUsage(definition, workDimensions);
        if (estimatedMemoryUsage > MaxMemoryAllocation)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Category = "ResourceUsage",
                Message = $"Estimated memory usage ({estimatedMemoryUsage:N0} bytes) exceeds recommended limit",
                Details = "Consider reducing work dimensions or optimizing data structures"
            });
        }

        // Estimate execution time
        var estimatedExecutionTime = EstimateExecutionTime(definition, workDimensions);
        if (estimatedExecutionTime > TimeSpan.FromMinutes(5))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Category = "Performance",
                Message = $"Estimated execution time ({estimatedExecutionTime:g}) is very long",
                Impact = "Consider chunking the workload or optimizing the algorithm"
            });
        }
    }

    // Execution plan validation methods

    private static async Task ValidateVectorizationSettingsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
    {
        if (plan.UseVectorization)
        {
            if (!Vector.IsHardwareAccelerated)
            {
                result.Issues.Add("Vectorization enabled but hardware acceleration not available");
            }

            if (plan.VectorizationFactor <= 0)
            {
                result.IsValid = false;
                result.Issues.Add("Vectorization factor must be positive");
            }

            if (plan.VectorWidth <= 0 || plan.VectorWidth > Vector<float>.Count)
            {
                result.Issues.Add($"Invalid vector width: {plan.VectorWidth} (max: {Vector<float>.Count})");
            }
        }
    }

    private static async Task ValidateParallelizationSettingsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
    {
        if (plan.UseParallelization)
        {
            if (plan.OptimalThreadCount <= 0)
            {
                result.IsValid = false;
                result.Issues.Add("Thread count must be positive");
            }

            if (plan.OptimalThreadCount > Environment.ProcessorCount * 2)
            {
                result.Issues.Add($"Thread count ({plan.OptimalThreadCount}) significantly exceeds CPU core count ({Environment.ProcessorCount})");
            }
        }
    }

    private static async Task ValidateMemoryOptimizationSettingsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
    {
        if (plan.MemoryOptimizations?.Contains("InvalidOptimization") == true)
        {
            result.Issues.Add("Invalid memory optimization specified");
        }
    }

    private static async Task ValidateCacheOptimizationSettingsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
    {
        if (plan.CacheOptimizations?.Contains("InvalidOptimization") == true)
        {
            result.Issues.Add("Invalid cache optimization specified");
        }
    }

    private static async Task ValidateResourceConstraintsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
    {
        // Validate resource constraints are reasonable
        var estimatedMemory = plan.OptimalThreadCount * 1024 * 1024; // 1MB per thread estimate
        if (estimatedMemory > MaxMemoryAllocation)
        {
            result.Issues.Add($"Execution plan may exceed memory limits: {estimatedMemory:N0} bytes estimated");
        }
    }

    // Argument validation methods

    private static async Task ValidateArgumentCountAsync(KernelArguments arguments, KernelDefinition definition, ArgumentValidationResult result)
    {
        var expectedCount = definition.Parameters?.Count ?? 0;
        if (arguments.Count != expectedCount)
        {
            result.IsValid = false;
            result.Issues.Add($"Argument count mismatch: expected {expectedCount}, got {arguments.Count}");
        }
    }

    private async Task ValidateArgumentTypesAsync(KernelArguments arguments, KernelDefinition definition, ArgumentValidationResult result)
    {
        if (definition.Parameters == null)
        {
            return;
        }


        for (var i = 0; i < Math.Min(arguments.Count, definition.Parameters.Count); i++)
        {
            var argument = arguments[i];
            var parameter = definition.Parameters[i];

            if (!IsTypeCompatible(argument.GetType(), parameter.Type))
            {
                result.IsValid = false;
                result.Issues.Add($"Argument {i} type mismatch: expected {parameter.Type}, got {argument.GetType()}");
            }
        }
    }

    private async Task ValidateArgumentConstraintsAsync(KernelArguments arguments, KernelDefinition definition, ArgumentValidationResult result)
    {
        if (definition.Parameters == null)
        {
            return;
        }


        for (var i = 0; i < Math.Min(arguments.Count, definition.Parameters.Count); i++)
        {
            var argument = arguments[i];
            var parameter = definition.Parameters[i];

            // Validate array sizes
            if (argument is Array array && parameter.MinSize > 0)
            {
                if (array.Length < parameter.MinSize)
                {
                    result.IsValid = false;
                    result.Issues.Add($"Argument {i} array too small: {array.Length} < {parameter.MinSize}");
                }
            }

            // Validate numeric ranges
            if (parameter.MinValue.HasValue || parameter.MaxValue.HasValue)
            {
                ValidateNumericRange(argument, parameter, i, result);
            }
        }
    }

    private static async Task ValidateMemoryAlignmentAsync(KernelArguments arguments, ArgumentValidationResult result)
    {
        // Check for proper memory alignment for vectorization
        foreach (var (arg, index) in arguments.Select((arg, i) => (arg, i)))
        {
            if (arg is Array array && Vector.IsHardwareAccelerated)
            {
                // Check if array is properly aligned for SIMD operations
                // This is a simplified check - real implementation would check actual memory alignment
                if (array.Length % Vector<float>.Count != 0)
                {
                    result.Issues.Add($"Argument {index} may not be optimally aligned for vectorization");
                }
            }
        }
    }

    // Runtime validation methods

    private static async Task ValidateExecutionContextAsync(KernelExecutionContext context, RuntimeValidationResult result)
    {
        if (context.WorkDimensions.X <= 0 || context.WorkDimensions.Y <= 0 || context.WorkDimensions.Z <= 0)
        {
            result.IsValid = false;
            result.Issues.Add("Invalid work dimensions in execution context");
        }

        if (context.Arguments?.Any() != true)
        {
            result.Issues.Add("No arguments provided in execution context");
        }
    }

    private static async Task ValidateRaceConditionsAsync(KernelExecutionContext context, KernelDefinition definition, RuntimeValidationResult result)
    {
        // Check for potential race conditions in shared memory access
        if (definition.HasSharedState && context.WorkDimensions.X * context.WorkDimensions.Y * context.WorkDimensions.Z > 1)
        {
            result.Issues.Add("Potential race condition: shared state access in parallel execution");
        }
    }

    private static async Task ValidateMemoryBoundsAsync(KernelExecutionContext context, RuntimeValidationResult result)
    {
        // Validate memory bounds for array arguments
        foreach (var (arg, index) in context.Arguments.Select((arg, i) => (arg, i)))
        {
            if (arg is Array array)
            {
                var totalWorkItems = context.WorkDimensions.X * context.WorkDimensions.Y * context.WorkDimensions.Z;
                if (array.Length < totalWorkItems)
                {
                    result.Issues.Add($"Argument {index} array size ({array.Length}) insufficient for work dimensions ({totalWorkItems})");
                }
            }
        }
    }

    private static async Task ValidateStackUsageAsync(KernelExecutionContext context, RuntimeValidationResult result)
    {
        // Estimate stack usage based on work dimensions and recursion depth
        var estimatedStackDepth = EstimateStackDepth(context);
        if (estimatedStackDepth > MaxStackDepth)
        {
            result.Issues.Add($"Estimated stack depth ({estimatedStackDepth}) may cause stack overflow");
        }
    }

    // Helper methods

    private static CpuCapabilities DetectCpuCapabilities()
    {
        var capabilities = new CpuCapabilities
        {
            CoreCount = Environment.ProcessorCount,
            IsHardwareAccelerated = Vector.IsHardwareAccelerated,
            VectorWidth = Vector<float>.Count,
            SupportedInstructionSets = new HashSet<string>()
        };

        // Detect available instruction sets (simplified)
        if (Vector.IsHardwareAccelerated)
        {
            capabilities.SupportedInstructionSets.Add("SSE");
            capabilities.SupportedInstructionSets.Add("AVX");
        }

        return capabilities;
    }

    private static long EstimateMemoryUsage(KernelDefinition definition, WorkDimensions workDimensions)
    {
        var totalWorkItems = workDimensions.X * workDimensions.Y * workDimensions.Z;
        var averageParameterSize = definition.Parameters?.Average(p => p.Size) ?? 64;
        return (long)(totalWorkItems * averageParameterSize);
    }

    private static TimeSpan EstimateExecutionTime(KernelDefinition definition, WorkDimensions workDimensions)
    {
        var totalWorkItems = workDimensions.X * workDimensions.Y * workDimensions.Z;
        var estimatedTimePerWorkItem = 0.001; // 1ms per work item (rough estimate)
        return TimeSpan.FromMilliseconds(totalWorkItems * estimatedTimePerWorkItem);
    }

    private static bool IsTypeCompatible(Type argumentType, Type parameterType)
    {
        return argumentType == parameterType || argumentType.IsAssignableTo(parameterType);
    }

    private static void ValidateNumericRange(object argument, KernelParameter parameter, int index, ArgumentValidationResult result)
    {
        if (argument is IComparable comparable)
        {
            if (parameter.MinValue.HasValue && comparable.CompareTo(parameter.MinValue.Value) < 0)
            {
                result.IsValid = false;
                result.Issues.Add($"Argument {index} below minimum value: {argument} < {parameter.MinValue}");
            }

            if (parameter.MaxValue.HasValue && comparable.CompareTo(parameter.MaxValue.Value) > 0)
            {
                result.IsValid = false;
                result.Issues.Add($"Argument {index} above maximum value: {argument} > {parameter.MaxValue}");
            }
        }
    }

    private static int EstimateStackDepth(KernelExecutionContext context)
    {
        // Simplified stack depth estimation
        var totalWorkItems = context.WorkDimensions.X * context.WorkDimensions.Y * context.WorkDimensions.Z;
        return (int)Math.Min(totalWorkItems / 1000, MaxStackDepth);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

// Supporting classes and enums for CPU validation

public class CpuCapabilities
{
    public int CoreCount { get; set; }
    public bool IsHardwareAccelerated { get; set; }
    public int VectorWidth { get; set; }
    public HashSet<string> SupportedInstructionSets { get; set; } = new();
}

public class CpuValidationResult
{
    public required string KernelName { get; set; }
    public DateTimeOffset ValidationTime { get; set; }
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
}

public class ExecutionPlanValidationResult
{
    public required string KernelName { get; set; }
    public DateTimeOffset ValidationTime { get; set; }
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class ArgumentValidationResult
{
    public required string KernelName { get; set; }
    public DateTimeOffset ValidationTime { get; set; }
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class RuntimeValidationResult
{
    public required string KernelName { get; set; }
    public DateTimeOffset ValidationTime { get; set; }
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class ValidationWarning
{
    public required string Category { get; set; }
    public required string Message { get; set; }
    public required string Impact { get; set; }
}