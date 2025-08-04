// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;
using CoreICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using CoreKernelDefinition = DotCompute.Abstractions.KernelDefinition;
using CoreKernelExecutionContext = DotCompute.Core.KernelExecutionContext;
using IMemoryBuffer = DotCompute.Abstractions.IMemoryBuffer;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Represents a compiled kernel for CPU execution with vectorization support.
/// </summary>
internal sealed class CpuCompiledKernel : CoreICompiledKernel
{
    private readonly CoreKernelDefinition _definition;
    private readonly KernelExecutionPlan _executionPlan;
    private readonly CpuThreadPool _threadPool;
    private readonly ILogger _logger;
    private readonly SimdCodeGenerator _codeGenerator;
    private readonly SimdKernelExecutor? _kernelExecutor;
    private Delegate? _compiledDelegate;
    private long _executionCount;
    private double _totalExecutionTimeMs;
    private int _disposed;

    public CpuCompiledKernel(
        CoreKernelDefinition definition,
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
    }

    // Interface implementation for Core.ICompiledKernel - no longer needed
    // CoreKernelDefinition CoreICompiledKernel.Definition => _definition;

    public ValueTask ExecuteAsync(CoreKernelExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Convert KernelExecutionContext to KernelArguments for internal processing
        var arguments = ConvertContextToArguments(context);
        return ExecuteAsync(arguments, cancellationToken);
    }

    public CoreKernelDefinition Definition => _definition;

    public string Name => _definition.Name;

    public string Id => $"{_definition.Name}_{_definition.GetHashCode():X8}";

    public string Source => _definition.Code != null ? "[Bytecode]" : "[Unknown]";

    public string EntryPoint => _definition.Name;

    public bool IsValid => _disposed == 0;

    /// <summary>
    /// Sets the compiled delegate for direct kernel execution.
    /// </summary>
    public void SetCompiledDelegate(Delegate compiledDelegate)
    {
        _compiledDelegate = compiledDelegate ?? throw new ArgumentNullException(nameof(compiledDelegate));
    }

    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        // KernelArguments is non-nullable, so no need for null check

        // Convert KernelArguments to KernelExecutionContext for internal processing
        var context = new CoreKernelExecutionContext
        {
            Name = _definition.Name,
            WorkDimensions = new[] { 1024L }, // Default work size - should be configurable
            Arguments = arguments.Arguments.ToArray()
        };

        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug(
            "Executing kernel '{KernelName}' with global work size: [{WorkSize}], vectorization: {Vectorization}",
            _definition.Name,
            string.Join(", ", context.WorkDimensions),
            _executionPlan.UseVectorization ? $"{_executionPlan.VectorWidth}-bit" : "disabled");

        // Validate work dimensions
        if (context.WorkDimensions.Count == 0)
        {
            throw new ArgumentException("Global work size must have at least one dimension", nameof(arguments));
        }

        if (context.WorkDimensions.Count > 3)
        {
            throw new ArgumentException("Global work size cannot exceed 3 dimensions", nameof(arguments));
        }

        if (context.WorkDimensions.Any(dim => dim <= 0))
        {
            throw new ArgumentException("All work dimensions must be positive", nameof(arguments));
        }

        // Validate arguments
        if (context.Arguments == null || context.Arguments.Length == 0)
        {
            throw new ArgumentException("Kernel requires at least one argument", nameof(arguments));
        }

        // Validate argument types are supported
        foreach (var arg in context.Arguments)
        {
            if (arg != null && !IsSupportedArgumentType(arg.GetType()))
            {
                throw new ArgumentException($"Unsupported argument type: {arg.GetType().Name}", nameof(arguments));
            }
        }

        // Calculate total work items
        long totalWorkItems = GetTotalWorkItems(context.WorkDimensions);

        // Determine work distribution with vectorization
        var workerCount = _threadPool.WorkerCount;
        var vectorizedWorkItems = _executionPlan.UseVectorization ?
            (totalWorkItems + _executionPlan.VectorizationFactor - 1) / _executionPlan.VectorizationFactor :
            totalWorkItems;
        var workItemsPerWorker = (vectorizedWorkItems + workerCount - 1) / workerCount;

        // Create tasks for parallel execution
        var tasks = new Task[workerCount];
        var barrier = new Barrier(workerCount);

        for (int workerId = 0; workerId < workerCount; workerId++)
        {
            var localWorkerId = workerId;
            var startIndex = localWorkerId * workItemsPerWorker;
            var endIndex = Math.Min(startIndex + workItemsPerWorker, vectorizedWorkItems);

            tasks[workerId] = Task.Run(async () =>
            {
                await _threadPool.EnqueueAsync(() =>
                {
                    ExecuteWorkItems(
                        context,
                        startIndex,
                        endIndex,
                        barrier,
                        cancellationToken);
                }, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
        }

        // Wait for all workers to complete
        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Dispose the barrier
        barrier.Dispose();

        stopwatch.Stop();

        // Update performance metrics with atomic add to avoid race conditions
        Interlocked.Increment(ref _executionCount);
        // Use Interlocked.Add for proper atomic addition instead of Exchange
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        var doubleAsLong = BitConverter.DoubleToInt64Bits(elapsedMs);
        long currentBits, newBits;
        do
        {
            currentBits = Interlocked.Read(ref Unsafe.As<double, long>(ref _totalExecutionTimeMs));
            var currentValue = BitConverter.Int64BitsToDouble(currentBits);
            var newValue = currentValue + elapsedMs;
            newBits = BitConverter.DoubleToInt64Bits(newValue);
        } while (Interlocked.CompareExchange(ref Unsafe.As<double, long>(ref _totalExecutionTimeMs), newBits, currentBits) != currentBits);

        _logger.LogDebug("Kernel '{KernelName}' execution completed in {ElapsedMs:F2}ms",
            _definition.Name, stopwatch.Elapsed.TotalMilliseconds);

        // Log performance stats periodically
        if (_executionCount % 100 == 0)
        {
            var avgTime = _totalExecutionTimeMs / _executionCount;
            _logger.LogInformation(
                "Kernel '{KernelName}' performance: {ExecutionCount} executions, avg time: {AvgTime:F2}ms",
                _definition.Name, _executionCount, avgTime);
        }
    }

    private void ExecuteWorkItems(
        CoreKernelExecutionContext context,
        long startIndex,
        long endIndex,
        Barrier barrier,
        CancellationToken cancellationToken)
    {
        // Set up execution context with vectorization support
        var executionContext = new VectorizedExecutionContext
        {
            KernelContext = context,
            ExecutionPlan = _executionPlan,
            CancellationToken = cancellationToken
        };

        if (_executionPlan.UseVectorization)
        {
            ExecuteVectorizedWorkItems(executionContext, startIndex, endIndex);
        }
        else
        {
            ExecuteScalarWorkItems(executionContext, startIndex, endIndex);
        }

        // Synchronize with other workers if needed
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

    private void ExecuteScalarWorkItems(
        VectorizedExecutionContext context,
        long startIndex,
        long endIndex)
    {
        for (long i = startIndex; i < endIndex && !context.CancellationToken.IsCancellationRequested; i++)
        {
            var workItemId = GetWorkItemId(i, context.KernelContext.WorkDimensions ?? Array.Empty<long>());
            ExecuteSingleWorkItem(context.KernelContext, workItemId);
        }
    }

    private void ExecuteVectorizedWorkItems(
        VectorizedExecutionContext context,
        long startIndex,
        long endIndex)
    {
        var vectorFactor = context.ExecutionPlan.VectorizationFactor;
        var vectorWidth = context.ExecutionPlan.VectorWidth;

        for (long i = startIndex; i < endIndex && !context.CancellationToken.IsCancellationRequested; i++)
        {
            var baseIndex = i * vectorFactor;
            var workItemIds = new long[vectorFactor][];

            // Prepare vectorized work items
            for (int v = 0; v < vectorFactor; v++)
            {
                var actualIndex = baseIndex + v;
                if (actualIndex < GetTotalWorkItems(context.KernelContext.WorkDimensions ?? Array.Empty<long>()))
                {
                    workItemIds[v] = GetWorkItemId(actualIndex, context.KernelContext.WorkDimensions ?? Array.Empty<long>());
                }
            }

            // Execute vectorized kernel
            ExecuteVectorizedWorkItem(context, workItemIds, vectorWidth);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteVectorizedWorkItem(
        VectorizedExecutionContext context,
        long[][] workItemIds,
        int vectorWidth)
    {
        // Use the pre-compiled kernel executor if available
        if (_kernelExecutor != null && TryGetBufferArguments(context.KernelContext, out var input1, out var input2, out var output))
        {
            // Calculate element count and execute using the optimized SIMD executor
            var elementCount = workItemIds.Length * context.ExecutionPlan.VectorizationFactor;
            var baseIndex = workItemIds[0]?[0] ?? 0;

            if (input1 is CpuMemoryBuffer cpuInput1 &&
                input2 is CpuMemoryBuffer cpuInput2 &&
                output is CpuMemoryBuffer cpuOutput)
            {
                var mem1 = cpuInput1.GetMemory();
                var mem2 = cpuInput2.GetMemory();
                var memOut = cpuOutput.GetMemory();

                // Calculate offset for this work item batch
                var offset = (int)(baseIndex * sizeof(float));

                // Create spans for the kernel executor
                var input1Span = mem1.Span.Slice(offset);
                var input2Span = mem2.Span.Slice(offset);
                var outputSpan = memOut.Span.Slice(offset);

                // Execute using the optimized SIMD kernel
                _kernelExecutor.Execute(
                    input1Span,
                    input2Span,
                    outputSpan,
                    elementCount,
                    vectorWidth);

                return;
            }
        }

        // Fall back to inline SIMD execution if no pre-compiled executor
        switch (vectorWidth)
        {
            case 512 when Avx512F.IsSupported:
                ExecuteAvx512Kernel(context, workItemIds);
                break;
            case 256 when Avx2.IsSupported:
                ExecuteAvx2Kernel(context, workItemIds);
                break;
            case 128 when Sse2.IsSupported:
                ExecuteSseKernel(context, workItemIds);
                break;
            default:
                // Fall back to scalar execution
                foreach (var workItemId in workItemIds)
                {
                    if (workItemId != null)
                    {
                        ExecuteSingleWorkItem(context.KernelContext, workItemId);
                    }
                }
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteAvx512Kernel(VectorizedExecutionContext context, long[][] workItemIds)
    {
        // AVX512 vectorized execution (16 floats at once)
        if (!TryGetBufferArguments(context.KernelContext, out var input1, out var input2, out var output))
        {
            // Fall back to scalar for non-buffer kernels
            foreach (var workItemId in workItemIds)
            {
                if (workItemId != null)
                {
                    ExecuteSingleWorkItem(context.KernelContext, workItemId);
                }
            }
            return;
        }

        // Access CPU memory buffers directly
        if (input1 is CpuMemoryBuffer cpuInput1 &&
            input2 is CpuMemoryBuffer cpuInput2 &&
            output is CpuMemoryBuffer cpuOutput)
        {
            var mem1 = cpuInput1.GetMemory();
            var mem2 = cpuInput2.GetMemory();
            var memOut = cpuOutput.GetMemory();

            // Calculate starting index (assuming 1D kernel)
            var baseIndex = workItemIds[0]?[0] ?? 0;
            var offset = (int)(baseIndex * sizeof(float));

            // Ensure we have enough data for a full vector
            var remainingElements = (mem1.Length - offset) / sizeof(float);
            if (remainingElements >= 16 && Avx512F.IsSupported)
            {
                fixed (byte* pIn1 = mem1.Span)
                fixed (byte* pIn2 = mem2.Span)
                fixed (byte* pOut = memOut.Span)
                {
                    var f1 = (float*)(pIn1 + offset);
                    var f2 = (float*)(pIn2 + offset);
                    var fOut = (float*)(pOut + offset);

                    var v1 = Avx512F.LoadVector512(f1);
                    var v2 = Avx512F.LoadVector512(f2);
                    var result = Avx512F.Add(v1, v2);
                    Avx512F.Store(fOut, result);
                }
            }
            else
            {
                // Fall back to scalar execution for remaining items
                foreach (var workItemId in workItemIds)
                {
                    if (workItemId != null)
                    {
                        ExecuteSingleWorkItem(context.KernelContext, workItemId);
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteAvx2Kernel(VectorizedExecutionContext context, long[][] workItemIds)
    {
        // AVX2 vectorized execution (8 floats at once)
        if (!TryGetBufferArguments(context.KernelContext, out var input1, out var input2, out var output))
        {
            // Fall back to scalar for non-buffer kernels
            foreach (var workItemId in workItemIds)
            {
                if (workItemId != null)
                {
                    ExecuteSingleWorkItem(context.KernelContext, workItemId);
                }
            }
            return;
        }

        // Access CPU memory buffers directly
        if (input1 is CpuMemoryBuffer cpuInput1 &&
            input2 is CpuMemoryBuffer cpuInput2 &&
            output is CpuMemoryBuffer cpuOutput)
        {
            var mem1 = cpuInput1.GetMemory();
            var mem2 = cpuInput2.GetMemory();
            var memOut = cpuOutput.GetMemory();

            // Calculate starting index (assuming 1D kernel)
            var baseIndex = workItemIds[0]?[0] ?? 0;
            var offset = (int)(baseIndex * sizeof(float));

            // Ensure we have enough data for a full vector
            var remainingElements = (mem1.Length - offset) / sizeof(float);
            if (remainingElements >= 8 && Avx2.IsSupported)
            {
                fixed (byte* pIn1 = mem1.Span)
                fixed (byte* pIn2 = mem2.Span)
                fixed (byte* pOut = memOut.Span)
                {
                    var f1 = (float*)(pIn1 + offset);
                    var f2 = (float*)(pIn2 + offset);
                    var fOut = (float*)(pOut + offset);

                    var v1 = Avx.LoadVector256(f1);
                    var v2 = Avx.LoadVector256(f2);
                    var result = Avx.Add(v1, v2);
                    Avx.Store(fOut, result);
                }
            }
            else
            {
                // Fall back to scalar execution for remaining items
                foreach (var workItemId in workItemIds)
                {
                    if (workItemId != null)
                    {
                        ExecuteSingleWorkItem(context.KernelContext, workItemId);
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteSseKernel(VectorizedExecutionContext context, long[][] workItemIds)
    {
        // SSE vectorized execution (4 floats at once)
        if (!TryGetBufferArguments(context.KernelContext, out var input1, out var input2, out var output))
        {
            // Fall back to scalar for non-buffer kernels
            foreach (var workItemId in workItemIds)
            {
                if (workItemId != null)
                {
                    ExecuteSingleWorkItem(context.KernelContext, workItemId);
                }
            }
            return;
        }

        // Access CPU memory buffers directly
        if (input1 is CpuMemoryBuffer cpuInput1 &&
            input2 is CpuMemoryBuffer cpuInput2 &&
            output is CpuMemoryBuffer cpuOutput)
        {
            var mem1 = cpuInput1.GetMemory();
            var mem2 = cpuInput2.GetMemory();
            var memOut = cpuOutput.GetMemory();

            // Calculate starting index (assuming 1D kernel)
            var baseIndex = workItemIds[0]?[0] ?? 0;
            var offset = (int)(baseIndex * sizeof(float));

            // Ensure we have enough data for a full vector
            var remainingElements = (mem1.Length - offset) / sizeof(float);
            if (remainingElements >= 4 && Sse.IsSupported)
            {
                fixed (byte* pIn1 = mem1.Span)
                fixed (byte* pIn2 = mem2.Span)
                fixed (byte* pOut = memOut.Span)
                {
                    var f1 = (float*)(pIn1 + offset);
                    var f2 = (float*)(pIn2 + offset);
                    var fOut = (float*)(pOut + offset);

                    var v1 = Sse.LoadVector128(f1);
                    var v2 = Sse.LoadVector128(f2);
                    var result = Sse.Add(v1, v2);
                    Sse.Store(fOut, result);
                }
            }
            else
            {
                // Fall back to scalar execution for remaining items
                foreach (var workItemId in workItemIds)
                {
                    if (workItemId != null)
                    {
                        ExecuteSingleWorkItem(context.KernelContext, workItemId);
                    }
                }
            }
        }
    }

    private static long[] GetWorkItemId(long linearIndex, IReadOnlyList<long> globalWorkSize)
    {
        var dimensions = globalWorkSize.Count;
        var workItemId = new long[dimensions];

        for (int i = dimensions - 1; i >= 0; i--)
        {
            workItemId[i] = linearIndex % globalWorkSize[i];
            linearIndex /= globalWorkSize[i];
        }

        return workItemId;
    }

    private static long GetTotalWorkItems(IReadOnlyList<long> globalWorkSize)
    {
        long total = 1;
        foreach (var size in globalWorkSize)
        {
            total *= size;
        }
        return total;
    }

    private void ExecuteSingleWorkItem(CoreKernelExecutionContext context, long[] workItemId)
    {
        // If we have a compiled delegate, use it
        if (_compiledDelegate != null)
        {
            try
            {
                // Prepare arguments for the delegate
                var argCount = context.Arguments?.Length ?? 0;
                var delegateArgs = new object[argCount + 1];
                if (context.Arguments != null)
                {
                    Array.Copy(context.Arguments, delegateArgs, argCount);
                }
                delegateArgs[delegateArgs.Length - 1] = workItemId;

                // Invoke the compiled kernel
                _compiledDelegate.DynamicInvoke(delegateArgs);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to execute compiled delegate, falling back to default implementation");
            }
        }

        // Default implementation
        ExecuteSingleWorkItemDefault(context, workItemId);
    }

    private static void ExecuteSingleWorkItemDefault(CoreKernelExecutionContext context, long[] workItemId)
    {
        // Extract arguments based on their types
        var args = context.Arguments;
        if (args == null || args.Length < 2)
            return; // Need at least 2 arguments for most operations

        // For demonstration, we'll implement a simple vector addition kernel
        // In production, this would dispatch to the actual compiled kernel code

        // Calculate linear index from work item ID
        long linearIndex = workItemId[0];
        if (context.WorkDimensions != null)
        {
            for (int i = 1; i < workItemId.Length; i++)
            {
                linearIndex = linearIndex * context.WorkDimensions[i] + workItemId[i];
            }
        }

        // Handle different kernel patterns
        if (args.Length >= 3 &&
            args[0] is IMemoryBuffer input1 &&
            args[1] is IMemoryBuffer input2 &&
            args[2] is IMemoryBuffer output)
        {
            // Vector operation pattern (e.g., C = A + B)
            ExecuteVectorOperation(input1, input2, output, linearIndex);
        }
        else if (args.Length >= 2 &&
                 args[0] is IMemoryBuffer input &&
                 args[1] is IMemoryBuffer outputBuf)
        {
            // Unary operation pattern (e.g., B = sqrt(A))
            ExecuteUnaryOperation(input, outputBuf, linearIndex);
        }
        else if (args.Length >= 1 && args[0] is IMemoryBuffer buffer)
        {
            // In-place operation pattern
            ExecuteInPlaceOperation(buffer, linearIndex, args);
        }
        // Add more patterns as needed
    }

    private static unsafe void ExecuteVectorOperation(
        IMemoryBuffer input1,
        IMemoryBuffer input2,
        IMemoryBuffer output,
        long index)
    {
        // Get element size based on buffer type (defaulting to float for numeric operations)
        const int elementSize = sizeof(float);
        var offset = index * elementSize;

        // Check bounds
        if (offset + elementSize > input1.SizeInBytes ||
            offset + elementSize > input2.SizeInBytes ||
            offset + elementSize > output.SizeInBytes)
        {
            return;
        }

        // For CPU backend, access memory directly
        if (input1 is CpuMemoryBuffer cpuInput1 &&
            input2 is CpuMemoryBuffer cpuInput2 &&
            output is CpuMemoryBuffer cpuOutput)
        {
            var mem1 = cpuInput1.GetMemory();
            var mem2 = cpuInput2.GetMemory();
            var memOut = cpuOutput.GetMemory();

            // Perform the operation (vector addition)
            fixed (byte* p1 = mem1.Span)
            fixed (byte* p2 = mem2.Span)
            fixed (byte* pOut = memOut.Span)
            {
                var f1 = (float*)(p1 + offset);
                var f2 = (float*)(p2 + offset);
                var fOut = (float*)(pOut + offset);

                *fOut = *f1 + *f2;
            }
        }
    }

    private static unsafe void ExecuteUnaryOperation(
        IMemoryBuffer input,
        IMemoryBuffer output,
        long index)
    {
        // Get element size based on buffer type (defaulting to float for numeric operations)
        const int elementSize = sizeof(float);
        var offset = index * elementSize;

        // Check bounds
        if (offset + elementSize > input.SizeInBytes ||
            offset + elementSize > output.SizeInBytes)
        {
            return;
        }

        // For CPU backend, access memory directly
        if (input is CpuMemoryBuffer cpuInput &&
            output is CpuMemoryBuffer cpuOutput)
        {
            var memIn = cpuInput.GetMemory();
            var memOut = cpuOutput.GetMemory();

            // Perform a unary operation (e.g., square root)
            fixed (byte* pIn = memIn.Span)
            fixed (byte* pOut = memOut.Span)
            {
                var fIn = (float*)(pIn + offset);
                var fOut = (float*)(pOut + offset);

                *fOut = MathF.Sqrt(*fIn);
            }
        }
    }

    private static unsafe void ExecuteInPlaceOperation(
        IMemoryBuffer buffer,
        long index,
        object[]? args)
    {
        // Get element size based on buffer type (defaulting to float for numeric operations)
        const int elementSize = sizeof(float);
        var offset = index * elementSize;

        // Check bounds
        if (offset + elementSize > buffer.SizeInBytes)
        {
            return;
        }

        // For CPU backend, access memory directly
        if (buffer is CpuMemoryBuffer cpuBuffer)
        {
            var mem = cpuBuffer.GetMemory();

            // Perform an in-place operation (e.g., scale by scalar)
            fixed (byte* p = mem.Span)
            {
                var f = (float*)(p + offset);

                // Check if we have a scalar parameter
                if (args != null && args.Length > 1 && args[1] is float scalar)
                {
                    *f *= scalar;
                }
                else
                {
                    // Default operation: increment
                    *f += 1.0f;
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return ValueTask.CompletedTask;

        // Clean up any native resources
        // In a real implementation, this might free JIT-compiled code

        return ValueTask.CompletedTask;
    }

    private static bool TryGetBufferArguments(CoreKernelExecutionContext context, out IMemoryBuffer? input1, out IMemoryBuffer? input2, out IMemoryBuffer? output)
    {
        input1 = null;
        input2 = null;
        output = null;

        if (context.Arguments == null || context.Arguments.Length < 3)
            return false;

        // Try to extract buffer arguments (assuming simple vector operation kernel)
        if (context.Arguments[0] is IMemoryBuffer buf1 &&
            context.Arguments[1] is IMemoryBuffer buf2 &&
            context.Arguments[2] is IMemoryBuffer buf3)
        {
            input1 = buf1;
            input2 = buf2;
            output = buf3;
            return true;
        }

        return false;
    }

    private static unsafe bool TryGetBufferSpan<T>(IMemoryBuffer buffer, out Span<T> span) where T : unmanaged
    {
        span = default;

        // For CPU backend, we need to access the underlying memory
        if (buffer is CpuMemoryBuffer cpuBuffer)
        {
            var memory = cpuBuffer.GetMemory();
            if (memory.Length >= sizeof(T))
            {
                span = MemoryMarshal.Cast<byte, T>(memory.Span);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets performance metrics for this kernel.
    /// </summary>
    public KernelPerformanceMetrics GetPerformanceMetrics()
    {
        var execCount = Interlocked.Read(ref _executionCount);
        var totalTime = _totalExecutionTimeMs;

        return new KernelPerformanceMetrics
        {
            KernelName = _definition.Name,
            ExecutionCount = execCount,
            TotalExecutionTimeMs = totalTime,
            AverageExecutionTimeMs = execCount > 0 ? totalTime / execCount : 0,
            VectorizationEnabled = _executionPlan.UseVectorization,
            VectorWidth = _executionPlan.VectorWidth,
            InstructionSets = _executionPlan.InstructionSets
        };
    }

    private static KernelArguments ConvertContextToArguments(CoreKernelExecutionContext context)
    {
        // Convert KernelExecutionContext arguments to KernelArguments
        // The KernelArguments expects an array of objects
        if (context.Arguments != null && context.Arguments.Length > 0)
        {
            // Arguments is already an object[]
            return new KernelArguments(context.Arguments);
        }

        // Return empty arguments if none provided
        return new KernelArguments();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(CpuCompiledKernel));
    }

    private static bool IsSupportedArgumentType(Type type)
    {
        // Check if the type is a supported kernel argument type
        return type == typeof(IMemoryBuffer) ||
               type == typeof(CpuMemoryBuffer) ||
               type.IsArray ||
               type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type.IsEnum ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Memory<>)) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>));
    }
}

/// <summary>
/// Execution context for vectorized kernel execution.
/// </summary>
internal sealed class VectorizedExecutionContext
{
    public required CoreKernelExecutionContext KernelContext { get; init; }
    public required KernelExecutionPlan ExecutionPlan { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
