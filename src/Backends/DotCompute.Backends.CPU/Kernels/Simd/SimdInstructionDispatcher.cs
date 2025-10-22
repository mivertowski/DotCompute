// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Simd;

/// <summary>
/// SIMD instruction dispatcher responsible for coordinating execution across different
/// instruction sets and managing the execution pipeline.
/// </summary>
public sealed partial class SimdInstructionDispatcher : IDisposable
{
    private readonly ILogger<SimdInstructionDispatcher> _logger;
    private readonly SimdOptimizationEngine _optimizationEngine;
    private readonly SimdPerformanceAnalyzer _performanceAnalyzer;
    private readonly ThreadLocal<ExecutionContext> _threadContext;
    private volatile bool _disposed;

    // LoggerMessage delegates - Event IDs 7580-7599
    [LoggerMessage(EventId = 7580, Level = LogLevel.Debug, Message = "SIMD instruction dispatcher initialized")]
    private partial void LogDispatcherInitialized();

    [LoggerMessage(EventId = 7581, Level = LogLevel.Trace, Message = "Selected execution strategy: {Strategy} for {ElementCount} elements of type {TypeName}")]
    private partial void LogStrategySelected(SimdExecutionStrategy strategy, long elementCount, string typeName);

    [LoggerMessage(EventId = 7582, Level = LogLevel.Error, Message = "Error during SIMD execution dispatch for {ElementCount} elements")]
    private partial void LogExecutionError(long elementCount, Exception ex);

    [LoggerMessage(EventId = 7583, Level = LogLevel.Error, Message = "Error during SIMD reduction dispatch for {ElementCount} elements")]
    private partial void LogReductionError(int elementCount, Exception ex);

    [LoggerMessage(EventId = 7584, Level = LogLevel.Information, Message = "Performance declining for thread context, considering strategy adjustment")]
    private partial void LogPerformanceDeclining();

    [LoggerMessage(EventId = 7585, Level = LogLevel.Debug, Message = "SIMD instruction dispatcher disposed")]
    private partial void LogDispatcherDisposed();

    /// <summary>
    /// Initializes a new instance of the SimdInstructionDispatcher class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="optimizationEngine">The optimization engine.</param>
    /// <param name="performanceAnalyzer">The performance analyzer.</param>
    /// <param name="capabilities">The capabilities.</param>

    public SimdInstructionDispatcher(
        ILogger<SimdInstructionDispatcher> logger,
        SimdOptimizationEngine optimizationEngine,
        SimdPerformanceAnalyzer performanceAnalyzer,
        SimdSummary capabilities)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _optimizationEngine = optimizationEngine ?? throw new ArgumentNullException(nameof(optimizationEngine));
        _performanceAnalyzer = performanceAnalyzer ?? throw new ArgumentNullException(nameof(performanceAnalyzer));

        _threadContext = new ThreadLocal<ExecutionContext>(
            () => new ExecutionContext(capabilities),
            trackAllValues: true);

        LogDispatcherInitialized();
    }

    /// <summary>
    /// Dispatches kernel execution using optimal SIMD strategy.
    /// </summary>
    /// <typeparam name="T">Element type (must be unmanaged).</typeparam>
    /// <param name="definition">Kernel definition.</param>
    /// <param name="input1">First input buffer.</param>
    /// <param name="input2">Second input buffer.</param>
    /// <param name="output">Output buffer.</param>
    /// <param name="elementCount">Number of elements to process.</param>
    public unsafe void DispatchExecution<T>(
        KernelDefinition definition,
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount) where T : unmanaged
    {
        ThrowIfDisposed();

        var context = GetExecutionContext();
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Increment execution counters
            _performanceAnalyzer.RecordExecutionStart(elementCount);
            context.ThreadExecutions++;

            // Determine optimal execution strategy
            var strategy = _optimizationEngine.DetermineExecutionStrategy<T>(elementCount, context);

            // Log strategy selection for debugging
            LogStrategySelected(strategy, elementCount, typeof(T).Name);

            // Dispatch to appropriate execution path
            DispatchToStrategy(strategy, input1, input2, output, elementCount, context);

            // Record successful execution
            var executionTime = DateTimeOffset.UtcNow - startTime;
            _performanceAnalyzer.RecordExecutionComplete(elementCount, executionTime, strategy);
            context.LastExecution = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            LogExecutionError(elementCount, ex);
            throw;
        }
    }

    /// <summary>
    /// Dispatches reduction operation using optimal SIMD strategy.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="input">Input data.</param>
    /// <param name="operation">Reduction operation.</param>
    /// <returns>Reduced result.</returns>
    public unsafe T DispatchReduction<T>(ReadOnlySpan<T> input, ReductionOperation operation) where T : unmanaged
    {
        ThrowIfDisposed();

        if (input.IsEmpty)
        {
            return default;
        }

        _ = GetExecutionContext();
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _performanceAnalyzer.RecordReductionStart(input.Length);

            var result = operation switch
            {
                ReductionOperation.Sum => SimdVectorOperations.Sum(input),
                ReductionOperation.Min => SimdVectorOperations.Min(input),
                ReductionOperation.Max => SimdVectorOperations.Max(input),
                ReductionOperation.Product => DispatchProductReduction(input),
                _ => throw new ArgumentException($"Unsupported reduction operation: {operation}")
            };

            var executionTime = DateTimeOffset.UtcNow - startTime;
            _performanceAnalyzer.RecordReductionComplete(input.Length, executionTime, operation);

            return result;
        }
        catch (Exception ex)
        {
            LogReductionError(input.Length, ex);
            throw;
        }
    }

    /// <summary>
    /// Dispatches execution to the selected strategy.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="strategy">Selected execution strategy.</param>
    /// <param name="input1">First input buffer.</param>
    /// <param name="input2">Second input buffer.</param>
    /// <param name="output">Output buffer.</param>
    /// <param name="elementCount">Number of elements.</param>
    /// <param name="context">Execution context.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void DispatchToStrategy<T>(
        SimdExecutionStrategy strategy,
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            switch (strategy)
            {
                case SimdExecutionStrategy.Avx512:
                    SimdVectorOperations.AddAvx512(ptr1, ptr2, ptrOut, elementCount);
                    _performanceAnalyzer.RecordVectorizedElements(CalculateVectorizedElements<T>(elementCount, 16));
                    break;

                case SimdExecutionStrategy.Avx2:
                    SimdVectorOperations.AddAvx2(ptr1, ptr2, ptrOut, elementCount);
                    _performanceAnalyzer.RecordVectorizedElements(CalculateVectorizedElements<T>(elementCount, 8));
                    break;

                case SimdExecutionStrategy.Sse:
                    SimdVectorOperations.AddSse(ptr1, ptr2, ptrOut, elementCount);
                    _performanceAnalyzer.RecordVectorizedElements(CalculateVectorizedElements<T>(elementCount, 4));
                    break;

                case SimdExecutionStrategy.Neon:
                    SimdVectorOperations.AddNeon(ptr1, ptr2, ptrOut, elementCount);
                    _performanceAnalyzer.RecordVectorizedElements(CalculateVectorizedElements<T>(elementCount, 4));
                    break;

                case SimdExecutionStrategy.Scalar:
                    SimdScalarOperations.AddRemainder(ptr1, ptr2, ptrOut, elementCount);
                    _performanceAnalyzer.RecordScalarElements(elementCount);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported execution strategy: {strategy}");
            }
        }
    }

    /// <summary>
    /// Dispatches product reduction with appropriate optimization.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="input">Input data.</param>
    /// <returns>Product result.</returns>
    private static T DispatchProductReduction<T>(ReadOnlySpan<T> input) where T : unmanaged
        // For now, delegate to scalar operations
        // TODO: Implement vectorized product reduction
        => SimdScalarOperations.Product(input);

    /// <summary>
    /// Calculates the number of elements that will be processed using vectorized instructions.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="totalElements">Total number of elements.</param>
    /// <param name="vectorLanes">Number of lanes in the vector.</param>
    /// <returns>Number of vectorized elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long CalculateVectorizedElements<T>(long totalElements, int vectorLanes) where T : unmanaged
    {
        var elementsPerVector = vectorLanes / System.Runtime.InteropServices.Marshal.SizeOf<T>();
        var vectorCount = totalElements / elementsPerVector;
        return vectorCount * elementsPerVector;
    }

    /// <summary>
    /// Gets or creates the execution context for the current thread.
    /// </summary>
    /// <returns>Execution context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ExecutionContext GetExecutionContext() => _threadContext.Value ?? throw new InvalidOperationException("Failed to get thread execution context");

    /// <summary>
    /// Validates execution parameters before dispatching.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="input1">First input buffer.</param>
    /// <param name="input2">Second input buffer.</param>
    /// <param name="output">Output buffer.</param>
    /// <param name="elementCount">Number of elements.</param>
    public static void ValidateExecutionParameters<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount) where T : unmanaged
    {
        if (elementCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), "Element count must be non-negative");
        }

        if (input1.Length < elementCount)
        {
            throw new ArgumentException($"Input1 buffer too small: {input1.Length} < {elementCount}", nameof(input1));
        }

        if (input2.Length < elementCount)
        {
            throw new ArgumentException($"Input2 buffer too small: {input2.Length} < {elementCount}", nameof(input2));
        }

        if (output.Length < elementCount)
        {
            throw new ArgumentException($"Output buffer too small: {output.Length} < {elementCount}", nameof(output));
        }
    }

    /// <summary>
    /// Performs adaptive optimization based on execution history.
    /// </summary>
    /// <param name="context">Execution context.</param>
    public void PerformAdaptiveOptimization(ExecutionContext context)
    {
        if (context.ThreadExecutions > 100 && context.ThreadExecutions % 50 == 0)
        {
            // Analyze performance trends and adjust optimization parameters
            var performance = SimdOptimizationEngine.AnalyzeHistoricalPerformance(context);

            if (performance.TrendDirection == PerformanceTrend.Declining)
            {
                LogPerformanceDeclining();
                // Could adjust thresholds or strategy selection here
            }
        }
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
            LogDispatcherDisposed();
        }
    }
}

#region Supporting Types

/// <summary>
/// An reduction operation enumeration.
/// </summary>

/// <summary>
/// Reduction operations supported by the dispatcher.
/// </summary>
public enum ReductionOperation
{
    Sum,
    Min,
    Max,
    Product
}



#endregion