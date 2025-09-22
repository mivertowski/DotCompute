// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Validation;
using DotCompute.Core.Kernels.Compilation;
using DotCompute.Core.Kernels.Validation;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using AbstractionsICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

// Using aliases to resolve ValidationIssue conflicts
using CoreValidationIssue = DotCompute.Abstractions.ValidationIssue;
using DebugValidationIssue = DotCompute.Abstractions.Debugging.DebugValidationIssue;
using ValidationValidationIssue = DotCompute.Abstractions.Validation.ValidationIssue;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Base abstract class for kernel compiler implementations, consolidating common patterns.
/// This addresses the critical issue of 15+ duplicate compiler implementations.
/// </summary>
public abstract class BaseKernelCompiler : IUnifiedKernelCompiler
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, AbstractionsICompiledKernel> _compilationCache;
    private readonly ConcurrentDictionary<string, CompilationMetrics> _metricsCache;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AbstractionsICompiledKernel>> _compilationTasks;


    /// <summary>
    /// Initializes a new instance of the <see cref="BaseKernelCompiler"/> class.
    /// </summary>
    protected BaseKernelCompiler(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _compilationCache = new ConcurrentDictionary<string, AbstractionsICompiledKernel>();
        _metricsCache = new ConcurrentDictionary<string, CompilationMetrics>();
        _compilationTasks = new ConcurrentDictionary<string, TaskCompletionSource<AbstractionsICompiledKernel>>();
    }


    /// <summary>
    /// Gets the compiler name for logging purposes.
    /// </summary>
    protected abstract string CompilerName { get; }


    /// <summary>
    /// Gets whether compilation caching is enabled.
    /// </summary>
    protected virtual bool EnableCaching => true;

    /// <inheritdoc/>
    public virtual string Name => CompilerName;

    /// <inheritdoc/>
    public abstract IReadOnlyList<KernelLanguage> SupportedSourceTypes { get; }

    /// <inheritdoc/>
    public virtual IReadOnlyDictionary<string, object> Capabilities { get; } = new Dictionary<string, object>
    {
        ["SupportsAsync"] = true,
        ["SupportsCaching"] = true,
        ["SupportsOptimization"] = true
    };

    /// <inheritdoc/>
    public virtual async ValueTask<AbstractionsICompiledKernel> CompileAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // Validate kernel definition

        var validationResult = ValidateKernelDefinition(definition);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Kernel validation failed: {validationResult.ErrorMessage}");
        }


        options ??= GetDefaultCompilationOptions();

        // Check cache if enabled

        var cacheKey = GenerateCacheKey(definition, options);
        if (EnableCaching && _compilationCache.TryGetValue(cacheKey, out var cachedKernel))
        {
            _logger.LogDebugMessage($"{CompilerName}: Using cached compilation for kernel '{definition.Name}'");
            return cachedKernel;
        }

        // Check if compilation is already in progress for this kernel

        if (EnableCaching)
        {
            var tcs = new TaskCompletionSource<AbstractionsICompiledKernel>();
            if (!_compilationTasks.TryAdd(cacheKey, tcs))
            {
                // Another thread is already compiling this kernel, wait for it
                if (_compilationTasks.TryGetValue(cacheKey, out var existingTcs))
                {
                    _logger.LogDebugMessage($"{CompilerName}: Waiting for concurrent compilation of kernel '{definition.Name}'");
                    return await existingTcs.Task.ConfigureAwait(false);
                }
            }
        }


        _logger.LogDebugMessage($"{CompilerName}: Starting compilation of kernel '{definition.Name}'");


        var stopwatch = Stopwatch.StartNew();


        try
        {
            // Perform the actual compilation
            var compiledKernel = await CompileKernelCoreAsync(definition, options, cancellationToken)
                .ConfigureAwait(false);


            stopwatch.Stop();

            // Record metrics

            var metrics = new CompilationMetrics
            {
                KernelName = definition.Name,
                CompilationTime = stopwatch.Elapsed,
                OptimizationLevel = options.OptimizationLevel,
                CacheHit = false,
                Timestamp = DateTime.UtcNow
            };
            _ = _metricsCache.TryAdd(cacheKey, metrics);

            // Cache the result if enabled

            if (EnableCaching)
            {
                _ = _compilationCache.TryAdd(cacheKey, compiledKernel);

                // Signal waiting threads

                if (_compilationTasks.TryRemove(cacheKey, out var tcs))
                {
                    tcs.SetResult(compiledKernel);
                }
            }


            _logger.LogInformation(
                "{CompilerName}: Successfully compiled kernel '{KernelName}' in {ElapsedMs}ms with {OptimizationLevel} optimization",
                CompilerName, definition.Name, stopwatch.ElapsedMilliseconds, options.OptimizationLevel);


            return compiledKernel;
        }
        catch (Exception ex)
        {
            // Remove the task on failure and propagate the exception
            if (EnableCaching && _compilationTasks.TryRemove(cacheKey, out var tcs))
            {
                tcs.SetException(ex);
            }


            if (ex is OperationCanceledException)
            {
                throw;
            }


            _logger.LogErrorMessage(ex, $"{CompilerName}: Failed to compile kernel '{definition.Name}'");
            throw new KernelCompilationException($"Failed to compile kernel '{definition.Name}'", ex);
        }
    }


    /// <summary>
    /// Core compilation logic to be implemented by derived classes.
    /// </summary>
    protected abstract ValueTask<AbstractionsICompiledKernel> CompileKernelCoreAsync(
        KernelDefinition definition,
        CompilationOptions options,
        CancellationToken cancellationToken);


    /// <summary>
    /// Validates kernel definition parameters.
    /// Common validation logic that was duplicated across implementations.
    /// </summary>
    protected virtual DotCompute.Abstractions.Validation.UnifiedValidationResult ValidateKernelDefinition(KernelDefinition definition)
    {
        var result = new DotCompute.Abstractions.Validation.UnifiedValidationResult();


        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            result.AddError("Kernel name cannot be empty");
            return result;
        }


        if (definition.Code == null || definition.Code.Length == 0)
        {
            result.AddError("Kernel code cannot be null or empty");
            return result;
        }

        // Validate work dimensions if available

        if (definition.Metadata?.TryGetValue("WorkDimensions", out var workDimsObj) == true)
        {
            if (workDimsObj is int workDimensions && (workDimensions < 1 || workDimensions > 3))
            {
                result.AddError("Work dimensions must be between 1 and 3");
                return result;
            }
        }

        // Validate parameters if available

        if (definition.Metadata?.TryGetValue("Parameters", out var paramsObj) == true)
        {
            if (paramsObj is IList<object> parameters && parameters.Count == 0)
            {
                result.AddError("Kernel must have at least one parameter");
                return result;
            }
        }

        // Additional validation can be added by derived classes

        return AdditionalValidation(definition);
    }


    /// <summary>
    /// Hook for derived classes to add additional validation.
    /// </summary>
    protected virtual DotCompute.Abstractions.Validation.UnifiedValidationResult AdditionalValidation(KernelDefinition definition) => DotCompute.Abstractions.Validation.UnifiedValidationResult.Success();


    /// <summary>
    /// Generates a cache key for the compilation.
    /// </summary>
    protected virtual string GenerateCacheKey(KernelDefinition definition, CompilationOptions options)
    {
        // Use kernel name, code hash, and optimization level for cache key
        var codeHash = definition.Code != null

            ? BitConverter.ToString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(definition.Code))).Replace("-", "")
            : "empty";


        return $"{definition.Name}_{codeHash}_{options.OptimizationLevel}_{CompilerName}";
    }


    /// <summary>
    /// Gets default compilation options.
    /// </summary>
    protected virtual CompilationOptions GetDefaultCompilationOptions()
    {
        return new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            EnableDebugInfo = false,
            EnableProfileGuidedOptimizations = false,
            MaxRegisters = null
        };
    }


    /// <summary>
    /// Clears the compilation cache.
    /// </summary>
    public virtual void ClearCache()
    {
        _compilationCache.Clear();
        _metricsCache.Clear();
        _compilationTasks.Clear();
        _logger.LogDebugMessage("{CompilerName}: Compilation cache cleared");
    }


    /// <summary>
    /// Gets compilation metrics for analysis.
    /// </summary>
    public virtual IReadOnlyDictionary<string, CompilationMetrics> GetMetrics() => _metricsCache;


    /// <summary>
    /// Logs performance metrics for a compilation.
    /// </summary>
    protected void LogCompilationMetrics(string kernelName, TimeSpan compilationTime, long? byteCodeSize = null)
    {
        _logger.LogDebug(
            "{CompilerName}: Kernel '{KernelName}' compilation metrics: Time={CompilationTime}ms, Size={ByteCodeSize}",
            CompilerName,
            kernelName,
            compilationTime.TotalMilliseconds,
            byteCodeSize?.ToString() ?? "N/A");
    }


    /// <summary>
    /// Enriches kernel definition with compilation metadata.
    /// </summary>
    protected virtual KernelDefinition EnrichDefinitionWithMetadata(
        KernelDefinition definition,
        Dictionary<string, object> additionalMetadata)
    {
        var metadata = definition.Metadata ?? [];

        // Add compilation metadata

        metadata["Compiler"] = CompilerName;
        metadata["CompilationTimestamp"] = DateTime.UtcNow;

        // Add additional metadata from derived compiler

        foreach (var kvp in additionalMetadata)
        {
            metadata[kvp.Key] = kvp.Value;
        }

        // Create new definition with enriched metadata

        return new KernelDefinition(definition.Name, definition.Code ?? string.Empty, definition.EntryPoint)
        {
            Metadata = metadata
        };
    }

    /// <summary>
    /// Converts an IKernelSource to a KernelDefinition for processing.
    /// </summary>
    /// <param name="source">The kernel source to convert.</param>
    /// <returns>A KernelDefinition equivalent.</returns>
    protected virtual KernelDefinition ConvertToKernelDefinition(IKernelSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // If it's already a KernelDefinition, return as-is
        if (source is KernelDefinition kernelDef)
        {

            return kernelDef;
        }

        // Try to extract basic information from the source

        var name = source.GetType().Name;
        var code = source.ToString() ?? string.Empty;
        var entryPoint = "main"; // Default entry point

        // Use reflection to get properties if available
        var sourceType = source.GetType();
        var nameProperty = sourceType.GetProperty("Name");
        if (nameProperty?.GetValue(source) is string sourceName)
        {
            name = sourceName;
        }

        var codeProperty = sourceType.GetProperty("Code") ?? sourceType.GetProperty("Source");
        if (codeProperty?.GetValue(source) is string sourceCode)
        {
            code = sourceCode;
        }

        var entryProperty = sourceType.GetProperty("EntryPoint");
        if (entryProperty?.GetValue(source) is string sourceEntry)
        {
            entryPoint = sourceEntry;
        }


        return new KernelDefinition(name, code, entryPoint);
    }

    public async Task<ManagedCompiledKernel> CompileAsync(IKernelSource source, CompilationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            // Convert IKernelSource to KernelDefinition if possible
            var kernelDef = ConvertToKernelDefinition(source);

            // Use the main compilation method
            var compiled = await CompileAsync(kernelDef, options, cancellationToken);

            // Create ManagedCompiledKernel wrapper
            return new ManagedCompiledKernel
            {
                Name = kernelDef.Name,
                Binary = Array.Empty<byte>(), // Implementation-specific
                Parameters = Array.Empty<KernelParameter>() // No parameters for this kernel type
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile kernel from IKernelSource: {SourceType}", source.GetType().Name);
            throw;
        }
    }
    public async Task<KernelValidationResult> ValidateAsync(IKernelSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            // Convert IKernelSource to KernelDefinition
            var kernelDef = ConvertToKernelDefinition(source);

            // Use the main validation method
            var result = await ValidateAsync(kernelDef, cancellationToken);

            // Convert UnifiedValidationResult to KernelValidationResult
            return new KernelValidationResult
            {
                IsValid = result.IsValid,
                Errors = result.Errors
                    .Select(e => new DotCompute.Abstractions.Validation.ValidationIssue(DotCompute.Abstractions.Validation.ValidationSeverity.Error, e.Message, e.Code))
                    .ToList(),
                Warnings = result.Warnings
                    .Select(w => new DotCompute.Abstractions.Validation.ValidationWarning
                    {
                        Code = w.Code ?? "UNKNOWN",
                        Message = w.Message,
                        Severity = DotCompute.Abstractions.Validation.WarningSeverity.Medium
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate kernel from IKernelSource: {SourceType}", source.GetType().Name);

            return new KernelValidationResult
            {
                IsValid = false,
                Errors = [new DotCompute.Abstractions.Validation.ValidationIssue(DotCompute.Abstractions.Validation.ValidationSeverity.Error, $"Validation failed: {ex.Message}", "VALIDATION_ERROR")]
            };
        }
    }
    /// <inheritdoc/>
    public virtual DotCompute.Abstractions.Validation.UnifiedValidationResult Validate(KernelDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Use the existing validation logic

        return ValidateKernelDefinition(source);
    }


    /// <inheritdoc/>
    public virtual async ValueTask<DotCompute.Abstractions.Validation.UnifiedValidationResult> ValidateAsync(
        KernelDefinition source,
        CancellationToken cancellationToken = default)
        // For base implementation, use synchronous validation
        // Derived classes can override for async validation


        => await ValueTask.FromResult(Validate(source));


    /// <inheritdoc/>
    public virtual async ValueTask<AbstractionsICompiledKernel> OptimizeAsync(
        AbstractionsICompiledKernel kernel,
        OptimizationLevel level,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);


        _logger.LogDebugMessage($"{CompilerName}: Optimizing kernel '{kernel.Name}' at level {level}");

        // Base implementation returns the same kernel
        // Derived classes should override for actual optimization

        return await OptimizeKernelCore(kernel, level, cancellationToken);
    }


    /// <summary>
    /// Core optimization logic to be implemented by derived classes.
    /// </summary>
    protected virtual ValueTask<AbstractionsICompiledKernel> OptimizeKernelCore(
        AbstractionsICompiledKernel kernel,
        OptimizationLevel level,
        CancellationToken cancellationToken)
        // Default: no optimization


        => ValueTask.FromResult(kernel);

    // Backward compatibility methods for legacy IKernelCompiler interface

    /// <inheritdoc />
    public virtual async Task<AbstractionsICompiledKernel> CompileAsync(
        KernelDefinition kernelDefinition,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernelDefinition);
        ArgumentNullException.ThrowIfNull(accelerator);

        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Full,
            EnableDebugInfo = false,
            TargetArchitecture = accelerator.Info.DeviceType
        };

        return await CompileAsync(kernelDefinition, options, cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task<bool> CanCompileAsync(KernelDefinition kernelDefinition, IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(kernelDefinition);
        ArgumentNullException.ThrowIfNull(accelerator);

        try
        {
            var validationResult = Validate(kernelDefinition);
            return Task.FromResult(validationResult.IsValid);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public virtual CompilationOptions GetSupportedOptions(IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);

        return new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Full,
            EnableDebugInfo = false,
            TargetArchitecture = accelerator.Info.DeviceType,
            AllowUnsafeCode = true,
            PreferredBlockSize = new Dim3(256, 1, 1),
            SharedMemorySize = 48 * 1024 // 48KB default shared memory
        };
    }

    /// <inheritdoc />
    public virtual async Task<IDictionary<string, AbstractionsICompiledKernel>> BatchCompileAsync(
        IEnumerable<KernelDefinition> kernelDefinitions,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernelDefinitions);
        ArgumentNullException.ThrowIfNull(accelerator);

        var results = new Dictionary<string, AbstractionsICompiledKernel>();

        foreach (var kernelDef in kernelDefinitions)
        {
            try
            {
                var compiled = await CompileAsync(kernelDef, accelerator, cancellationToken);
                results[kernelDef.Name] = compiled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile kernel {KernelName} in batch operation", kernelDef.Name);
                throw new KernelCompilationException($"Batch compilation failed for kernel {kernelDef.Name}: {ex.Message}", ex);
            }
        }

        return results;
    }
}

/// <summary>
/// Represents compilation metrics for performance analysis.
/// </summary>
public record CompilationMetrics
{
    public required string KernelName { get; init; }
    public required TimeSpan CompilationTime { get; init; }
    public required OptimizationLevel OptimizationLevel { get; init; }
    public required bool CacheHit { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Exception thrown when kernel compilation fails.
/// </summary>
public class KernelCompilationException : Exception
{
    public KernelCompilationException(string message) : base(message) { }
    public KernelCompilationException(string message, Exception innerException) : base(message, innerException) { }
    public KernelCompilationException()
    {
    }
}
