// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Enums;
using DotCompute.Abstractions.Kernels;
using DotCompute.Core.Kernels.Compilation;
using DotCompute.Core.Kernels.Validation;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Base abstract class for kernel compiler implementations, consolidating common patterns.
/// This addresses the critical issue of 15+ duplicate compiler implementations.
/// </summary>
public abstract class BaseKernelCompiler : IKernelCompiler
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, ICompiledKernel> _compilationCache;
    private readonly ConcurrentDictionary<string, CompilationMetrics> _metricsCache;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseKernelCompiler"/> class.
    /// </summary>
    protected BaseKernelCompiler(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _compilationCache = new ConcurrentDictionary<string, ICompiledKernel>();
        _metricsCache = new ConcurrentDictionary<string, CompilationMetrics>();
    }
    
    /// <summary>
    /// Gets the compiler name for logging purposes.
    /// </summary>
    protected abstract string CompilerName { get; }
    
    /// <summary>
    /// Gets whether compilation caching is enabled.
    /// </summary>
    protected virtual bool EnableCaching => true;

    public IReadOnlyList<KernelSourceType> SupportedSourceTypes => throw new NotImplementedException();

    public IReadOnlyDictionary<string, object> Capabilities => throw new NotImplementedException();

    /// <inheritdoc/>
    public virtual async ValueTask<ICompiledKernel> CompileAsync(
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
            _logger.LogDebug("{CompilerName}: Using cached compilation for kernel '{KernelName}'", 
                CompilerName, definition.Name);
            return cachedKernel;
        }
        
        _logger.LogDebug("{CompilerName}: Starting compilation of kernel '{KernelName}'", 
            CompilerName, definition.Name);
        
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
            _metricsCache.TryAdd(cacheKey, metrics);
            
            // Cache the result if enabled
            if (EnableCaching)
            {
                _compilationCache.TryAdd(cacheKey, compiledKernel);
            }
            
            _logger.LogInformation(
                "{CompilerName}: Successfully compiled kernel '{KernelName}' in {ElapsedMs}ms with {OptimizationLevel} optimization",
                CompilerName, definition.Name, stopwatch.ElapsedMilliseconds, options.OptimizationLevel);
            
            return compiledKernel;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "{CompilerName}: Failed to compile kernel '{KernelName}'", 
                CompilerName, definition.Name);
            throw new KernelCompilationException($"Failed to compile kernel '{definition.Name}'", ex);
        }
    }
    
    /// <summary>
    /// Core compilation logic to be implemented by derived classes.
    /// </summary>
    protected abstract ValueTask<ICompiledKernel> CompileKernelCoreAsync(
        KernelDefinition definition,
        CompilationOptions options,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Validates kernel definition parameters.
    /// Common validation logic that was duplicated across implementations.
    /// </summary>
    protected virtual ValidationResult ValidateKernelDefinition(KernelDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return new ValidationResult(false, "Kernel name cannot be empty");
        }
        
        if (definition.Code == null || definition.Code.Length == 0)
        {
            return new ValidationResult(false, "Kernel code cannot be null or empty");
        }
        
        // Validate work dimensions if available
        if (definition.Metadata?.TryGetValue("WorkDimensions", out var workDimsObj) == true)
        {
            if (workDimsObj is int workDimensions && (workDimensions < 1 || workDimensions > 3))
            {
                return new ValidationResult(false, "Work dimensions must be between 1 and 3");
            }
        }
        
        // Validate parameters if available
        if (definition.Metadata?.TryGetValue("Parameters", out var paramsObj) == true)
        {
            if (paramsObj is IList<object> parameters && parameters.Count == 0)
            {
                return new ValidationResult(false, "Kernel must have at least one parameter");
            }
        }
        
        // Additional validation can be added by derived classes
        return AdditionalValidation(definition);
    }
    
    /// <summary>
    /// Hook for derived classes to add additional validation.
    /// </summary>
    protected virtual ValidationResult AdditionalValidation(KernelDefinition definition)
    {
        return new ValidationResult(true, null);
    }
    
    /// <summary>
    /// Generates a cache key for the compilation.
    /// </summary>
    protected virtual string GenerateCacheKey(KernelDefinition definition, CompilationOptions options)
    {
        // Use kernel name, code hash, and optimization level for cache key
        var codeHash = definition.Code != null 
            ? BitConverter.ToString(System.Security.Cryptography.SHA256.HashData(definition.Code)).Replace("-", "")
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
            EnableProfileGuidedOptimizations =false,
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
        _logger.LogDebug("{CompilerName}: Compilation cache cleared", CompilerName);
    }
    
    /// <summary>
    /// Gets compilation metrics for analysis.
    /// </summary>
    public virtual IReadOnlyDictionary<string, CompilationMetrics> GetMetrics()
    {
        return _metricsCache;
    }
    
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
        var metadata = definition.Metadata ?? new Dictionary<string, object>();
        
        // Add compilation metadata
        metadata["Compiler"] = CompilerName;
        metadata["CompilationTimestamp"] = DateTime.UtcNow;
        
        // Add additional metadata from derived compiler
        foreach (var kvp in additionalMetadata)
        {
            metadata[kvp.Key] = kvp.Value;
        }
        
        // Create new definition with enriched metadata
        return new KernelDefinition(definition.Name, definition.Code, definition.EntryPoint)
        {
            Metadata = metadata
        };
    }

    public Task<ManagedCompiledKernel> CompileAsync(IKernelSource source, CompilationOptions options, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<KernelValidationResult> ValidateAsync(IKernelSource source, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ManagedCompiledKernel> OptimizeAsync(ManagedCompiledKernel kernel, OptimizationLevel level, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}

/// <summary>
/// Represents the result of kernel validation.
/// </summary>
public record ValidationResult(bool IsValid, string? ErrorMessage);

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
}
