// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Execution;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Backends.CPU.Kernels.Generators;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Abstractions.Debugging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Handles core execution logic for CPU kernels with SIMD vectorization support.
/// Provides optimized execution paths for both scalar and vectorized operations.
/// </summary>
internal sealed class CpuKernelExecutor : IDisposable
{
    private readonly KernelDefinition _definition;
    private readonly KernelExecutionPlan _executionPlan;
    private readonly CpuThreadPool _threadPool;
    private readonly ILogger _logger;
    private readonly SimdCodeGenerator _codeGenerator;
    private readonly SimdKernelExecutor? _kernelExecutor;
    private Delegate? _compiledDelegate;
    private long _executionCount;
    private readonly double _totalExecutionTimeMs;
    private bool _disposed;

    public CpuKernelExecutor(
        KernelDefinition definition,
        KernelExecutionPlan executionPlan,
        CpuThreadPool threadPool,
        ILogger logger)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _executionPlan = executionPlan ?? throw new ArgumentNullException(nameof(executionPlan));
        _threadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize SIMD code generator
        var simdSummary = executionPlan.Analysis.Definition.Metadata?.TryGetValue("SimdCapabilities", out var caps) == true
            ? (SimdSummary)caps
            : new SimdSummary
            {
                IsHardwareAccelerated = Vector.IsHardwareAccelerated,
                PreferredVectorWidth = SimdCapabilities.PreferredVectorWidth,
                SupportedInstructionSets = new HashSet<string>()
            };
        _codeGenerator = new SimdCodeGenerator(simdSummary);

        // Get or create the kernel executor if vectorization is enabled
        if (_executionPlan.UseVectorization)
        {
            _kernelExecutor = _codeGenerator.GetOrCreateVectorizedKernel(definition, executionPlan);
        }

        _logger.LogDebug("CpuKernelExecutor initialized for kernel {kernelName} with vectorization: {useVectorization}",
            definition.Name, _executionPlan.UseVectorization);
    }

    /// <summary>
    /// Sets the compiled delegate for direct kernel execution.
    /// </summary>
    public void SetCompiledDelegate(Delegate compiledDelegate)
    {
        _compiledDelegate = compiledDelegate ?? throw new ArgumentNullException(nameof(compiledDelegate));
        _logger.LogDebug("Compiled delegate set for kernel {kernelName}", _definition.Name);
    }

    /// <summary>
    /// Executes the kernel with the provided context.
    /// </summary>
    public async ValueTask ExecuteAsync(KernelExecutionContext context, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Executing kernel {kernelName} with work dimensions [{x}, {y}, {z}]",
                _definition.Name, context.WorkDimensions.X, context.WorkDimensions.Y, context.WorkDimensions.Z);

            // Choose execution path based on vectorization capability
            if (_executionPlan.UseVectorization && _kernelExecutor != null)
            {
                await ExecuteVectorizedAsync(context, cancellationToken);
            }
            else if (_compiledDelegate != null)
            {
                await ExecuteCompiledDelegateAsync(context, cancellationToken);
            }
            else
            {
                await ExecuteScalarAsync(context, cancellationToken);
            }

            stopwatch.Stop();
            UpdatePerformanceMetrics(stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Kernel execution failed for {kernelName}", _definition.Name);
            throw;
        }
    }

    /// <summary>
    /// Executes the kernel using vectorized SIMD operations.
    /// </summary>
    private async ValueTask ExecuteVectorizedAsync(KernelExecutionContext context, CancellationToken cancellationToken)
    {
        if (_kernelExecutor == null)
        {

            throw new InvalidOperationException("Vectorized kernel executor is not available");
        }


        _logger.LogDebug("Executing vectorized kernel {kernelName}", _definition.Name);

        var totalWorkItems = GetTotalWorkItems(context.WorkDimensions);
        var vectorFactor = _executionPlan.VectorizationFactor;
        var numThreads = Math.Min(_threadPool.MaxConcurrency, (int)Math.Ceiling((double)totalWorkItems / vectorFactor));
        var workItemsPerThread = totalWorkItems / numThreads;

        using var barrier = new Barrier(numThreads);
        var tasks = new Task[numThreads];

        // Create vectorized execution tasks
        for (var threadIndex = 0; threadIndex < numThreads; threadIndex++)
        {
            var startIndex = threadIndex * workItemsPerThread;
            var endIndex = Math.Min(startIndex + workItemsPerThread, totalWorkItems);

            tasks[threadIndex] = Task.Run(() => ExecuteVectorizedWorkItems(
                context, startIndex, endIndex, vectorFactor, barrier, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Executes the kernel using a compiled delegate.
    /// </summary>
    private async ValueTask ExecuteCompiledDelegateAsync(KernelExecutionContext context, CancellationToken cancellationToken)
    {
        if (_compiledDelegate == null)
        {

            throw new InvalidOperationException("Compiled delegate is not available");
        }


        _logger.LogDebug("Executing compiled delegate for kernel {kernelName}", _definition.Name);

        var totalWorkItems = GetTotalWorkItems(context.WorkDimensions);
        var numThreads = Math.Min(_threadPool.MaxConcurrency, (int)totalWorkItems);
        var workItemsPerThread = totalWorkItems / numThreads;

        using var barrier = new Barrier(numThreads);
        var tasks = new Task[numThreads];

        // Create execution tasks for compiled delegate
        for (var threadIndex = 0; threadIndex < numThreads; threadIndex++)
        {
            var startIndex = threadIndex * workItemsPerThread;
            var endIndex = Math.Min(startIndex + workItemsPerThread, totalWorkItems);

            tasks[threadIndex] = Task.Run(() => ExecuteCompiledDelegateWorkItems(
                context, startIndex, endIndex, barrier, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Executes the kernel using scalar operations.
    /// </summary>
    private async ValueTask ExecuteScalarAsync(KernelExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing scalar kernel {kernelName}", _definition.Name);

        var totalWorkItems = GetTotalWorkItems(context.WorkDimensions);
        var numThreads = Math.Min(_threadPool.MaxConcurrency, (int)totalWorkItems);
        var workItemsPerThread = totalWorkItems / numThreads;

        using var barrier = new Barrier(numThreads);
        var tasks = new Task[numThreads];

        // Create scalar execution tasks
        for (var threadIndex = 0; threadIndex < numThreads; threadIndex++)
        {
            var startIndex = threadIndex * workItemsPerThread;
            var endIndex = Math.Min(startIndex + workItemsPerThread, totalWorkItems);

            tasks[threadIndex] = Task.Run(() => ExecuteScalarWorkItems(
                context, startIndex, endIndex, barrier, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    // Work item execution methods

    private void ExecuteVectorizedWorkItems(
        KernelExecutionContext context,
        long startIndex,
        long endIndex,
        int vectorFactor,
        Barrier barrier,
        CancellationToken cancellationToken)
    {
        var vectorWidth = _executionPlan.VectorWidth;

        for (var i = startIndex; i < endIndex && !cancellationToken.IsCancellationRequested; i++)
        {
            var baseIndex = i * vectorFactor;
            var workItemIds = new long[vectorFactor][];

            // Prepare vectorized work items
            for (var v = 0; v < vectorFactor; v++)
            {
                var actualIndex = baseIndex + v;
                if (actualIndex < GetTotalWorkItems(context.WorkDimensions))
                {
                    workItemIds[v] = GetWorkItemId(actualIndex, context.WorkDimensions);
                }
            }

            // Execute vectorized kernel
            ExecuteVectorizedWorkItem(context, workItemIds, vectorWidth);
        }

        // Synchronize with other workers
        SynchronizeWithBarrier(barrier, cancellationToken);
    }

    private void ExecuteCompiledDelegateWorkItems(
        KernelExecutionContext context,
        long startIndex,
        long endIndex,
        Barrier barrier,
        CancellationToken cancellationToken)
    {
        for (var i = startIndex; i < endIndex && !cancellationToken.IsCancellationRequested; i++)
        {
            var workItemId = GetWorkItemId(i, context.WorkDimensions);
            ExecuteCompiledDelegateWorkItem(context, workItemId);
        }

        // Synchronize with other workers
        SynchronizeWithBarrier(barrier, cancellationToken);
    }

    private void ExecuteScalarWorkItems(
        KernelExecutionContext context,
        long startIndex,
        long endIndex,
        Barrier barrier,
        CancellationToken cancellationToken)
    {
        for (var i = startIndex; i < endIndex && !cancellationToken.IsCancellationRequested; i++)
        {
            var workItemId = GetWorkItemId(i, context.WorkDimensions);
            ExecuteSingleWorkItem(context, workItemId);
        }

        // Synchronize with other workers
        SynchronizeWithBarrier(barrier, cancellationToken);
    }

    // Individual work item execution

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteVectorizedWorkItem(
        KernelExecutionContext context,
        long[][] workItemIds,
        int vectorWidth)
    {
        try
        {
            // Execute using the vectorized kernel executor
            _kernelExecutor?.ExecuteVectorized(context, workItemIds, vectorWidth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vectorized work item execution failed");
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteCompiledDelegateWorkItem(KernelExecutionContext context, long[] workItemId)
    {
        try
        {
            // Execute using the compiled delegate
            // This would need to be properly implemented based on delegate signature
            _compiledDelegate?.DynamicInvoke(context, workItemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compiled delegate work item execution failed");
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteSingleWorkItem(KernelExecutionContext context, long[] workItemId)
    {
        try
        {
            // Execute using the basic kernel definition
            // This would be a fallback execution path
            ExecuteBasicKernel(context, workItemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scalar work item execution failed");
            throw;
        }
    }

    // Helper methods

    private void ExecuteBasicKernel(KernelExecutionContext context, long[] workItemId)
    {
        // Basic kernel execution - this would be implemented based on kernel definition
        // For now, this is a placeholder that would delegate to the actual kernel logic
        _logger.LogTrace("Executing basic kernel for work item [{workItem}]", string.Join(", ", workItemId));
    }

    private static long GetTotalWorkItems(WorkDimensions dimensions)
    {
        return dimensions.X * dimensions.Y * dimensions.Z;
    }

    private static long[] GetWorkItemId(long globalIndex, WorkDimensions dimensions)
    {
        var z = globalIndex / (dimensions.X * dimensions.Y);
        var remainder = globalIndex % (dimensions.X * dimensions.Y);
        var y = remainder / dimensions.X;
        var x = remainder % dimensions.X;

        return [x, y, z];
    }

    private static void SynchronizeWithBarrier(Barrier barrier, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                barrier.SignalAndWait(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
        }
    }

    private void UpdatePerformanceMetrics(double executionTimeMs)
    {
        Interlocked.Increment(ref _executionCount);
        var newTotal = Interlocked.Add(ref _totalExecutionTimeMs, executionTimeMs);

        _logger.LogTrace("Kernel {kernelName} executed in {executionTime:F2}ms (execution #{count})",
            _definition.Name, executionTimeMs, _executionCount);

        // Log performance stats periodically
        if (_executionCount % 100 == 0)
        {
            var avgTime = newTotal / _executionCount;
            _logger.LogInformation("Kernel {kernelName} performance: {count} executions, {avgTime:F2}ms average",
                _definition.Name, _executionCount, avgTime);
        }
    }

    public ExecutionStatistics GetExecutionStatistics()
    {
        return new ExecutionStatistics
        {
            KernelName = _definition.Name,
            ExecutionCount = _executionCount,
            TotalExecutionTimeMs = _totalExecutionTimeMs,
            AverageExecutionTimeMs = _executionCount > 0 ? _totalExecutionTimeMs / _executionCount : 0,
            UseVectorization = _executionPlan.UseVectorization,
            VectorizationFactor = _executionPlan.VectorizationFactor,
            VectorWidth = _executionPlan.VectorWidth
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _kernelExecutor?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Context for vectorized execution operations.
/// </summary>
internal sealed class VectorizedExecutionContext
{
    public required KernelExecutionContext KernelContext { get; set; }
    public required KernelExecutionPlan ExecutionPlan { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

