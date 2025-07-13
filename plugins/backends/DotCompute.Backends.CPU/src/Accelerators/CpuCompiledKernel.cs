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
using DotCompute.Backends.CPU.Threading;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using Microsoft.Extensions.Logging;
using CoreKernelDefinition = DotCompute.Core.KernelDefinition;
using CoreICompiledKernel = DotCompute.Core.ICompiledKernel;
using CoreKernelExecutionContext = DotCompute.Core.KernelExecutionContext;
using IMemoryBuffer = DotCompute.Abstractions.IMemoryBuffer;
using TextKernelSource = DotCompute.Core.TextKernelSource;
using BytecodeKernelSource = DotCompute.Core.BytecodeKernelSource;

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

    // Interface implementation for Core.ICompiledKernel
    CoreKernelDefinition CoreICompiledKernel.Definition => _definition;
    
    public ValueTask ExecuteAsync(CoreKernelExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Convert KernelExecutionContext to KernelArguments for internal processing
        var arguments = ConvertContextToArguments(context);
        return ExecuteAsync(arguments, cancellationToken);
    }

    public CoreKernelDefinition Definition => _definition;

    public string Name => _definition.Name;

    public string Id => $"{_definition.Name}_{_definition.GetHashCode():X8}";

    public string Source => _definition.Source switch
    {
        TextKernelSource textSource => textSource.Code,
        BytecodeKernelSource => "[Bytecode]",
        _ => "[Unknown]"
    };

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
            GlobalWorkSize = new[] { 1024L }, // Default work size - should be configurable
            Arguments = arguments.Arguments.ToArray()
        };

        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogDebug(
            "Executing kernel '{KernelName}' with global work size: [{WorkSize}], vectorization: {Vectorization}",
            _definition.Name,
            string.Join(", ", context.GlobalWorkSize),
            _executionPlan.UseVectorization ? $"{_executionPlan.VectorWidth}-bit" : "disabled");

        // Validate work dimensions
        if (context.GlobalWorkSize.Count != _definition.WorkDimensions)
        {
            throw new ArgumentException(
                $"Work dimensions mismatch. Kernel expects {_definition.WorkDimensions} dimensions, but got {context.GlobalWorkSize.Count}",
                nameof(arguments));
        }

        // Validate arguments
        if (context.Arguments.Count != _definition.Parameters.Count)
        {
            throw new ArgumentException(
                $"Argument count mismatch. Kernel expects {_definition.Parameters.Count} arguments, but got {context.Arguments.Count}",
                nameof(arguments));
        }

        // Calculate total work items
        long totalWorkItems = GetTotalWorkItems(context.GlobalWorkSize);

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
        
        // Update performance metrics
        Interlocked.Increment(ref _executionCount);
        Interlocked.Exchange(ref _totalExecutionTimeMs, _totalExecutionTimeMs + stopwatch.Elapsed.TotalMilliseconds);
        
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
            var workItemId = GetWorkItemId(i, context.KernelContext.GlobalWorkSize);
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
                if (actualIndex < GetTotalWorkItems(context.KernelContext.GlobalWorkSize))
                {
                    workItemIds[v] = GetWorkItemId(actualIndex, context.KernelContext.GlobalWorkSize);
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
                var delegateArgs = new object[context.Arguments.Count + 1];
                context.Arguments.CopyTo(delegateArgs, 0);
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
        if (args.Count < 2)
            return; // Need at least 2 arguments for most operations
        
        // For demonstration, we'll implement a simple vector addition kernel
        // In production, this would dispatch to the actual compiled kernel code
        
        // Calculate linear index from work item ID
        long linearIndex = workItemId[0];
        for (int i = 1; i < workItemId.Length; i++)
        {
            linearIndex = linearIndex * context.GlobalWorkSize[i] + workItemId[i];
        }
        
        // Handle different kernel patterns
        if (args.Count >= 3 && 
            args[0] is IMemoryBuffer input1 && 
            args[1] is IMemoryBuffer input2 && 
            args[2] is IMemoryBuffer output)
        {
            // Vector operation pattern (e.g., C = A + B)
            ExecuteVectorOperation(input1, input2, output, linearIndex);
        }
        else if (args.Count >= 2 && 
                 args[0] is IMemoryBuffer input && 
                 args[1] is IMemoryBuffer outputBuf)
        {
            // Unary operation pattern (e.g., B = sqrt(A))
            ExecuteUnaryOperation(input, outputBuf, linearIndex);
        }
        else if (args.Count >= 1 && args[0] is IMemoryBuffer buffer)
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
        // Get element size (assuming float for now)
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
        // Get element size (assuming float for now)
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
        IReadOnlyList<object> args)
    {
        // Get element size (assuming float for now)
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
                if (args.Count > 1 && args[1] is float scalar)
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
        
        if (context.Arguments.Count < 3)
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
        if (context.Arguments != null && context.Arguments.Count > 0)
        {
            // Convert IReadOnlyList<object> to object[]
            var args = new object[context.Arguments.Count];
            for (int i = 0; i < context.Arguments.Count; i++)
            {
                args[i] = context.Arguments[i];
            }
            return new KernelArguments(args);
        }
        
        // Return empty arguments if none provided
        return new KernelArguments();
    }
    
    private void ThrowIfDisposed()
    {
        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(CpuCompiledKernel));
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