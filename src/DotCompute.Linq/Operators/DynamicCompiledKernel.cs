// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

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
            AcceleratorType.DirectCompute => new DirectComputeKernelCompiler(_logger),
            AcceleratorType.Vulkan => new VulkanKernelCompiler(_logger),
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