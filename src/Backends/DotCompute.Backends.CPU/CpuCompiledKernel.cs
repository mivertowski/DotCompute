// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Backends.CPU.Kernels.Models;
using DotCompute.Abstractions.Execution;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Represents a compiled kernel for CPU execution with vectorization support.
/// Orchestrates execution through specialized components for optimal performance.
/// </summary>
public sealed class CpuCompiledKernel : ICompiledKernel
{
    private readonly KernelDefinition _definition;
    private readonly KernelExecutionPlan _executionPlan;
    private readonly CpuThreadPool _threadPool;
    private readonly ILogger _logger;

    // Orchestrated components
    private readonly CpuKernelExecutor _executor;
    private readonly CpuKernelValidator _validator;
    private readonly CpuKernelOptimizer _optimizer;
    private readonly CpuKernelCache _cache;

    private int _disposed;
    /// <summary>
    /// Initializes a new instance of the CpuCompiledKernel class.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="executionPlan">The execution plan.</param>
    /// <param name="threadPool">The thread pool.</param>
    /// <param name="logger">The logger.</param>

    public CpuCompiledKernel(
        KernelDefinition definition,
        KernelExecutionPlan executionPlan,
        CpuThreadPool threadPool,
        ILogger logger)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _executionPlan = executionPlan ?? throw new ArgumentNullException(nameof(executionPlan));
        _threadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize orchestrated components
        _executor = new CpuKernelExecutor(definition, executionPlan, threadPool, logger);
        _validator = new CpuKernelValidator(logger);
        _optimizer = new CpuKernelOptimizer(logger, threadPool);
        _cache = new CpuKernelCache(logger);

        _logger.LogDebug("CpuCompiledKernel initialized with orchestrated components for kernel {KernelName}", definition.Name);
    }
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask ExecuteAsync(KernelExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Convert KernelExecutionContext to KernelArguments for internal processing
        var arguments = ConvertContextToArguments(context);
        return ExecuteAsync(arguments, cancellationToken);
    }
    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    /// <value>The definition.</value>

    public KernelDefinition Definition => _definition;
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>

    public string Name => _definition.Name;
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>

    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    /// <value>The source.</value>

    public string Source => _definition.Code != null ? "[Bytecode]" : "[Unknown]";
    /// <summary>
    /// Gets or sets the entry point.
    /// </summary>
    /// <value>The entry point.</value>

    public string EntryPoint => _definition.EntryPoint;
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>

    public bool IsValid => _disposed == 0;

    /// <summary>
    /// Sets the compiled delegate for direct kernel execution.
    /// </summary>
    public void SetCompiledDelegate(Delegate compiledDelegate)
    {
        ArgumentNullException.ThrowIfNull(compiledDelegate);
        _executor.SetCompiledDelegate(compiledDelegate);
        _logger.LogDebug("Compiled delegate set for kernel {KernelName}", _definition.Name);
    }
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(arguments);

        // Convert KernelArguments to KernelExecutionContext for internal processing
        var context = new KernelExecutionContext
        {
            KernelName = _definition.Name,
            WorkDimensions = (Dim3)new WorkDimensions(1024, 1, 1) // Default work size - should be configurable
        };

        // Map arguments to context parameters
        if (arguments.Arguments != null)
        {
            for (var i = 0; i < arguments.Arguments.Count; i++)
            {
                context.SetParameter(i, arguments.Arguments[i]!);
            }
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate execution context using the validator component
            var validationResult = await _validator.ValidateExecutionContextAsync(context, cancellationToken);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"Kernel validation failed: {string.Join(", ", validationResult.Issues)}", nameof(arguments));
            }

            // Check cache for optimized execution plan
            var cacheKey = CpuKernelCache.GenerateCacheKey(_definition, context.WorkDimensions, Abstractions.Types.OptimizationLevel.O2);
            var cachedKernel = await _cache.GetKernelAsync(cacheKey);

            if (cachedKernel != null)
            {
                _logger.LogDebug("Using cached compiled kernel for kernel {KernelName}", _definition.Name);
            }

            // Execute using the executor component
            await _executor.ExecuteAsync(context, cancellationToken);

            stopwatch.Stop();

            // Store performance metrics in cache
            var metrics = _executor.GetExecutionStatistics();
            await _cache.UpdateKernelPerformanceAsync(cacheKey, metrics);

            _logger.LogDebug("Kernel {KernelName} executed successfully in {ExecutionTime:F2}ms",
                _definition.Name, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Kernel execution failed for {KernelName}", _definition.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets performance metrics for this kernel.
    /// </summary>
    public ExecutionStatistics GetPerformanceMetrics() => _executor.GetExecutionStatistics();
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Dispose orchestrated components
        _executor?.Dispose();
        _validator?.Dispose();
        _optimizer?.Dispose();
        _cache?.Dispose();

        _logger.LogDebug("CpuCompiledKernel disposed for kernel {KernelName}", _definition.Name);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed != 0, this);

    private static KernelArguments ConvertContextToArguments(KernelExecutionContext context)
    {
        // Convert KernelExecutionContext parameters to KernelArguments
        var maxIndex = Math.Max(
            context.Buffers.Keys.DefaultIfEmpty(-1).Max(),
            context.Scalars.Keys.DefaultIfEmpty(-1).Max());

        if (maxIndex < 0)
        {
            return [];
        }

        var args = new object[maxIndex + 1];
        foreach (var (index, buffer) in context.Buffers)
        {
            if (index >= 0 && index < args.Length)
            {
                args[index] = buffer;
            }
        }
        foreach (var (index, scalar) in context.Scalars)
        {
            if (index >= 0 && index < args.Length)
            {
                args[index] = scalar;
            }
        }

        return [.. args];
    }
}
