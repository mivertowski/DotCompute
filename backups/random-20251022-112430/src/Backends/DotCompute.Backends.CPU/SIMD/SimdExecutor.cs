// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels.Simd;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// Main SIMD kernel executor with strategy-based execution
/// </summary>
public sealed partial class SimdExecutor : IDisposable
{
    private readonly ILogger<SimdExecutor> _logger;
    private readonly SimdSummary _capabilities;
    private readonly ExecutorConfiguration _config;
    private readonly ThreadLocal<ExecutionContext> _threadContext;
    private readonly SimdVectorOperations _vectorOps;
    private readonly SimdMatrixOperations _matrixOps;
    private readonly SimdMemoryOptimizer _memoryOptimizer;
    private readonly SimdInstructionSelector _instructionSelector;
    private readonly SimdPerformanceProfiler _profiler;

    // Performance counters
    private long _totalExecutions;
    private long _totalElements;
    private long _vectorizedElements;
    private long _scalarElements;
    private long _totalExecutionTime;
    private volatile bool _disposed;

    // LoggerMessage delegates - Event IDs 7640-7659
    [LoggerMessage(EventId = 7640, Level = LogLevel.Debug, Message = "SIMD executor initialized with capabilities: {Capabilities}")]
    private partial void LogExecutorInitialized(SimdSummary capabilities);

    [LoggerMessage(EventId = 7641, Level = LogLevel.Error, Message = "Error executing SIMD kernel for {ElementCount} elements")]
    private partial void LogExecutionError(long elementCount, Exception ex);

    [LoggerMessage(EventId = 7642, Level = LogLevel.Debug, Message = "SIMD executor disposed")]
    private partial void LogExecutorDisposed();

    /// <summary>
    /// Initializes a new instance of the SimdExecutor class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="config">The config.</param>

    public SimdExecutor(ILogger<SimdExecutor> logger, ExecutorConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? ExecutorConfiguration.Default;
        _capabilities = SimdCapabilities.GetSummary();
        _threadContext = new ThreadLocal<ExecutionContext>(() => new ExecutionContext(_capabilities),
            trackAllValues: true);

        // Initialize sub-components
        _vectorOps = new SimdVectorOperations(_capabilities, logger);
        _matrixOps = new SimdMatrixOperations(_capabilities, logger);
        _memoryOptimizer = new SimdMemoryOptimizer(_config, logger);
        _instructionSelector = new SimdInstructionSelector(_capabilities, logger);
        _profiler = new SimdPerformanceProfiler(logger);

        LogExecutorInitialized(_capabilities);
    }

    /// <summary>
    /// Gets executor performance statistics
    /// </summary>
    public ExecutorStatistics Statistics => new()
    {
        TotalExecutions = Interlocked.Read(ref _totalExecutions),
        TotalElements = Interlocked.Read(ref _totalElements),
        VectorizedElements = Interlocked.Read(ref _vectorizedElements),
        ScalarElements = Interlocked.Read(ref _scalarElements),
        AverageExecutionTime = CalculateAverageExecutionTime(),
        VectorizationRatio = CalculateVectorizationRatio(),
        PerformanceGain = CalculatePerformanceGain()
    };

    /// <summary>
    /// Executes a vectorized kernel with optimal SIMD utilization
    /// </summary>
    public unsafe void Execute<T>(
        KernelDefinition definition,
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount) where T : unmanaged
    {
        ThrowIfDisposed();

        var startTime = DateTimeOffset.UtcNow;
        var context = _threadContext.Value ?? throw new InvalidOperationException("Failed to get thread context");

        try
        {
            _ = Interlocked.Increment(ref _totalExecutions);
            _ = Interlocked.Add(ref _totalElements, elementCount);

            // Determine optimal execution strategy
            var strategy = _instructionSelector.DetermineExecutionStrategy<T>(elementCount, context);

            // Optimize memory layout if needed
            _memoryOptimizer.OptimizeMemoryLayout(input1, input2, output);

            // Execute with the optimal strategy
            _vectorOps.ExecuteVectorized(strategy, input1, input2, output, elementCount, context);

            // Update performance counters
            var (vectorized, scalar) = SimdPerformanceProfiler.CalculateVectorizationStats(strategy, elementCount);
            _ = Interlocked.Add(ref _vectorizedElements, vectorized);
            _ = Interlocked.Add(ref _scalarElements, scalar);

            RecordExecutionTime(startTime);
        }
        catch (Exception ex)
        {
            LogExecutionError(elementCount, ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a reduction operation with optimized SIMD reduction patterns
    /// </summary>
    public unsafe T ExecuteReduction<T>(
        ReadOnlySpan<T> input,
        ReductionOperation operation) where T : unmanaged
    {
        ThrowIfDisposed();

        if (input.IsEmpty)
        {
            return default;
        }

        var context = _threadContext.Value ?? throw new InvalidOperationException("Failed to get thread context");
        return _vectorOps.ExecuteReduction(input, operation, context);
    }

    /// <summary>
    /// Executes matrix operations with SIMD optimization
    /// </summary>
    public void ExecuteMatrixOperation<T>(
        ReadOnlySpan<T> matrixA,
        ReadOnlySpan<T> matrixB,
        Span<T> result,
        int rows,
        int cols,
        MatrixOperation operation) where T : unmanaged
    {
        ThrowIfDisposed();

        var context = _threadContext.Value ?? throw new InvalidOperationException("Failed to get thread context");
        _matrixOps.Execute(matrixA, matrixB, result, rows, cols, operation, context);
    }

    private TimeSpan CalculateAverageExecutionTime()
    {
        var totalTime = Interlocked.Read(ref _totalExecutionTime);
        var totalExecs = Interlocked.Read(ref _totalExecutions);
        return totalExecs > 0 ? TimeSpan.FromTicks(totalTime / totalExecs) : TimeSpan.Zero;
    }

    private double CalculateVectorizationRatio()
    {
        var vectorized = Interlocked.Read(ref _vectorizedElements);
        var total = Interlocked.Read(ref _totalElements);
        return total > 0 ? (double)vectorized / total : 0.0;
    }

    private double CalculatePerformanceGain()
    {
        var vectorizationRatio = CalculateVectorizationRatio();
        var baseGain = _capabilities.SupportsAvx512 ? 16.0 :
                      _capabilities.SupportsAvx2 ? 8.0 :
                      _capabilities.SupportsSse2 ? 4.0 : 1.0;
        return 1.0 + (baseGain - 1.0) * vectorizationRatio;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordExecutionTime(DateTimeOffset startTime)
    {
        var elapsed = (DateTimeOffset.UtcNow - startTime).Ticks;
        _ = Interlocked.Add(ref _totalExecutionTime, elapsed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _threadContext?.Dispose();
            _vectorOps?.Dispose();
            _matrixOps?.Dispose();
            _memoryOptimizer?.Dispose();
            _instructionSelector?.Dispose();
            _profiler?.Dispose();
            LogExecutorDisposed();
        }
    }
}

/// <summary>
/// Execution context for thread-local optimizations
/// </summary>
public sealed class ExecutionContext(SimdSummary capabilities)
{
    /// <summary>
    /// Gets or sets the capabilities.
    /// </summary>
    /// <value>The capabilities.</value>
    public SimdSummary Capabilities { get; } = capabilities;
    /// <summary>
    /// Gets or sets the thread executions.
    /// </summary>
    /// <value>The thread executions.</value>
    public long ThreadExecutions { get; set; }
    /// <summary>
    /// Gets or sets the last execution.
    /// </summary>
    /// <value>The last execution.</value>
    public DateTimeOffset LastExecution { get; set; } = DateTimeOffset.UtcNow;
}
/// <summary>
/// An matrix operation enumeration.
/// </summary>

/// <summary>
/// Matrix operations supported by SIMD executor
/// </summary>
public enum MatrixOperation
{
    Multiply,
    Add,
    Subtract,
    Transpose
}