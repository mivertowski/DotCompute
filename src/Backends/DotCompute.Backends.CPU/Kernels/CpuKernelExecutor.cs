// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels.Models;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// High-performance kernel executor for CPU with SIMD vectorization and parallel execution.
/// </summary>
internal sealed partial class CpuKernelExecutor(CpuThreadPool threadPool, ILogger logger, SimdSummary? simdCapabilities = null)
{
    private readonly CpuThreadPool _threadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SimdSummary _simdCapabilities = simdCapabilities ?? SimdCapabilities.GetSummary();
    private long _executionCount;
    private double _totalExecutionTime;

    /// <summary>
    /// Executes a kernel with the specified arguments and work group configuration.
    /// </summary>
    public async ValueTask ExecuteAsync(
        KernelDefinition definition,
        KernelArguments arguments,
        KernelExecutionPlan executionPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(executionPlan);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Determine work distribution strategy
            var totalWorkItems = CalculateTotalWorkItems(arguments);
            var strategy = DetermineExecutionStrategy(totalWorkItems, executionPlan);

            LogExecutingKernel(_logger, definition.Name, totalWorkItems, strategy);

            switch (strategy)
            {
                case ExecutionStrategy.Sequential:
                    await ExecuteSequentialAsync(definition, arguments, executionPlan, cancellationToken).ConfigureAwait(false);
                    break;

                case ExecutionStrategy.Parallel:
                    await ExecuteParallelAsync(definition, arguments, executionPlan, cancellationToken).ConfigureAwait(false);
                    break;

                case ExecutionStrategy.Vectorized:
                    await ExecuteVectorizedAsync(definition, arguments, executionPlan, cancellationToken).ConfigureAwait(false);
                    break;

                case ExecutionStrategy.ParallelVectorized:
                    await ExecuteParallelVectorizedAsync(definition, arguments, executionPlan, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported execution strategy: {strategy}");
            }

            stopwatch.Stop();
            UpdatePerformanceMetrics(stopwatch.Elapsed.TotalMilliseconds);

            LogKernelExecuted(_logger, definition.Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            LogExecutionError(_logger, ex, definition.Name);
            throw;
        }
    }

    private static async ValueTask ExecuteSequentialAsync(
        KernelDefinition definition,
        KernelArguments arguments,
        KernelExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        // Simple sequential execution for small workloads
        var totalItems = CalculateTotalWorkItems(arguments);

        for (long i = 0; i < totalItems && !cancellationToken.IsCancellationRequested; i++)
        {
            var workItemId = new long[] { i };
            ExecuteWorkItem(definition, arguments, workItemId, executionPlan);
        }

        await Task.CompletedTask;
    }

    private async ValueTask ExecuteParallelAsync(
        KernelDefinition definition,
        KernelArguments arguments,
        KernelExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        var totalItems = CalculateTotalWorkItems(arguments);
        var workerCount = _threadPool.WorkerCount;
        var itemsPerWorker = (totalItems + workerCount - 1) / workerCount;

        var tasks = new Task[workerCount];

        for (var workerId = 0; workerId < workerCount; workerId++)
        {
            var localWorkerId = workerId;
            var startIndex = localWorkerId * itemsPerWorker;
            var endIndex = Math.Min(startIndex + itemsPerWorker, totalItems);

            tasks[workerId] = _threadPool.EnqueueAsync(() =>
            {
                for (var i = startIndex; i < endIndex && !cancellationToken.IsCancellationRequested; i++)
                {
                    var workItemId = new long[] { i };
                    ExecuteWorkItem(definition, arguments, workItemId, executionPlan);
                }
            }, cancellationToken).AsTask();
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async ValueTask ExecuteVectorizedAsync(
        KernelDefinition definition,
        KernelArguments arguments,
        KernelExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        // Single-threaded vectorized execution
        var totalItems = CalculateTotalWorkItems(arguments);
        var vectorWidth = executionPlan.VectorWidth;
        var elementsPerVector = GetElementsPerVector(vectorWidth);

        if (TryGetVectorizedBuffers(arguments, out var vectorizedArgs))
        {
            await ExecuteVectorizedKernelAsync(definition, vectorizedArgs, totalItems, elementsPerVector, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Fallback to scalar execution
            await ExecuteSequentialAsync(definition, arguments, executionPlan, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ExecuteParallelVectorizedAsync(
        KernelDefinition definition,
        KernelArguments arguments,
        KernelExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        var totalItems = CalculateTotalWorkItems(arguments);
        var vectorWidth = executionPlan.VectorWidth;
        var elementsPerVector = GetElementsPerVector(vectorWidth);
        var vectorizedItems = (totalItems + elementsPerVector - 1) / elementsPerVector;

        var workerCount = _threadPool.WorkerCount;
        var itemsPerWorker = (vectorizedItems + workerCount - 1) / workerCount;

        if (!TryGetVectorizedBuffers(arguments, out var vectorizedArgs))
        {
            // Fallback to parallel scalar execution
            await ExecuteParallelAsync(definition, arguments, executionPlan, cancellationToken).ConfigureAwait(false);
            return;
        }

        var tasks = new Task[workerCount];

        for (var workerId = 0; workerId < workerCount; workerId++)
        {
            var localWorkerId = workerId;
            var startVector = localWorkerId * itemsPerWorker;
            var endVector = Math.Min(startVector + itemsPerWorker, vectorizedItems);

            tasks[workerId] = _threadPool.EnqueueAsync(() =>
            {
                // Re-obtain vectorized buffers inside the lambda to avoid ref struct capture
                if (TryGetVectorizedBuffers(arguments, out var localVectorizedArgs))
                {
                    ExecuteVectorizedWorker(definition, localVectorizedArgs, startVector, endVector, elementsPerVector, vectorWidth, cancellationToken);
                }
            }, cancellationToken).AsTask();
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static ValueTask ExecuteVectorizedKernelAsync(
        KernelDefinition definition,
        VectorizedBuffers vectorizedArgs,
        long totalItems,
        int elementsPerVector,
        CancellationToken cancellationToken)
    {
        var vectorizedItems = (totalItems + elementsPerVector - 1) / elementsPerVector;

        for (var vectorIndex = 0L; vectorIndex < vectorizedItems && !cancellationToken.IsCancellationRequested; vectorIndex++)
        {
            var startElement = vectorIndex * elementsPerVector;
            var elementsToProcess = Math.Min(elementsPerVector, totalItems - startElement);

            ExecuteVectorizedOperation(definition, vectorizedArgs, startElement, elementsToProcess, elementsPerVector);
        }

        return ValueTask.CompletedTask;
    }

    private static void ExecuteVectorizedWorker(
        KernelDefinition definition,
        VectorizedBuffers vectorizedArgs,
        long startVector,
        long endVector,
        int elementsPerVector,
        int vectorWidth,
        CancellationToken cancellationToken)
    {
        for (var vectorIndex = startVector; vectorIndex < endVector && !cancellationToken.IsCancellationRequested; vectorIndex++)
        {
            var startElement = vectorIndex * elementsPerVector;
            ExecuteVectorizedOperation(definition, vectorizedArgs, startElement, elementsPerVector, elementsPerVector);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVectorizedOperation(
        KernelDefinition definition,
        VectorizedBuffers args,
        long startElement,
        long elementsToProcess,
        int vectorElements)
    {
        var operationType = InferOperationType(definition);
        var offset = startElement * sizeof(float);

        switch (operationType)
        {
            case VectorOperationType.Add:
                ExecuteVectorizedAdd(args, offset, vectorElements);
                break;

            case VectorOperationType.Multiply:
                ExecuteVectorizedMultiply(args, offset, vectorElements);
                break;

            case VectorOperationType.Subtract:
                ExecuteVectorizedSubtract(args, offset, vectorElements);
                break;

            case VectorOperationType.Divide:
                ExecuteVectorizedDivide(args, offset, vectorElements);
                break;

            case VectorOperationType.Fma:
                ExecuteVectorizedFma(args, offset, vectorElements);
                break;

            case VectorOperationType.Sqrt:
                ExecuteVectorizedSqrt(args, offset, vectorElements);
                break;

            case VectorOperationType.Min:
                ExecuteVectorizedMin(args, offset, vectorElements);
                break;

            case VectorOperationType.Max:
                ExecuteVectorizedMax(args, offset, vectorElements);
                break;

            default:
                // Fallback to scalar execution
                ExecuteScalarOperation(definition, args, startElement, elementsToProcess);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVectorizedAdd(VectorizedBuffers args, long offset, int vectorElements)
    {
        var input1 = args.Input1;
        var input2 = args.Input2;
        var output = args.Output;

        if (Vector512.IsHardwareAccelerated && vectorElements >= 16)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector512.Load(f1);
                var v2 = Vector512.Load(f2);
                var result = Vector512.Add(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector256.IsHardwareAccelerated && vectorElements >= 8)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector256.Load(f1);
                var v2 = Vector256.Load(f2);
                var result = Vector256.Add(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector128.IsHardwareAccelerated && vectorElements >= 4)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector128.Load(f1);
                var v2 = Vector128.Load(f2);
                var result = Vector128.Add(v1, v2);
                result.Store(fOut);
            }
        }
        else
        {
            // Scalar fallback
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                *fOut = *f1 + *f2;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVectorizedMultiply(VectorizedBuffers args, long offset, int vectorElements)
    {
        var input1 = args.Input1;
        var input2 = args.Input2;
        var output = args.Output;

        if (Vector512.IsHardwareAccelerated && vectorElements >= 16)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector512.Load(f1);
                var v2 = Vector512.Load(f2);
                var result = Vector512.Multiply(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector256.IsHardwareAccelerated && vectorElements >= 8)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector256.Load(f1);
                var v2 = Vector256.Load(f2);
                var result = Vector256.Multiply(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector128.IsHardwareAccelerated && vectorElements >= 4)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector128.Load(f1);
                var v2 = Vector128.Load(f2);
                var result = Vector128.Multiply(v1, v2);
                result.Store(fOut);
            }
        }
        else
        {
            // Scalar fallback
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                *fOut = *f1 * *f2;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVectorizedSubtract(VectorizedBuffers args, long offset, int vectorElements)
    {
        var input1 = args.Input1;
        var input2 = args.Input2;
        var output = args.Output;

        if (Vector512.IsHardwareAccelerated && vectorElements >= 16)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector512.Load(f1);
                var v2 = Vector512.Load(f2);
                var result = Vector512.Subtract(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector256.IsHardwareAccelerated && vectorElements >= 8)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector256.Load(f1);
                var v2 = Vector256.Load(f2);
                var result = Vector256.Subtract(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector128.IsHardwareAccelerated && vectorElements >= 4)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector128.Load(f1);
                var v2 = Vector128.Load(f2);
                var result = Vector128.Subtract(v1, v2);
                result.Store(fOut);
            }
        }
        else
        {
            // Scalar fallback
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                *fOut = *f1 - *f2;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVectorizedDivide(VectorizedBuffers args, long offset, int vectorElements)
    {
        var input1 = args.Input1;
        var input2 = args.Input2;
        var output = args.Output;

        if (Vector512.IsHardwareAccelerated && vectorElements >= 16)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector512.Load(f1);
                var v2 = Vector512.Load(f2);
                var result = Vector512.Divide(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector256.IsHardwareAccelerated && vectorElements >= 8)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector256.Load(f1);
                var v2 = Vector256.Load(f2);
                var result = Vector256.Divide(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector128.IsHardwareAccelerated && vectorElements >= 4)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector128.Load(f1);
                var v2 = Vector128.Load(f2);
                var result = Vector128.Divide(v1, v2);
                result.Store(fOut);
            }
        }
        else
        {
            // Scalar fallback with proper division by zero handling
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                if (Math.Abs(*f2) > float.Epsilon)
                {
                    *fOut = *f1 / *f2;
                }
                else
                {
                    *fOut = *f1 > 0 ? float.PositiveInfinity :
                           *f1 < 0 ? float.NegativeInfinity : float.NaN;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVectorizedFma(VectorizedBuffers args, long offset, int vectorElements)
    {
        var input1 = args.Input1;
        var input2 = args.Input2;
        var input3 = args.Input3;
        var output = args.Output;

        if (input3.IsEmpty)
        {
            return;
        }

        if (Vector512.IsHardwareAccelerated && vectorElements >= 16 && Avx512F.IsSupported)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* p3 = input3)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var f3 = (float*)(p3 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector512.Load(f1);
                var v2 = Vector512.Load(f2);
                var v3 = Vector512.Load(f3);
                var result = Avx512F.FusedMultiplyAdd(v1, v2, v3);
                result.Store(fOut);
            }
        }
        else if (Vector256.IsHardwareAccelerated && vectorElements >= 8 && Fma.IsSupported)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* p3 = input3)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var f3 = (float*)(p3 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector256.Load(f1);
                var v2 = Vector256.Load(f2);
                var v3 = Vector256.Load(f3);
                var result = Fma.MultiplyAdd(v1, v2, v3);
                result.Store(fOut);
            }
        }
        else
        {
            // Scalar FMA fallback
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* p3 = input3)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var f3 = (float*)(p3 + offset);
                var fOut = (float*)(pOut + offset);

                *fOut = (*f1 * *f2) + *f3;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVectorizedSqrt(VectorizedBuffers args, long offset, int vectorElements)
    {
        var input1 = args.Input1;
        var output = args.Output;

        if (Vector512.IsHardwareAccelerated && vectorElements >= 16)
        {
            fixed (byte* p1 = input1)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector512.Load(f1);
                var result = Vector512.Sqrt(v1);
                result.Store(fOut);
            }
        }
        else if (Vector256.IsHardwareAccelerated && vectorElements >= 8)
        {
            fixed (byte* p1 = input1)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector256.Load(f1);
                var result = Vector256.Sqrt(v1);
                result.Store(fOut);
            }
        }
        else if (Vector128.IsHardwareAccelerated && vectorElements >= 4)
        {
            fixed (byte* p1 = input1)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector128.Load(f1);
                var result = Vector128.Sqrt(v1);
                result.Store(fOut);
            }
        }
        else
        {
            // Scalar fallback
            fixed (byte* p1 = input1)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var fOut = (float*)(pOut + offset);

                *fOut = MathF.Sqrt(*f1);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVectorizedMin(VectorizedBuffers args, long offset, int vectorElements)
    {
        var input1 = args.Input1;
        var input2 = args.Input2;
        var output = args.Output;

        if (Vector512.IsHardwareAccelerated && vectorElements >= 16)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector512.Load(f1);
                var v2 = Vector512.Load(f2);
                var result = Vector512.Min(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector256.IsHardwareAccelerated && vectorElements >= 8)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector256.Load(f1);
                var v2 = Vector256.Load(f2);
                var result = Vector256.Min(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector128.IsHardwareAccelerated && vectorElements >= 4)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector128.Load(f1);
                var v2 = Vector128.Load(f2);
                var result = Vector128.Min(v1, v2);
                result.Store(fOut);
            }
        }
        else
        {
            // Scalar fallback
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                *fOut = MathF.Min(*f1, *f2);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVectorizedMax(VectorizedBuffers args, long offset, int vectorElements)
    {
        var input1 = args.Input1;
        var input2 = args.Input2;
        var output = args.Output;

        if (Vector512.IsHardwareAccelerated && vectorElements >= 16)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector512.Load(f1);
                var v2 = Vector512.Load(f2);
                var result = Vector512.Max(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector256.IsHardwareAccelerated && vectorElements >= 8)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector256.Load(f1);
                var v2 = Vector256.Load(f2);
                var result = Vector256.Max(v1, v2);
                result.Store(fOut);
            }
        }
        else if (Vector128.IsHardwareAccelerated && vectorElements >= 4)
        {
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                var v1 = Vector128.Load(f1);
                var v2 = Vector128.Load(f2);
                var result = Vector128.Max(v1, v2);
                result.Store(fOut);
            }
        }
        else
        {
            // Scalar fallback
            fixed (byte* p1 = input1)
            fixed (byte* p2 = input2)
            fixed (byte* pOut = output)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                *fOut = MathF.Max(*f1, *f2);
            }
        }
    }

    private static void ExecuteScalarOperation(KernelDefinition definition, VectorizedBuffers args, long startElement, long elementsToProcess)
    {
        // Fallback scalar execution for unsupported vector operations
        for (var i = 0L; i < elementsToProcess; i++)
        {
            var offset = (startElement + i) * sizeof(float);
            // Simple add operation as fallback
            ExecuteVectorizedAdd(args, offset, 1);
        }
    }

    private static long CalculateTotalWorkItems(KernelArguments arguments)
    {
        // For now, assume the size is determined by buffer size
        // In a real implementation, this would be configurable
        var argumentsArray = arguments.Arguments.ToArray();
        if (argumentsArray.Any(arg => arg is IUnifiedMemoryBuffer))
        {
            var firstBuffer = argumentsArray.OfType<IUnifiedMemoryBuffer>().First();
            return firstBuffer.SizeInBytes / sizeof(float);
        }

        return 1024; // Default work size
    }

    private ExecutionStrategy DetermineExecutionStrategy(long totalWorkItems, KernelExecutionPlan executionPlan)
    {
        var hasVectorization = executionPlan.UseVectorization && _simdCapabilities.IsHardwareAccelerated;
        var useParallel = totalWorkItems > 1000 && _threadPool.WorkerCount > 1;

        if (hasVectorization && useParallel)
        {
            return ExecutionStrategy.ParallelVectorized;
        }

        if (hasVectorization)
        {
            return ExecutionStrategy.Vectorized;
        }

        if (useParallel)
        {
            return ExecutionStrategy.Parallel;
        }

        return ExecutionStrategy.Sequential;
    }

    private static int GetElementsPerVector(int vectorWidth) => vectorWidth / 32; // Assuming 32-bit floats

    private static bool TryGetVectorizedBuffers(KernelArguments arguments, out VectorizedBuffers vectorizedArgs)
    {
        vectorizedArgs = default;

        var argumentsArray = arguments.Arguments.ToArray();
        var buffers = argumentsArray.OfType<CpuMemoryBuffer>().ToArray();
        if (buffers.Length < 2)
        {
            return false;
        }

        vectorizedArgs = new VectorizedBuffers
        {
            Input1 = buffers[0].GetMemory().Span,
            Input2 = buffers.Length > 1 ? buffers[1].GetMemory().Span : default,
            Input3 = buffers.Length > 2 ? buffers[2].GetMemory().Span : default,
            Output = buffers.Length > 2 ? buffers[2].GetMemory().Span : buffers[^1].GetMemory().Span
        };

        return true;
    }

    private static VectorOperationType InferOperationType(KernelDefinition definition)
    {
        var name = definition.Name.ToUpperInvariant();

        if (name.Contains("ADD", StringComparison.Ordinal))
        {
            return VectorOperationType.Add;
        }

        if (name.Contains("MUL", StringComparison.Ordinal))
        {
            return VectorOperationType.Multiply;
        }

        if (name.Contains("SUB", StringComparison.Ordinal))
        {
            return VectorOperationType.Subtract;
        }

        if (name.Contains("DIV", StringComparison.Ordinal))
        {
            return VectorOperationType.Divide;
        }

        if (name.Contains("FMA", StringComparison.Ordinal))
        {
            return VectorOperationType.Fma;
        }

        if (name.Contains("SQRT", StringComparison.Ordinal))
        {
            return VectorOperationType.Sqrt;
        }

        if (name.Contains("MIN", StringComparison.Ordinal))
        {
            return VectorOperationType.Min;
        }

        if (name.Contains("MAX", StringComparison.Ordinal))
        {
            return VectorOperationType.Max;
        }

        return VectorOperationType.Add; // Default to add
    }

    private static void ExecuteWorkItem(KernelDefinition definition, KernelArguments arguments, long[] workItemId, KernelExecutionPlan executionPlan)
    {
        // Basic scalar execution for a single work item
        var linearIndex = workItemId[0];

        if (TryGetVectorizedBuffers(arguments, out var vectorizedArgs))
        {
            var offset = linearIndex * sizeof(float);
            ExecuteVectorizedAdd(vectorizedArgs, offset, 1);
        }
    }

    private void UpdatePerformanceMetrics(double executionTimeMs)
    {
        _ = Interlocked.Increment(ref _executionCount);

        // Thread-safe update of total execution time
        var currentTotal = _totalExecutionTime;
        var newTotal = currentTotal + executionTimeMs;

        while (Interlocked.CompareExchange(ref _totalExecutionTime, newTotal, currentTotal) != currentTotal)
        {
            currentTotal = _totalExecutionTime;
            newTotal = currentTotal + executionTimeMs;
        }
    }

    /// <summary>
    /// Gets performance metrics for the executor.
    /// </summary>
    public ExecutorPerformanceMetrics GetPerformanceMetrics()
    {
        var execCount = Interlocked.Read(ref _executionCount);
        var totalTime = _totalExecutionTime;

        return new ExecutorPerformanceMetrics
        {
            ExecutionCount = execCount,
            TotalExecutionTimeMs = totalTime,
            AverageExecutionTimeMs = execCount > 0 ? totalTime / execCount : 0,
            ThreadPoolStatistics = _threadPool.GetStatistics()
        };
    }
}
/// <summary>
/// An execution strategy enumeration.
/// </summary>

/// <summary>
/// Execution strategy for kernel execution.
/// </summary>
internal enum ExecutionStrategy
{
    Sequential,
    Parallel,
    Vectorized,
    ParallelVectorized
}
/// <summary>
/// An vector operation type enumeration.
/// </summary>

/// <summary>
/// Vector operation type for optimized execution.
/// </summary>
internal enum VectorOperationType
{
    Add,
    Multiply,
    Subtract,
    Divide,
    Fma,
    Sqrt,
    Min,
    Max
}

/// <summary>
/// Vectorized buffer arguments for SIMD operations.
/// </summary>
internal readonly ref struct VectorizedBuffers
{
    /// <summary>
    /// Gets or sets the input1.
    /// </summary>
    /// <value>The input1.</value>
    public required Span<byte> Input1 { get; init; }
    /// <summary>
    /// Gets or sets the input2.
    /// </summary>
    /// <value>The input2.</value>
    public Span<byte> Input2 { get; init; }
    /// <summary>
    /// Gets or sets the input3.
    /// </summary>
    /// <value>The input3.</value>
    public Span<byte> Input3 { get; init; }
    /// <summary>
    /// Gets or sets the output.
    /// </summary>
    /// <value>The output.</value>
    public required Span<byte> Output { get; init; }
}

/// <summary>
/// Performance metrics for the kernel executor.
/// </summary>
public sealed class ExecutorPerformanceMetrics
{
    /// <summary>
    /// Gets the execution count.
    /// </summary>
    /// <value>
    /// The execution count.
    /// </value>
    public required long ExecutionCount { get; init; }

    /// <summary>
    /// Gets the total execution time ms.
    /// </summary>
    /// <value>
    /// The total execution time ms.
    /// </value>
    public required double TotalExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the average execution time ms.
    /// </summary>
    /// <value>
    /// The average execution time ms.
    /// </value>
    public required double AverageExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the thread pool statistics.
    /// </summary>
    /// <value>
    /// The thread pool statistics.
    /// </value>
    public required ThreadPoolStatistics ThreadPoolStatistics { get; init; }
}
