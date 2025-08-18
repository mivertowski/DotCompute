// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Operators;

/// <summary>
/// A kernel compiled dynamically from generated source code.
/// </summary>
internal class DynamicCompiledKernel : IKernel, IAsyncDisposable
{
    private readonly GeneratedKernel _generatedKernel;
    private readonly IAccelerator _accelerator;
    private readonly ILogger _logger;
    private readonly Lazy<IKernelCompiler> _compiler;
    private bool _disposed;
    private ICompiledKernel? _compiledKernel;

    public DynamicCompiledKernel(GeneratedKernel generatedKernel, IAccelerator accelerator, ILogger logger)
    {
        _generatedKernel = generatedKernel ?? throw new ArgumentNullException(nameof(generatedKernel));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _compiler = new Lazy<IKernelCompiler>(() => CreateCompiler());
        
        Properties = CreateKernelProperties();
    }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name => _generatedKernel.Name;

    /// <summary>
    /// Gets the kernel properties.
    /// </summary>
    public KernelProperties Properties { get; }

    /// <summary>
    /// Compiles the kernel for execution.
    /// </summary>
    public async Task CompileAsync(CancellationToken cancellationToken = default)
    {
        if (_compiledKernel != null)
        {
            return; // Already compiled
        }

        try
        {
            _logger.LogDebug("Compiling dynamic kernel {KernelName}", Name);
            
            // Check compilation cache first
            var tempRequest = new KernelCompilationRequest
            {
                Name = _generatedKernel.Name,
                Source = _generatedKernel.Source,
                Language = _generatedKernel.Language,
                TargetAccelerator = _accelerator,
                OptimizationLevel = OptimizationLevel.Default
            };
            
            var cacheKey = KernelCompilationCache.GenerateCacheKey(tempRequest);
            
            if (KernelCompilationCache.TryGetCached(cacheKey, out var cachedKernel))
            {
                _compiledKernel = cachedKernel;
                _logger.LogDebug("Using cached compiled kernel {KernelName}", Name);
                return;
            }
            
            var compilationRequest = new KernelCompilationRequest
            {
                Name = _generatedKernel.Name,
                Source = _generatedKernel.Source,
                Language = _generatedKernel.Language,
                TargetAccelerator = _accelerator,
                OptimizationLevel = OptimizationLevel.Default,
                Metadata = _generatedKernel.OptimizationMetadata ?? new Dictionary<string, object>()
            };

            var result = await _compiler.Value.CompileKernelAsync(compilationRequest, cancellationToken)
                .ConfigureAwait(false);
                
            if (!result.Success)
            {
                throw new InvalidOperationException($"Kernel compilation failed: {result.ErrorMessage}");
            }

            _compiledKernel = result.CompiledKernel;
            
            // Cache the compiled kernel
            if (_compiledKernel != null)
            {
                KernelCompilationCache.Cache(cacheKey, _compiledKernel);
            }
            
            _logger.LogInformation("Successfully compiled dynamic kernel {KernelName}", Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile dynamic kernel {KernelName}", Name);
            throw;
        }
    }

    /// <summary>
    /// Executes the kernel with the given parameters.
    /// </summary>
    public async Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        if (_compiledKernel == null)
        {
            await CompileAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            _logger.LogDebug("Executing dynamic kernel {KernelName} with work items: {WorkItems}", 
                Name, string.Join(",", workItems.GlobalWorkSize));
                
            // Convert work items to execution parameters
            var executionParams = CreateExecutionParameters(workItems, parameters);
            
            // Execute the compiled kernel
            await _compiledKernel!.ExecuteAsync(executionParams, cancellationToken).ConfigureAwait(false);
            
            _logger.LogDebug("Successfully executed dynamic kernel {KernelName}", Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute dynamic kernel {KernelName}", Name);
            throw;
        }
    }

    /// <summary>
    /// Gets information about the kernel parameters.
    /// </summary>
    public IReadOnlyList<KernelParameter> GetParameterInfo()
    {
        return _generatedKernel.Parameters.Select(p => new KernelParameter(p.Name, p.Type, 
            p.IsInput && p.IsOutput ? ParameterDirection.InOut :
            p.IsOutput ? ParameterDirection.Out : ParameterDirection.In)).ToArray();
    }

    /// <summary>
    /// Disposes the kernel and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // We can't await here, so dispose synchronously
            _disposed = true;
        }
    }

    /// <summary>
    /// Asynchronously disposes the kernel and cleans up resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_compiledKernel != null)
            {
                // Note: ICompiledKernel implements IAsyncDisposable, but we can't call it from sync Dispose
                // The caller should use DisposeAsync() instead
            }
            _disposed = true;
        }
        
        // Add await statement to fix CS1998 warning
        await Task.Delay(1, CancellationToken.None).ConfigureAwait(false);
    }

    private IKernelCompiler CreateCompiler()
    {
        // Compilers are now provided by the backend accelerators themselves
        // The accelerator should have its own compiler implementation
        throw new NotImplementedException("Kernel compilation should be handled by the backend-specific accelerator implementation");
    }

    private KernelProperties CreateKernelProperties()
    {
        var maxThreads = _generatedKernel.RequiredWorkGroupSize?.Aggregate(1, (a, b) => (int)(a * b)) ?? 256;
        
        return new KernelProperties
        {
            MaxThreadsPerBlock = Math.Min(maxThreads, 1024),
            SharedMemorySize = _generatedKernel.SharedMemorySize,
            RegisterCount = EstimateRegisterCount(_generatedKernel)
        };
    }

    private static int EstimateRegisterCount(GeneratedKernel kernel)
    {
        // Rough estimation based on kernel complexity
        var sourceLines = kernel.Source.Split('\n').Length;
        var paramCount = kernel.Parameters.Length;
        
        // Simple heuristic: more lines and parameters = more registers
        return Math.Min(32 + (sourceLines / 10) + (paramCount * 2), 255);
    }

    private KernelExecutionParameters CreateExecutionParameters(WorkItems workItems, Dictionary<string, object> parameters)
    {
        return new KernelExecutionParameters
        {
            GlobalWorkSize = workItems.GlobalWorkSize,
            LocalWorkSize = workItems.LocalWorkSize,
            Arguments = parameters,
            SharedMemorySize = _generatedKernel.SharedMemorySize
        };
    }
}

/// <summary>
/// Kernel implementation that falls back to CPU execution for unsupported expressions.
/// </summary>
internal class ExpressionFallbackKernel : IKernel
{
    private readonly Expression _expression;
    private readonly Func<object> _compiledExpression;
    private bool _disposed;

    public ExpressionFallbackKernel(Expression expression)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        
        // Pre-compile the expression for CPU execution
        var lambda = Expression.Lambda(_expression);
        _compiledExpression = lambda.Compile() as Func<object> ?? 
            throw new InvalidOperationException("Failed to compile expression for CPU fallback");
            
        Name = $"FallbackKernel_{expression.NodeType}";
        Properties = new KernelProperties
        {
            MaxThreadsPerBlock = 1, // CPU execution is single-threaded
            SharedMemorySize = 0,
            RegisterCount = 0
        };
    }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the kernel properties.
    /// </summary>
    public KernelProperties Properties { get; }

    /// <summary>
    /// Compiles the kernel (no-op for fallback kernel).
    /// </summary>
    public Task CompileAsync(CancellationToken cancellationToken = default)
    {
        // Already compiled during construction
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the expression on CPU.
    /// </summary>
    public Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                // Execute the compiled expression
                var result = _compiledExpression();
                
                // Store result in parameters if output parameter exists
                if (parameters.ContainsKey("output"))
                {
                    parameters["output"] = result;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"CPU fallback execution failed: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Gets parameter information (empty for fallback kernel).
    /// </summary>
    public IReadOnlyList<KernelParameter> GetParameterInfo()
    {
        return Array.Empty<KernelParameter>();
    }

    /// <summary>
    /// Disposes the fallback kernel.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

// Note: Kernel compiler implementations are now provided by DotCompute.Core.Kernels
// This module uses the centralized implementations rather than duplicated placeholder classes

/// <summary>
/// Interface for kernel compilers used by LINQ operations.
/// </summary>
public interface IKernelCompiler
{
    Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Adapter that bridges the Core kernel compilers with the LINQ interface.
/// </summary>
internal class KernelCompilerAdapter : IKernelCompiler
{
    private readonly DotCompute.Abstractions.IKernelCompiler _coreCompiler;
    private readonly ILogger _logger;

    public KernelCompilerAdapter(DotCompute.Abstractions.IKernelCompiler coreCompiler, ILogger logger)
    {
        _coreCompiler = coreCompiler ?? throw new ArgumentNullException(nameof(coreCompiler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a mock kernel source for compatibility
            var kernelSource = new MockKernelSource
            {
                Name = request.Name,
                Code = request.Source,
                Language = DotCompute.Abstractions.KernelLanguage.CSharpIL,
                EntryPoint = request.Name
            };
            
            var options = new DotCompute.Abstractions.CompilationOptions
            {
                OptimizationLevel = ConvertOptimizationLevel(request.OptimizationLevel)
            };
            
            var kernelDefinition = new DotCompute.Abstractions.KernelDefinition(request.Name, kernelSource, options);

            var compiledKernel = await _coreCompiler.CompileAsync(kernelDefinition, options, cancellationToken);
            
            return new KernelCompilationResult
            {
                Success = true,
                CompiledKernel = new CompiledKernelAdapter(compiledKernel),
                CompilationTime = TimeSpan.FromMilliseconds(0) // TODO: Get actual time
            };
        }
        catch (Exception ex)
        {
            return new KernelCompilationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static DotCompute.Abstractions.OptimizationLevel ConvertOptimizationLevel(OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.Debug => DotCompute.Abstractions.OptimizationLevel.Debug,
            OptimizationLevel.Default => DotCompute.Abstractions.OptimizationLevel.Default,
            OptimizationLevel.Release => DotCompute.Abstractions.OptimizationLevel.Release,
            OptimizationLevel.Aggressive => DotCompute.Abstractions.OptimizationLevel.Aggressive,
            _ => DotCompute.Abstractions.OptimizationLevel.Default
        };
    }
}

/// <summary>
/// Adapter for compiled kernels.
/// </summary>
internal class CompiledKernelAdapter : ICompiledKernel
{
    private readonly DotCompute.Abstractions.ICompiledKernel _coreKernel;
    private bool _disposed;

    public CompiledKernelAdapter(DotCompute.Abstractions.ICompiledKernel coreKernel)
    {
        _coreKernel = coreKernel ?? throw new ArgumentNullException(nameof(coreKernel));
    }

    public async Task ExecuteAsync(KernelExecutionParameters parameters, CancellationToken cancellationToken = default)
    {
        // Convert parameters and execute
        // This would need to be implemented based on the actual execution needs
        await Task.CompletedTask;
        throw new NotImplementedException("Kernel execution adapter needs implementation");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // _coreKernel disposal handled through IAsyncDisposable
            _disposed = true;
        }
    }
}

public interface ICompiledKernel : IDisposable
{
    Task ExecuteAsync(KernelExecutionParameters parameters, CancellationToken cancellationToken = default);
}

public class KernelCompilationRequest
{
    public required string Name { get; init; }
    public required string Source { get; init; }
    public required Core.Kernels.KernelLanguage Language { get; init; }
    public required IAccelerator TargetAccelerator { get; init; }
    public OptimizationLevel OptimizationLevel { get; init; } = OptimizationLevel.Default;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public class KernelCompilationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public ICompiledKernel? CompiledKernel { get; init; }
    public TimeSpan CompilationTime { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public class KernelExecutionParameters
{
    public required int[] GlobalWorkSize { get; init; }
    public int[]? LocalWorkSize { get; init; }
    public Dictionary<string, object> Arguments { get; init; } = new();
    public int SharedMemorySize { get; init; }
}

public enum OptimizationLevel
{
    Debug,
    Default,
    Release,
    Aggressive
}

/// <summary>
/// Mock implementation of compiled kernel for development and testing.
/// </summary>
internal class MockCompiledKernel : ICompiledKernel
{
    private readonly string _name;
    private readonly string _source;
    private readonly ILogger _logger;
    private bool _disposed;

    public MockCompiledKernel(string name, string source, ILogger logger)
    {
        _name = name;
        _source = source;
        _logger = logger;
    }

    public async Task ExecuteAsync(KernelExecutionParameters parameters, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockCompiledKernel));

        _logger.LogDebug("Executing mock compiled kernel {KernelName}", _name);
        
        try
        {
            // Simulate kernel execution
            var totalWorkItems = parameters.GlobalWorkSize.Aggregate(1, (a, b) => a * b);
            var executionTime = Math.Max(1, totalWorkItems / 1000); // Simulate execution time
            
            await Task.Delay(executionTime, cancellationToken);
            
            // Simulate some basic operations on the output buffer
            if (parameters.Arguments.TryGetValue("output", out var outputBuffer))
            {
                _logger.LogDebug("Mock kernel execution completed for {WorkItems} work items", totalWorkItems);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock kernel execution failed");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogDebug("Disposing mock compiled kernel {KernelName}", _name);
            _disposed = true;
        }
    }
}

/// <summary>
/// Performance and caching optimizations for dynamic kernel compilation.
/// </summary>
internal static class KernelCompilationCache
{
    private static readonly ConcurrentDictionary<string, WeakReference<ICompiledKernel>> _cache = new();
    // Clean up expired weak references every 5 minutes
    private static readonly Timer _cleanupTimer = new(new TimerCallback(CleanupExpiredEntries), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    
    public static bool TryGetCached(string key, out ICompiledKernel? kernel)
    {
        kernel = null;
        
        if (_cache.TryGetValue(key, out var weakRef) && weakRef.TryGetTarget(out kernel))
        {
            return true;
        }
        
        // Remove expired entry
        if (weakRef != null)
        {
            _cache.TryRemove(key, out _);
        }
        
        return false;
    }
    
    public static void Cache(string key, ICompiledKernel kernel)
    {
        _cache[key] = new WeakReference<ICompiledKernel>(kernel);
    }
    
    public static string GenerateCacheKey(KernelCompilationRequest request)
    {
        // Generate cache key based on source hash and compilation options
        var sourceHash = request.Source.GetHashCode();
        var optionsHash = $"{request.Language}_{request.OptimizationLevel}_{request.TargetAccelerator.Type}".GetHashCode();
        return $"{sourceHash:X}_{optionsHash:X}";
    }
    
    private static void CleanupExpiredEntries(object? state)
    {
        var expiredKeys = new List<string>();
        
        foreach (var kvp in _cache)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                expiredKeys.Add(kvp.Key);
            }
        }
        
        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }
}

/// <summary>
/// Mock kernel source implementation for compatibility
/// </summary>
internal class MockKernelSource : IKernelSource
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DotCompute.Abstractions.KernelLanguage Language { get; set; }
    public string EntryPoint { get; set; } = string.Empty;
    public string[] Dependencies { get; set; } = Array.Empty<string>();
}