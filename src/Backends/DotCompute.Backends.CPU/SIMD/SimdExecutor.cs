// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// Main SIMD kernel executor with strategy-based execution
/// </summary>
public sealed class SimdExecutor : IDisposable
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

        _logger.LogDebug("SIMD executor initialized with capabilities: {Capabilities}", _capabilities);
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
            Interlocked.Increment(ref _totalExecutions);
            Interlocked.Add(ref _totalElements, elementCount);

            // Determine optimal execution strategy
            var strategy = _instructionSelector.DetermineExecutionStrategy<T>(elementCount, context);

            // Optimize memory layout if needed
            _memoryOptimizer.OptimizeMemoryLayout(input1, input2, output);

            // Execute with the optimal strategy
            _vectorOps.ExecuteVectorized(strategy, input1, input2, output, elementCount, context);

            // Update performance counters
            var (vectorized, scalar) = _profiler.CalculateVectorizationStats(strategy, elementCount);
            Interlocked.Add(ref _vectorizedElements, vectorized);
            Interlocked.Add(ref _scalarElements, scalar);

            RecordExecutionTime(startTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SIMD kernel for {ElementCount} elements", elementCount);
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
        Interlocked.Add(ref _totalExecutionTime, elapsed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SimdExecutor));
        }
    }

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
            _logger.LogDebug("SIMD executor disposed");
        }
    }
}

/// <summary>
/// Execution context for thread-local optimizations
/// </summary>
internal sealed class ExecutionContext
{
    public SimdSummary Capabilities { get; }
    public long ThreadExecutions { get; set; }
    public DateTimeOffset LastExecution { get; set; }

    public ExecutionContext(SimdSummary capabilities)
    {
        Capabilities = capabilities;
        LastExecution = DateTimeOffset.UtcNow;
    }
}

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