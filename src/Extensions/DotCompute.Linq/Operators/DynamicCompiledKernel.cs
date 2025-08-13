// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Operators;

/// <summary>
/// A kernel compiled dynamically from generated source code.
/// </summary>
internal class DynamicCompiledKernel : IKernel
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
            _compiledKernel?.Dispose();
            _disposed = true;
        }
    }

    private IKernelCompiler CreateCompiler()
    {
        // Create appropriate compiler based on accelerator type
        return _accelerator.Type switch
        {
            AcceleratorType.CUDA => new CudaKernelCompiler(_logger),
            AcceleratorType.OpenCL => new OpenCLKernelCompiler(_logger),
            AcceleratorType.Metal => new MetalKernelCompiler(_logger),
            AcceleratorType.CPU => new CudaKernelCompiler(_logger), // Fallback to CUDA compiler
            AcceleratorType.GPU => new CudaKernelCompiler(_logger), // Fallback to CUDA compiler
            _ => throw new NotSupportedException($"Kernel compiler for {_accelerator.Type} is not supported")
        };
    }

    private KernelProperties CreateKernelProperties()
    {
        var maxThreads = _generatedKernel.RequiredWorkGroupSize?.Aggregate(1, (a, b) => a * b) ?? 256;
        
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

// Placeholder compiler classes that would be implemented in the Core.Kernels project
internal class CudaKernelCompiler : IKernelCompiler
{
    private readonly ILogger _logger;
    public CudaKernelCompiler(ILogger logger) => _logger = logger;
    public Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("CUDA compiler not yet implemented");
}

internal class OpenCLKernelCompiler : IKernelCompiler
{
    private readonly ILogger _logger;
    public OpenCLKernelCompiler(ILogger logger) => _logger = logger;
    public Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("OpenCL compiler not yet implemented");
}

internal class MetalKernelCompiler : IKernelCompiler
{
    private readonly ILogger _logger;
    public MetalKernelCompiler(ILogger logger) => _logger = logger;
    public Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Metal compiler not yet implemented");
}

internal class DirectComputeKernelCompiler : IKernelCompiler
{
    private readonly ILogger _logger;
    public DirectComputeKernelCompiler(ILogger logger) => _logger = logger;
    public Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("DirectCompute compiler not yet implemented");
}

internal class VulkanKernelCompiler : IKernelCompiler
{
    private readonly ILogger _logger;
    public VulkanKernelCompiler(ILogger logger) => _logger = logger;
    public Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Vulkan compiler not yet implemented");
}

// Supporting interfaces and classes that would be defined in the Core project
public interface IKernelCompiler
{
    Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default);
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
    private static readonly Timer _cleanupTimer;
    
    static KernelCompilationCache()
    {
        // Clean up expired weak references every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }
    
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