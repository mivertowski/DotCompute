// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels.Simd;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// Optimized SIMD kernel executor with advanced performance techniques:
/// - Instruction-level parallelism with multiple execution units
/// - Loop unrolling with optimal stride patterns
/// - Branch prediction optimization
/// - Cache-friendly memory access patterns
/// - Prefetch instructions for improved memory bandwidth
/// - Vectorized operations with fallback paths
/// - Runtime CPU feature detection and optimization
/// Target: 4-8x performance improvement over scalar code
/// </summary>
public sealed partial class OptimizedSimdExecutor : IDisposable
{
    private readonly ILogger<OptimizedSimdExecutor> _logger;
    private readonly SimdOptimizationEngine _optimizationEngine;
    private readonly SimdInstructionDispatcher _instructionDispatcher;
    private readonly SimdPerformanceAnalyzer _performanceAnalyzer;
    private volatile bool _disposed;

    // LoggerMessage delegates - Event IDs 7600-7619
    [LoggerMessage(EventId = 7600, Level = LogLevel.Debug, Message = "Optimized SIMD executor initialized with capabilities: {Capabilities}")]
    private partial void LogExecutorInitialized(SimdSummary capabilities);

    [LoggerMessage(EventId = 7601, Level = LogLevel.Error, Message = "Error executing SIMD kernel for {ElementCount} elements")]
    private partial void LogKernelExecutionError(long elementCount, Exception ex);

    [LoggerMessage(EventId = 7602, Level = LogLevel.Error, Message = "Error executing SIMD reduction for {ElementCount} elements")]
    private partial void LogReductionExecutionError(int elementCount, Exception ex);

    [LoggerMessage(EventId = 7603, Level = LogLevel.Information, Message = "SIMD executor statistics reset")]
    private partial void LogStatisticsReset();

    [LoggerMessage(EventId = 7604, Level = LogLevel.Debug, Message = "Optimized SIMD executor disposed")]
    private partial void LogExecutorDisposed();

    /// <summary>
    /// Initializes a new optimized SIMD executor.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="config">Executor configuration.</param>
    public OptimizedSimdExecutor(ILogger<OptimizedSimdExecutor> logger, ExecutorConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var executorConfig = config ?? ExecutorConfiguration.Default;
        var capabilities = SimdCapabilities.GetSummary();

        // Initialize optimization and analysis components
        _optimizationEngine = new SimdOptimizationEngine(capabilities, executorConfig);
        _performanceAnalyzer = new SimdPerformanceAnalyzer(
            LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<SimdPerformanceAnalyzer>());

        // Initialize instruction dispatcher
        _instructionDispatcher = new SimdInstructionDispatcher(
            LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<SimdInstructionDispatcher>(),
            _optimizationEngine,
            _performanceAnalyzer,
            capabilities);

        LogExecutorInitialized(capabilities);
    }

    /// <summary>
    /// Gets executor performance statistics.
    /// </summary>
    public ExecutorStatistics Statistics => _performanceAnalyzer.GetStatistics();

    /// <summary>
    /// Executes a vectorized kernel with optimal SIMD utilization.
    /// </summary>
    /// <typeparam name="T">Element type (must be unmanaged).</typeparam>
    /// <param name="definition">Kernel definition.</param>
    /// <param name="input1">First input buffer.</param>
    /// <param name="input2">Second input buffer.</param>
    /// <param name="output">Output buffer.</param>
    /// <param name="elementCount">Number of elements to process.</param>
    public void Execute<T>(
        KernelDefinition definition,
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount) where T : unmanaged
    {
        ThrowIfDisposed();

        // Validate input parameters
        SimdInstructionDispatcher.ValidateExecutionParameters(input1, input2, output, elementCount);

        try
        {
            // Dispatch execution through the optimized instruction dispatcher
            _instructionDispatcher.DispatchExecution(definition, input1, input2, output, elementCount);
        }
        catch (Exception ex)
        {
            LogKernelExecutionError(elementCount, ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a reduction operation with optimized SIMD reduction patterns.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="input">Input data.</param>
    /// <param name="operation">Reduction operation.</param>
    /// <returns>Reduced result.</returns>
    public T ExecuteReduction<T>(ReadOnlySpan<T> input, ReductionOperation operation) where T : unmanaged
    {
        ThrowIfDisposed();

        if (input.IsEmpty)
        {
            return default;
        }

        try
        {
            // Dispatch reduction through the instruction dispatcher
            return _instructionDispatcher.DispatchReduction(input, operation);
        }
        catch (Exception ex)
        {
            LogReductionExecutionError(input.Length, ex);
            throw;
        }
    }

    /// <summary>
    /// Analyzes workload characteristics for optimization planning.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="elementCount">Number of elements.</param>
    /// <returns>Workload analysis profile.</returns>
    public WorkloadProfile AnalyzeWorkload<T>(long elementCount) where T : unmanaged
    {
        ThrowIfDisposed();

        // Create a basic execution context for analysis
        var capabilities = SimdCapabilities.GetSummary();
        var context = new Simd.ExecutionContext(capabilities);

        return SimdOptimizationEngine.AnalyzeWorkload<T>(elementCount, context);
    }

    /// <summary>
    /// Gets performance trends and optimization recommendations.
    /// </summary>
    /// <returns>Performance trend analysis.</returns>
    public PerformanceTrendAnalysis GetPerformanceTrends()
    {
        ThrowIfDisposed();
        return _performanceAnalyzer.AnalyzeTrends();
    }

    /// <summary>
    /// Gets detailed performance metrics for a specific operation type.
    /// </summary>
    /// <param name="operationType">Type of operation to query.</param>
    /// <returns>Performance metrics snapshot or null if not found.</returns>
    public PerformanceMetricSnapshot? GetOperationMetrics(string operationType)
    {
        ThrowIfDisposed();
        return _performanceAnalyzer.GetMetrics(operationType);
    }

    /// <summary>
    /// Resets all performance counters and metrics.
    /// </summary>
    public void ResetStatistics()
    {
        ThrowIfDisposed();
        _performanceAnalyzer.Reset();
        LogStatisticsReset();
    }

    /// <summary>
    /// Estimates cache efficiency for a given workload.
    /// </summary>
    /// <param name="elementCount">Number of elements.</param>
    /// <param name="elementType">Type of elements.</param>
    /// <returns>Cache efficiency estimate (0.0 to 1.0).</returns>
    public double EstimateCacheEfficiency(long elementCount, Type elementType)
    {
        ThrowIfDisposed();
        return SimdOptimizationEngine.EstimateCacheEfficiency(elementCount, elementType);
    }

    /// <summary>
    /// Estimates vectorization potential for a given workload.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="elementCount">Number of elements.</param>
    /// <returns>Vectorization potential (0.0 to 1.0).</returns>
    public double EstimateVectorizationPotential<T>(long elementCount) where T : unmanaged
    {
        ThrowIfDisposed();
        return SimdOptimizationEngine.EstimateVectorizationPotential<T>(elementCount);
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
            _instructionDispatcher?.Dispose();
            _performanceAnalyzer?.Dispose();
            LogExecutorDisposed();
        }
    }
}