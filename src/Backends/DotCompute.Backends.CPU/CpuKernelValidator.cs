// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Execution;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Validation;
using DotCompute.Backends.CPU.Kernels.Models;

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
    /// <summary>
    /// Initializes a new instance of the CpuKernelValidator class.
    /// </summary>
    /// <param name="logger">The logger.</param>

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
            result.Issues.Add(new ValidationIssue(
                "CPU_VALIDATION_001",
                $"Validation failed: {ex.Message}\n{ex.StackTrace}",
                ValidationSeverity.Error));
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
    public async Task<RuntimeValidationResult> ValidateRuntimeExecutionAsync(
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
            await ValidateExecutionContextInternalAsync(context, result);

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

    private static Task ValidateKernelDefinitionAsync(KernelDefinition definition, CpuValidationResult result)
    {
        // Validate kernel name
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue(
                "CPU_KERNEL_001",
                "Kernel name cannot be null or empty",
                ValidationSeverity.Error));
        }

        // Validate entry point
        if (string.IsNullOrWhiteSpace(definition.EntryPoint))
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue(
                "CPU_KERNEL_002",
                "Kernel entry point cannot be null or empty",
                ValidationSeverity.Error));
        }

        // Validate kernel code
        if (definition.Code == null)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue(
                "CPU_KERNEL_003",
                "Kernel must have source code",
                ValidationSeverity.Error));
        }

        return Task.CompletedTask;
    }

    private static Task ValidateWorkDimensionsAsync(WorkDimensions workDimensions, CpuValidationResult result)
    {
        // Validate dimension values
        if (workDimensions.X <= 0 || workDimensions.Y <= 0 || workDimensions.Z <= 0)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue(
                "CPU_WORKDIM_001",
                "Work dimensions must be positive values",
                ValidationSeverity.Error));
        }

        // Check for excessive work items
        var totalWorkItems = workDimensions.X * workDimensions.Y * workDimensions.Z;
        if (totalWorkItems > int.MaxValue)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue(
                "CPU_WORKDIM_002",
                "Total work items exceeds maximum supported value",
                ValidationSeverity.Error));
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

        return Task.CompletedTask;
    }

    private Task ValidateCpuConstraintsAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        CpuValidationResult result)
    {
        // Validate against CPU capabilities
        if (definition.Metadata?.TryGetValue("RequiredFeatures", out var featuresObj) == true &&
            featuresObj is IEnumerable<string> requiredFeatures)
        {
            foreach (var feature in requiredFeatures)
            {
                if (!_cpuCapabilities.SupportedInstructionSets.Contains(feature))
                {
                    result.IsValid = false;
                    result.Issues.Add(new ValidationIssue(
                        "CPU_FEATURE_001",
                        $"Required CPU feature '{feature}' is not supported. Available features: {string.Join(", ", _cpuCapabilities.SupportedInstructionSets)}",
                        ValidationSeverity.Error));
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

        return Task.CompletedTask;
    }

    private static Task ValidateMemoryAccessAsync(KernelDefinition definition, CpuValidationResult result)
        // Validate memory access patterns
        // NOTE: KernelDefinition.Parameters property doesn't exist in current API
        // Parameter validation is handled by the kernel compiler and metadata
        // This validation would require extracting parameter info from metadata if needed

        => Task.CompletedTask;

    private static Task ValidateVectorizationCompatibilityAsync(KernelDefinition definition, CpuValidationResult result)
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
                result.Issues.Add(new ValidationIssue(
                    "CPU_VECTOR_001",
                    $"Vector width {vectorWidth} is outside supported range ({MinVectorWidth}-{MaxVectorWidth})",
                    ValidationSeverity.Warning));
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

        return Task.CompletedTask;
    }

    private static Task ValidateThreadSafetyAsync(KernelDefinition definition, CpuValidationResult result)
    {
        // Check for potential thread safety issues
        var hasSharedState = definition.Metadata?.TryGetValue("HasSharedState", out var sharedStateObj) == true &&
                             sharedStateObj is bool sharedState && sharedState;
        if (hasSharedState)
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

        return Task.CompletedTask;
    }

    private static Task ValidateResourceUsageAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        CpuValidationResult result)
    {
        // Estimate memory usage
        var estimatedMemoryUsage = EstimateMemoryUsage(definition, workDimensions);
        if (estimatedMemoryUsage > MaxMemoryAllocation)
        {
            result.Issues.Add(new ValidationIssue(
                "CPU_RESOURCE_001",
                $"Estimated memory usage ({estimatedMemoryUsage:N0} bytes) exceeds recommended limit. Consider reducing work dimensions or optimizing data structures",
                ValidationSeverity.Warning));
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

        return Task.CompletedTask;
    }

    // Execution plan validation methods

    private static Task ValidateVectorizationSettingsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
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

        return Task.CompletedTask;
    }

    private static Task ValidateParallelizationSettingsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
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

        return Task.CompletedTask;
    }

    private static Task ValidateMemoryOptimizationSettingsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
    {
        if (plan.MemoryOptimizations?.Contains("InvalidOptimization") == true)
        {
            result.Issues.Add("Invalid memory optimization specified");
        }

        return Task.CompletedTask;
    }

    private static Task ValidateCacheOptimizationSettingsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
    {
        if (plan.CacheOptimizations?.Contains("InvalidOptimization") == true)
        {
            result.Issues.Add("Invalid cache optimization specified");
        }

        return Task.CompletedTask;
    }

    private static Task ValidateResourceConstraintsAsync(KernelExecutionPlan plan, ExecutionPlanValidationResult result)
    {
        // Validate resource constraints are reasonable
        var estimatedMemory = plan.OptimalThreadCount * 1024 * 1024; // 1MB per thread estimate
        if (estimatedMemory > MaxMemoryAllocation)
        {
            result.Issues.Add($"Execution plan may exceed memory limits: {estimatedMemory:N0} bytes estimated");
        }

        return Task.CompletedTask;
    }

    // Argument validation methods

    private static Task ValidateArgumentCountAsync(KernelArguments arguments, KernelDefinition definition, ArgumentValidationResult result)
    {
        // NOTE: KernelDefinition.Parameters property doesn't exist in current API
        // Argument count validation is handled at the kernel execution level
        // We can check if arguments exist but can't validate count without parameter metadata
        if (arguments.Count == 0)
        {
            result.Issues.Add("No arguments provided for kernel execution");
        }
        return Task.CompletedTask;
    }

    private static Task ValidateArgumentTypesAsync(KernelArguments arguments, KernelDefinition definition, ArgumentValidationResult result)
    {
        // NOTE: KernelDefinition.Parameters property doesn't exist in current API
        // Type validation is performed by the kernel compiler based on metadata
        // We can only do basic null/existence checks here
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument == null)
            {
                result.Issues.Add($"Argument {i} is null");
            }
        }
        return Task.CompletedTask;
    }

    private static Task ValidateArgumentConstraintsAsync(KernelArguments arguments, KernelDefinition definition, ArgumentValidationResult result)
    {
        // NOTE: KernelDefinition.Parameters property doesn't exist in current API
        // Constraint validation would require parameter metadata which isn't available
        // Basic validation: check for empty arrays
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument is Array array && array.Length == 0)
            {
                result.Issues.Add($"Argument {i} is an empty array");
            }
        }
        return Task.CompletedTask;
    }

    private static Task ValidateMemoryAlignmentAsync(KernelArguments arguments, ArgumentValidationResult result)
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

        return Task.CompletedTask;
    }

    // Runtime validation methods

    /// <summary>
    /// Validates execution context for kernel execution (public overload).
    /// </summary>
    public async Task<RuntimeValidationResult> ValidateExecutionContextAsync(
        KernelExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);

        var result = new RuntimeValidationResult
        {
            KernelName = "ExecutionContext",
            ValidationTime = DateTimeOffset.UtcNow,
            IsValid = true
        };

        await ValidateExecutionContextInternalAsync(context, result);
        return result;
    }

    private static Task ValidateExecutionContextInternalAsync(KernelExecutionContext context, RuntimeValidationResult result)
    {
        if (context.WorkDimensions.X <= 0 || context.WorkDimensions.Y <= 0 || context.WorkDimensions.Z <= 0)
        {
            result.IsValid = false;
            result.Issues.Add("Invalid work dimensions in execution context");
        }

        if ((context.Buffers?.Count > 0 != true) && (context.Scalars?.Count > 0 != true))
        {
            result.Issues.Add("No arguments provided in execution context");
        }

        return Task.CompletedTask;
    }

    private static Task ValidateRaceConditionsAsync(KernelExecutionContext context, KernelDefinition definition, RuntimeValidationResult result)
    {
        // Check for potential race conditions in shared memory access
        var hasSharedState = definition.Metadata?.TryGetValue("HasSharedState", out var sharedStateObj) == true &&
                             sharedStateObj is bool sharedState && sharedState;
        if (hasSharedState && context.WorkDimensions.X * context.WorkDimensions.Y * context.WorkDimensions.Z > 1)
        {
            result.Issues.Add("Potential race condition: shared state access in parallel execution");
        }
        return Task.CompletedTask;
    }

    private static Task ValidateMemoryBoundsAsync(KernelExecutionContext context, RuntimeValidationResult result)
    {
        // Validate memory bounds for buffer arguments
        var totalWorkItems = context.WorkDimensions.X * context.WorkDimensions.Y * context.WorkDimensions.Z;

        foreach (var (index, buffer) in context.Buffers)
        {
            if (buffer.Length < totalWorkItems)
            {
                result.Issues.Add($"Buffer {index} size ({buffer.Length}) insufficient for work dimensions ({totalWorkItems})");
            }
        }

        return Task.CompletedTask;
    }

    private static Task ValidateStackUsageAsync(KernelExecutionContext context, RuntimeValidationResult result)
    {
        // Estimate stack usage based on work dimensions and recursion depth
        var estimatedStackDepth = EstimateStackDepth(context);
        if (estimatedStackDepth > MaxStackDepth)
        {
            result.Issues.Add($"Estimated stack depth ({estimatedStackDepth}) may cause stack overflow");
        }

        return Task.CompletedTask;
    }

    // Helper methods

    private static CpuCapabilities DetectCpuCapabilities()
    {
        var instructionSets = new HashSet<string>();

        // Detect available instruction sets (simplified)
        if (Vector.IsHardwareAccelerated)
        {
            _ = instructionSets.Add("SSE");
            _ = instructionSets.Add("AVX");
        }

        var capabilities = new CpuCapabilities
        {
            CoreCount = Environment.ProcessorCount,
            IsHardwareAccelerated = Vector.IsHardwareAccelerated,
            VectorWidth = Vector<float>.Count,
            SupportedInstructionSets = instructionSets
        };

        return capabilities;
    }

    private static long EstimateMemoryUsage(KernelDefinition definition, WorkDimensions workDimensions)
    {
        var totalWorkItems = workDimensions.X * workDimensions.Y * workDimensions.Z;
        // NOTE: KernelDefinition.Parameters doesn't exist, using default estimation
        var averageParameterSize = 64; // Default estimate per work item
        return (long)(totalWorkItems * averageParameterSize);
    }

    private static TimeSpan EstimateExecutionTime(KernelDefinition definition, WorkDimensions workDimensions)
    {
        var totalWorkItems = workDimensions.X * workDimensions.Y * workDimensions.Z;
        var estimatedTimePerWorkItem = 0.001; // 1ms per work item (rough estimate)
        return TimeSpan.FromMilliseconds(totalWorkItems * estimatedTimePerWorkItem);
    }

    private static bool IsTypeCompatible(Type argumentType, Type parameterType) => argumentType == parameterType || argumentType.IsAssignableTo(parameterType);

    private static void ValidateNumericRange(object argument, KernelParameter parameter, int index, ArgumentValidationResult result)
    {
        if (argument is IComparable comparable)
        {
            if (parameter.MinValue != null && comparable.CompareTo(parameter.MinValue) < 0)
            {
                result.IsValid = false;
                result.Issues.Add($"Argument {index} below minimum value: {argument} < {parameter.MinValue}");
            }

            if (parameter.MaxValue != null && comparable.CompareTo(parameter.MaxValue) > 0)
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
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
/// <summary>
/// A class that represents cpu capabilities.
/// </summary>

// Supporting classes and enums for CPU validation

public class CpuCapabilities
{
    /// <summary>
    /// Gets or sets the core count.
    /// </summary>
    /// <value>The core count.</value>
    public int CoreCount { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether hardware accelerated.
    /// </summary>
    /// <value>The is hardware accelerated.</value>
    public bool IsHardwareAccelerated { get; set; }
    /// <summary>
    /// Gets or sets the vector width.
    /// </summary>
    /// <value>The vector width.</value>
    public int VectorWidth { get; set; }
    /// <summary>
    /// Gets or sets the supported instruction sets.
    /// </summary>
    /// <value>The supported instruction sets.</value>
    public HashSet<string> SupportedInstructionSets { get; set; } = [];
}
/// <summary>
/// A class that represents cpu validation result.
/// </summary>

public class CpuValidationResult
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; set; }
    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public IList<ValidationIssue> Issues { get; } = [];
    /// <summary>
    /// Gets or sets the warnings.
    /// </summary>
    /// <value>The warnings.</value>
    public IList<ValidationWarning> Warnings { get; } = [];
}
/// <summary>
/// A class that represents execution plan validation result.
/// </summary>

public class ExecutionPlanValidationResult
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; set; }
    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public IList<string> Issues { get; } = [];
}
/// <summary>
/// A class that represents argument validation result.
/// </summary>

public class ArgumentValidationResult
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; set; }
    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public IList<string> Issues { get; } = [];
}
/// <summary>
/// A class that represents runtime validation result.
/// </summary>

public class RuntimeValidationResult
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; set; }
    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public IList<string> Issues { get; } = [];
}
/// <summary>
/// A class that represents validation warning.
/// </summary>

public class ValidationWarning
{
    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    /// <value>The category.</value>
    public required string Category { get; set; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public required string Message { get; set; }
    /// <summary>
    /// Gets or sets the impact.
    /// </summary>
    /// <value>The impact.</value>
    public required string Impact { get; set; }
}