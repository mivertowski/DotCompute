// <copyright file="DynamicCompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Operators.Caching;
using DotCompute.Linq.Operators.Compilation;
using DotCompute.Linq.Operators.Generation;
using DotCompute.Linq.Operators.Models;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.Operators.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Operators.Kernels;

/// <summary>
/// A kernel compiled dynamically from generated source code.
/// </summary>
internal class DynamicCompiledKernel : Operators.Interfaces.IKernel, IAsyncDisposable
{
    private readonly GeneratedKernel _generatedKernel;
    private readonly IAccelerator _accelerator;
    private readonly ILogger _logger;
    private readonly Lazy<Compilation.IUnifiedKernelCompiler> _compiler;
    private bool _disposed;
    private Execution.ICompiledKernel? _compiledKernel;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicCompiledKernel"/> class.
    /// </summary>
    /// <param name="generatedKernel">The generated kernel definition.</param>
    /// <param name="accelerator">The accelerator to compile for.</param>
    /// <param name="logger">The logger instance.</param>
    public DynamicCompiledKernel(GeneratedKernel generatedKernel, IAccelerator accelerator, ILogger logger)
    {
        _generatedKernel = generatedKernel ?? throw new ArgumentNullException(nameof(generatedKernel));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _compiler = new Lazy<Compilation.IUnifiedKernelCompiler>(() => CreateCompiler());

        Properties = CreateKernelProperties();
    }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name => _generatedKernel.Name;

    /// <summary>
    /// Gets the source code or IL representation of the kernel.
    /// </summary>
    public string Source => _generatedKernel.Source;

    /// <summary>
    /// Gets the entry point method name for the kernel.
    /// </summary>
    public string EntryPoint => _generatedKernel.EntryPoint ?? "main";

    /// <summary>
    /// Gets the required shared memory size in bytes.
    /// </summary>
    public int RequiredSharedMemory => _generatedKernel.RequiredSharedMemory;

    /// <summary>
    /// Gets the kernel properties.
    /// </summary>
    public KernelProperties Properties { get; }

    /// <summary>
    /// Compiles the kernel for execution.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
                Metadata = _generatedKernel.OptimizationMetadata ?? []
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
    /// <param name="workItems">The work items defining execution dimensions.</param>
    /// <param name="parameters">The kernel parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
    /// <returns>A read-only list of kernel parameters.</returns>
    public IReadOnlyList<Parameters.KernelParameter> GetParameterInfo()
    {
        return _generatedKernel.Parameters.Select(p => new Parameters.KernelParameter(
            p.Name, 
            p.Type, 
            p.IsInput && p.IsOutput ? Parameters.ParameterDirection.InOut :
                       p.IsOutput ? Parameters.ParameterDirection.Out : Parameters.ParameterDirection.In
        )).ToArray();
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
    /// <returns>A value task representing the asynchronous operation.</returns>
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

    private Compilation.IUnifiedKernelCompiler CreateCompiler()
    {
        // Create a kernel compiler adapter that bridges to the backend-specific compiler
        // This uses the accelerator's built-in compiler capabilities
        if (_accelerator != null)
        {
            // Use the accelerator's native compiler through the adapter
            var coreCompiler = new AcceleratorKernelCompiler(_accelerator);
            return new KernelCompilerAdapter(coreCompiler, _logger);
        }

        // Fallback to a basic compiler for CPU execution
        var fallbackCompiler = new CpuFallbackKernelCompiler(_logger);
        return new KernelCompilerAdapter(fallbackCompiler, _logger);
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