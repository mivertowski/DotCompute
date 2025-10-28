// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Execution;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels.Generators;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Abstractions.Debugging;
using DotCompute.Backends.CPU.Kernels.Models;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Handles core execution logic for CPU kernels with SIMD vectorization support.
/// Provides optimized execution paths for both scalar and vectorized operations.
/// </summary>
internal sealed partial class CpuKernelExecutor : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 7300,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "CpuKernelExecutor initialized for kernel {KernelName} with vectorization: {UseVectorization}")]
    private static partial void LogExecutorInitialized(ILogger logger, string kernelName, bool useVectorization);

    [LoggerMessage(
        EventId = 7301,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Compiled delegate set for kernel {KernelName}")]
    private static partial void LogCompiledDelegateSet(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7302,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Executing kernel {KernelName} with work dimensions [{X}, {Y}, {Z}]")]
    private static partial void LogExecutingKernel(ILogger logger, string kernelName, long x, long y, long z);

    [LoggerMessage(
        EventId = 7303,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Kernel execution failed for {KernelName}")]
    private static partial void LogKernelExecutionFailed(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 7304,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Executing vectorized kernel {KernelName}")]
    private static partial void LogExecutingVectorizedKernel(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7305,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Executing compiled delegate for kernel {KernelName}")]
    private static partial void LogExecutingCompiledDelegate(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7306,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Executing scalar kernel {KernelName}")]
    private static partial void LogExecutingScalarKernel(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7307,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Vectorized execution for {Count} work items with vector width {Width}")]
    private static partial void LogVectorizedExecution(ILogger logger, int count, int width);

    [LoggerMessage(
        EventId = 7308,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Vectorized work item execution failed")]
    private static partial void LogVectorizedWorkItemFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 7309,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Compiled delegate work item execution failed")]
    private static partial void LogCompiledDelegateWorkItemFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 7310,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Scalar work item execution failed")]
    private static partial void LogScalarWorkItemFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 7311,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Executing basic kernel for work item [{WorkItem}]")]
    private static partial void LogExecutingBasicKernel(ILogger logger, string workItem);

    [LoggerMessage(
        EventId = 7312,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Kernel {KernelName} executed in {ExecutionTime:F2}ms (execution #{Count})")]
    private static partial void LogKernelExecutionTime(ILogger logger, string kernelName, double executionTime, long count);

    [LoggerMessage(
        EventId = 7313,
        Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Kernel {KernelName} performance: {Count} executions, {AvgTime:F2}ms average")]
    private static partial void LogKernelPerformanceStats(ILogger logger, string kernelName, long count, double avgTime);

    #endregion
    private readonly KernelDefinition _definition;
    private readonly KernelExecutionPlan _executionPlan;
    private readonly CpuThreadPool _threadPool;
    private readonly ILogger _logger;
    private readonly SimdCodeGenerator _codeGenerator;
    private readonly SimdKernelExecutor? _kernelExecutor;
    private Delegate? _compiledDelegate;
    private long _executionCount;
    private double _totalExecutionTimeMs;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CpuKernelExecutor class.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="executionPlan">The execution plan.</param>
    /// <param name="threadPool">The thread pool.</param>
    /// <param name="logger">The logger.</param>

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

        LogExecutorInitialized(_logger, definition.Name, _executionPlan.UseVectorization);
    }

    /// <summary>
    /// Sets the compiled delegate for direct kernel execution.
    /// </summary>
    public void SetCompiledDelegate(Delegate compiledDelegate)
    {
        _compiledDelegate = compiledDelegate ?? throw new ArgumentNullException(nameof(compiledDelegate));
        LogCompiledDelegateSet(_logger, _definition.Name);
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
            LogExecutingKernel(_logger, _definition.Name, context.WorkDimensions.X, context.WorkDimensions.Y, context.WorkDimensions.Z);

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
            LogKernelExecutionFailed(_logger, ex, _definition.Name);
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

        LogExecutingVectorizedKernel(_logger, _definition.Name);

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
                context, startIndex, endIndex, vectorFactor, barrier, cancellationToken), cancellationToken);
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

        LogExecutingCompiledDelegate(_logger, _definition.Name);

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
                context, startIndex, endIndex, barrier, cancellationToken), cancellationToken);
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Executes the kernel using scalar operations.
    /// </summary>
    private async ValueTask ExecuteScalarAsync(KernelExecutionContext context, CancellationToken cancellationToken)
    {
        LogExecutingScalarKernel(_logger, _definition.Name);

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
                context, startIndex, endIndex, barrier, cancellationToken), cancellationToken);
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
            // Note: Actual vectorized execution requires proper buffer setup via context.Buffers
            if (_kernelExecutor != null && context.Buffers.Count >= 2)
            {
                // Extract input/output buffers from context
                var input1Buffer = context.Buffers[0];
                var outputBuffer = context.Buffers[context.Buffers.Count - 1];
                var elementCount = workItemIds.Length;

                // For now, log the vectorized execution attempt
                // Full implementation would convert buffers to Span<byte> and call Execute
                LogVectorizedExecution(_logger, elementCount, vectorWidth);
            }
        }
        catch (Exception ex)
        {
            LogVectorizedWorkItemFailed(_logger, ex);
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
            _ = (_compiledDelegate?.DynamicInvoke(context, workItemId));
        }
        catch (Exception ex)
        {
            LogCompiledDelegateWorkItemFailed(_logger, ex);
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
            LogScalarWorkItemFailed(_logger, ex);
            throw;
        }
    }

    // Helper methods

    private void ExecuteBasicKernel(KernelExecutionContext context, long[] workItemId)
        // Basic kernel execution - this would be implemented based on kernel definition
        // For now, this is a placeholder that would delegate to the actual kernel logic
        => LogExecutingBasicKernel(_logger, string.Join(", ", workItemId));

    private static long GetTotalWorkItems(WorkDimensions dimensions) => dimensions.X * dimensions.Y * dimensions.Z;

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
        _ = Interlocked.Increment(ref _executionCount);
        _totalExecutionTimeMs += executionTimeMs;
        var newTotal = _totalExecutionTimeMs;

        LogKernelExecutionTime(_logger, _definition.Name, executionTimeMs, _executionCount);

        // Log performance stats periodically
        if (_executionCount % 100 == 0)
        {
            var avgTime = newTotal / _executionCount;
            LogKernelPerformanceStats(_logger, _definition.Name, _executionCount, avgTime);
        }
    }
    /// <summary>
    /// Gets the execution statistics.
    /// </summary>
    /// <returns>The execution statistics.</returns>

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

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            // SimdKernelExecutor doesn't implement IDisposable - no cleanup needed
            _disposed = true;
        }
    }
}

/// <summary>
/// Context for vectorized execution operations.
/// </summary>
internal sealed class VectorizedExecutionContext
{
    /// <summary>
    /// Gets or sets the kernel context.
    /// </summary>
    /// <value>The kernel context.</value>
    public required KernelExecutionContext KernelContext { get; set; }
    /// <summary>
    /// Gets or sets the execution plan.
    /// </summary>
    /// <value>The execution plan.</value>
    public required KernelExecutionPlan ExecutionPlan { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether cellation token.
    /// </summary>
    /// <value>The cancellation token.</value>
    public CancellationToken CancellationToken { get; set; }
}

