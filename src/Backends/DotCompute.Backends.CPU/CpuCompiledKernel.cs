// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Core.Compute;
using KernelExecutionContext = DotCompute.Abstractions.Execution.KernelExecutionContext;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Represents a compiled kernel for CPU execution with vectorization support.
/// Orchestrates execution through specialized components for optimal performance.
/// </summary>
internal sealed class CpuCompiledKernel : ICompiledKernel
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
        _optimizer = new CpuKernelOptimizer(definition, executionPlan, logger);
        _cache = new CpuKernelCache(logger);

        _logger.LogDebug("CpuCompiledKernel initialized with orchestrated components for kernel {kernelName}", definition.Name);
    }

    public ValueTask ExecuteAsync(KernelExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Convert KernelExecutionContext to KernelArguments for internal processing
        var arguments = ConvertContextToArguments(context);
        return ExecuteAsync(arguments, cancellationToken);
    }

    public KernelDefinition Definition => _definition;

    public string Name => _definition.Name;

    public Guid Id { get; } = Guid.NewGuid();

    public string Source => _definition.Code != null ? "[Bytecode]" : "[Unknown]";

    public string EntryPoint => _definition.EntryPoint;

    public bool IsValid => _disposed == 0;

    /// <summary>
    /// Sets the compiled delegate for direct kernel execution.
    /// </summary>
    public void SetCompiledDelegate(Delegate compiledDelegate)
    {
        ArgumentNullException.ThrowIfNull(compiledDelegate);
        _executor.SetCompiledDelegate(compiledDelegate);
        _logger.LogDebug("Compiled delegate set for kernel {kernelName}", _definition.Name);
    }

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
            WorkDimensions = new Dim3(1024, 1, 1) // Default work size - should be configurable
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
            var cacheKey = _cache.GenerateCacheKey(_definition, _executionPlan, context);
            var cachedDelegate = await _cache.GetCompiledKernelAsync(cacheKey, cancellationToken);

            if (cachedDelegate != null)
            {
                _executor.SetCompiledDelegate(cachedDelegate);
                _logger.LogDebug("Using cached compiled delegate for kernel {kernelName}", _definition.Name);
            }

            // Optimize execution plan if needed
            var optimizedPlan = await _optimizer.OptimizeExecutionPlanAsync(context, cancellationToken);
            if (optimizedPlan != null)
            {
                _logger.LogDebug("Using optimized execution plan for kernel {kernelName}", _definition.Name);
            }

            // Execute using the executor component
            await _executor.ExecuteAsync(context, cancellationToken);

            stopwatch.Stop();

            // Store performance metrics in cache
            var metrics = _executor.GetExecutionStatistics();
            await _cache.StorePerformanceMetricsAsync(cacheKey, metrics, cancellationToken);

            _logger.LogDebug("Kernel {kernelName} executed successfully in {executionTime:F2}ms",
                _definition.Name, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Kernel execution failed for {kernelName}", _definition.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets performance metrics for this kernel.
    /// </summary>
    public ExecutionStatistics GetPerformanceMetrics()
    {
        return _executor.GetExecutionStatistics();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

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

        _logger.LogDebug("CpuCompiledKernel disposed for kernel {kernelName}", _definition.Name);
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
