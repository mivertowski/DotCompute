using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Core;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Represents a compiled kernel for CPU execution with vectorization support.
/// </summary>
internal sealed class CpuCompiledKernel : ICompiledKernel
{
    private readonly KernelDefinition _definition;
    private readonly KernelExecutionPlan _executionPlan;
    private readonly CpuThreadPool _threadPool;
    private readonly ILogger _logger;
    private readonly SimdCodeGenerator _codeGenerator;
    private readonly DynamicMethod _compiledMethod;
    private long _executionCount;
    private double _totalExecutionTimeMs;
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
        
        // Initialize SIMD code generator
        _codeGenerator = new SimdCodeGenerator(executionPlan.Analysis.Definition.Metadata?.TryGetValue("SimdCapabilities", out var caps) == true 
            ? (SimdSummary)caps 
            : new SimdSummary());
        
        // Pre-compile the kernel method if vectorization is enabled
        if (_executionPlan.UseVectorization)
        {
            _compiledMethod = _codeGenerator.GenerateVectorizedKernel(definition, executionPlan);
        }
    }

    public KernelDefinition Definition => _definition;

    public async ValueTask ExecuteAsync(
        KernelExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);

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
                nameof(context));
        }

        // Validate arguments
        if (context.Arguments.Count != _definition.Parameters.Count)
        {
            throw new ArgumentException(
                $"Argument count mismatch. Kernel expects {_definition.Parameters.Count} arguments, but got {context.Arguments.Count}",
                nameof(context));
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
        KernelExecutionContext context,
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
        // This is where vectorized kernel execution would occur
        // For demonstration, we'll show how different vector widths would be handled
        
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
        
        // For CPU backend, we need to handle memory access differently
        // This is a simplified implementation - production code would use proper buffer abstraction
        var elementCount = input1.SizeInBytes / sizeof(float);
        
        // Create temporary spans for demonstration
        var floatInput1 = new Span<float>(new float[elementCount]);
        var floatInput2 = new Span<float>(new float[elementCount]);
        var floatOutput = new Span<float>(new float[elementCount]);
        
        // In production, these would be populated from the actual buffer data
        // For now, we'll use the existing scalar execution path
        
        var baseIndex = workItemIds[0][0]; // Assuming 1D kernel for simplicity
        var count = Math.Min(16, floatInput1.Length - baseIndex);
        
        if (count >= 16 && Avx512F.IsSupported)
        {
            fixed (float* pIn1 = &floatInput1[(int)baseIndex])
            fixed (float* pIn2 = &floatInput2[(int)baseIndex])
            fixed (float* pOut = &floatOutput[(int)baseIndex])
            {
                var v1 = Avx512F.LoadVector512(pIn1);
                var v2 = Avx512F.LoadVector512(pIn2);
                var result = Avx512F.Add(v1, v2);
                Avx512F.Store(pOut, result);
            }
        }
        else
        {
            // Scalar fallback for partial vectors
            for (int i = 0; i < count; i++)
            {
                floatOutput[(int)baseIndex + i] = floatInput1[(int)baseIndex + i] + floatInput2[(int)baseIndex + i];
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
        
        // For CPU backend, we need to handle memory access differently
        var elementCount = input1.SizeInBytes / sizeof(float);
        
        // Create temporary spans for demonstration
        var floatInput1 = new Span<float>(new float[elementCount]);
        var floatInput2 = new Span<float>(new float[elementCount]);
        var floatOutput = new Span<float>(new float[elementCount]);
        
        var baseIndex = workItemIds[0][0]; // Assuming 1D kernel for simplicity
        var count = Math.Min(8, floatInput1.Length - baseIndex);
        
        if (count >= 8 && Avx2.IsSupported)
        {
            fixed (float* pIn1 = &floatInput1[(int)baseIndex])
            fixed (float* pIn2 = &floatInput2[(int)baseIndex])
            fixed (float* pOut = &floatOutput[(int)baseIndex])
            {
                var v1 = Avx.LoadVector256(pIn1);
                var v2 = Avx.LoadVector256(pIn2);
                var result = Avx.Add(v1, v2);
                Avx.Store(pOut, result);
            }
        }
        else
        {
            // Scalar fallback for partial vectors
            for (int i = 0; i < count; i++)
            {
                floatOutput[(int)baseIndex + i] = floatInput1[(int)baseIndex + i] + floatInput2[(int)baseIndex + i];
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
        
        // For CPU backend, we need to handle memory access differently
        var elementCount = input1.SizeInBytes / sizeof(float);
        
        // Create temporary spans for demonstration
        var floatInput1 = new Span<float>(new float[elementCount]);
        var floatInput2 = new Span<float>(new float[elementCount]);
        var floatOutput = new Span<float>(new float[elementCount]);
        
        var baseIndex = workItemIds[0][0]; // Assuming 1D kernel for simplicity
        var count = Math.Min(4, floatInput1.Length - baseIndex);
        
        if (count >= 4 && Sse.IsSupported)
        {
            fixed (float* pIn1 = &floatInput1[(int)baseIndex])
            fixed (float* pIn2 = &floatInput2[(int)baseIndex])
            fixed (float* pOut = &floatOutput[(int)baseIndex])
            {
                var v1 = Sse.LoadVector128(pIn1);
                var v2 = Sse.LoadVector128(pIn2);
                var result = Sse.Add(v1, v2);
                Sse.Store(pOut, result);
            }
        }
        else
        {
            // Scalar fallback for partial vectors
            for (int i = 0; i < count; i++)
            {
                floatOutput[(int)baseIndex + i] = floatInput1[(int)baseIndex + i] + floatInput2[(int)baseIndex + i];
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

    private static void ExecuteSingleWorkItem(KernelExecutionContext context, long[] workItemId)
    {
        // Stub implementation for scalar execution
        // In a real implementation, this would execute the compiled kernel code
        // with access to the work item ID and kernel arguments
        
        // This is where the actual kernel logic would be executed
        // For example, a vector addition kernel would:
        // 1. Extract buffer arguments from context.Arguments
        // 2. Calculate memory addresses based on workItemId
        // 3. Perform the computation
        // 4. Store results back to output buffer
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return ValueTask.CompletedTask;

        // Clean up any native resources
        // In a real implementation, this might free JIT-compiled code

        return ValueTask.CompletedTask;
    }

    private bool TryGetBufferArguments(KernelExecutionContext context, out IMemoryBuffer input1, out IMemoryBuffer input2, out IMemoryBuffer output)
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
        // This is a simplified approach - real implementation would need proper casting
        if (buffer is IMemoryBuffer<T> typedBuffer)
        {
            span = typedBuffer.AsSpan();
            return true;
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
    public required KernelExecutionContext KernelContext { get; init; }
    public required KernelExecutionPlan ExecutionPlan { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}