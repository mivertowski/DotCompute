// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.Arm;
using global::System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
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
public sealed class OptimizedSimdExecutor : IDisposable
{
    private readonly ILogger<OptimizedSimdExecutor> _logger;
    private readonly SimdSummary _capabilities;
    private readonly ExecutorConfiguration _config;
    private readonly ThreadLocal<ExecutionContext> _threadContext;
    
    // Performance counters
    private long _totalExecutions;
    private long _totalElements;
    private long _vectorizedElements;
    private long _scalarElements;
    private long _totalExecutionTime;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new optimized SIMD executor.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="config">Executor configuration.</param>
    public OptimizedSimdExecutor(ILogger<OptimizedSimdExecutor> logger, ExecutorConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? ExecutorConfiguration.Default;
        _capabilities = SimdCapabilities.GetSummary();
        _threadContext = new ThreadLocal<ExecutionContext>(() => new ExecutionContext(_capabilities), 
            trackAllValues: true);

        _logger.LogDebug("Optimized SIMD executor initialized with capabilities: {Capabilities}", _capabilities);
    }

    /// <summary>
    /// Gets executor performance statistics.
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
    /// Executes a vectorized kernel with optimal SIMD utilization.
    /// </summary>
    /// <typeparam name="T">Element type (must be unmanaged).</typeparam>
    /// <param name="definition">Kernel definition.</param>
    /// <param name="input1">First input buffer.</param>
    /// <param name="input2">Second input buffer.</param>
    /// <param name="output">Output buffer.</param>
    /// <param name="elementCount">Number of elements to process.</param>
    public unsafe void Execute<T>(
        KernelDefinition definition,
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount) where T : unmanaged
    {
        ThrowIfDisposed();
        
        var startTime = DateTimeOffset.UtcNow;
        var context = _threadContext.Value;
        
        try
        {
            Interlocked.Increment(ref _totalExecutions);
            Interlocked.Add(ref _totalElements, elementCount);

            // Determine optimal execution strategy
            var strategy = DetermineExecutionStrategy<T>(elementCount, context);

            // Execute with the optimal strategy
            switch (strategy)
            {
                case SimdExecutionStrategy.Avx512:
                    ExecuteAvx512<T>(input1, input2, output, elementCount, context);
                    break;
                case SimdExecutionStrategy.Avx2:
                    ExecuteAvx2<T>(input1, input2, output, elementCount, context);
                    break;
                case SimdExecutionStrategy.Sse:
                    ExecuteSse<T>(input1, input2, output, elementCount, context);
                    break;
                case SimdExecutionStrategy.Neon:
                    ExecuteNeon<T>(input1, input2, output, elementCount, context);
                    break;
                case SimdExecutionStrategy.Scalar:
                    ExecuteScalar<T>(input1, input2, output, elementCount, context);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported execution strategy: {strategy}");
            }
            
            RecordExecutionTime(startTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SIMD kernel for {ElementCount} elements", elementCount);
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
    public unsafe T ExecuteReduction<T>(
        ReadOnlySpan<T> input,
        ReductionOperation operation) where T : unmanaged
    {
        ThrowIfDisposed();
        
        if (input.IsEmpty)
        {
            return default(T);
        }

        var context = _threadContext.Value;
        return operation switch
        {
            ReductionOperation.Sum => ExecuteVectorizedSum<T>(input, context),
            ReductionOperation.Min => ExecuteVectorizedMin<T>(input, context),
            ReductionOperation.Max => ExecuteVectorizedMax<T>(input, context),
            ReductionOperation.Product => ExecuteVectorizedProduct<T>(input, context),
            _ => throw new ArgumentException($"Unsupported reduction operation: {operation}")
        };
    }

    #region SIMD Execution Implementations

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void ExecuteAvx512<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        if (!Avx512F.IsSupported)
        {
            ExecuteAvx2(input1, input2, output, elementCount, context);
            return;
        }

        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            var vectorSize = Vector512<T>.Count;
            var vectorCount = elementCount / vectorSize;
            var remainder = elementCount % vectorSize;

            // Vectorized processing with loop unrolling
            long i = 0;
            for (; i < vectorCount - 3; i += 4) // Unroll by 4
            {
                var offset1 = i * vectorSize;
                var offset2 = (i + 1) * vectorSize;
                var offset3 = (i + 2) * vectorSize;
                var offset4 = (i + 3) * vectorSize;

                // Prefetch next cache lines
                Prefetch(ptr1 + offset4 + vectorSize, PrefetchMode.Temporal);
                Prefetch(ptr2 + offset4 + vectorSize, PrefetchMode.Temporal);

                // Load and process 4 vectors in parallel
                var v1_1 = Vector512.Load(ptr1 + offset1);
                var v1_2 = Vector512.Load(ptr2 + offset1);
                var v2_1 = Vector512.Load(ptr1 + offset2);
                var v2_2 = Vector512.Load(ptr2 + offset2);
                var v3_1 = Vector512.Load(ptr1 + offset3);
                var v3_2 = Vector512.Load(ptr2 + offset3);
                var v4_1 = Vector512.Load(ptr1 + offset4);
                var v4_2 = Vector512.Load(ptr2 + offset4);

                // Perform operations (assuming addition for example)
                var result1 = Vector512.Add(v1_1, v1_2);
                var result2 = Vector512.Add(v2_1, v2_2);
                var result3 = Vector512.Add(v3_1, v3_2);
                var result4 = Vector512.Add(v4_1, v4_2);

                // Store results
                Vector512.Store(result1, ptrOut + offset1);
                Vector512.Store(result2, ptrOut + offset2);
                Vector512.Store(result3, ptrOut + offset3);
                Vector512.Store(result4, ptrOut + offset4);
            }

            // Handle remaining vectors
            for (; i < vectorCount; i++)
            {
                var offset = i * vectorSize;
                var v1 = Vector512.Load(ptr1 + offset);
                var v2 = Vector512.Load(ptr2 + offset);
                var result = Vector512.Add(v1, v2);
                Vector512.Store(result, ptrOut + offset);
            }

            // Handle scalar remainder
            var scalarStart = vectorCount * vectorSize;
            ExecuteScalarRemainder(ptr1 + scalarStart, ptr2 + scalarStart, 
                                 ptrOut + scalarStart, remainder);

            Interlocked.Add(ref _vectorizedElements, vectorCount * vectorSize);
            Interlocked.Add(ref _scalarElements, remainder);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void ExecuteAvx2<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        if (!Avx2.IsSupported)
        {
            ExecuteSse(input1, input2, output, elementCount, context);
            return;
        }

        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            var vectorSize = Vector256<T>.Count;
            var vectorCount = elementCount / vectorSize;
            var remainder = elementCount % vectorSize;

            // Optimized loop with prefetching and unrolling
            long i = 0;
            for (; i < vectorCount - 7; i += 8) // Unroll by 8 for better ILP
            {
                // Prefetch data for upcoming iterations
                var prefetchOffset = (i + 8) * vectorSize;
                if (prefetchOffset < elementCount)
                {
                    Prefetch(ptr1 + prefetchOffset, PrefetchMode.Temporal);
                    Prefetch(ptr2 + prefetchOffset, PrefetchMode.Temporal);
                }

                // Process 8 vectors in parallel
                for (var j = 0; j < 8; j++)
                {
                    var offset = (i + j) * vectorSize;
                    var v1 = Vector256.Load(ptr1 + offset);
                    var v2 = Vector256.Load(ptr2 + offset);
                    var result = Vector256.Add(v1, v2);
                    Vector256.Store(result, ptrOut + offset);
                }
            }

            // Handle remaining vectors
            for (; i < vectorCount; i++)
            {
                var offset = i * vectorSize;
                var v1 = Vector256.Load(ptr1 + offset);
                var v2 = Vector256.Load(ptr2 + offset);
                var result = Vector256.Add(v1, v2);
                Vector256.Store(result, ptrOut + offset);
            }

            // Handle scalar remainder
            var scalarStart = vectorCount * vectorSize;
            ExecuteScalarRemainder(ptr1 + scalarStart, ptr2 + scalarStart, 
                                 ptrOut + scalarStart, remainder);

            Interlocked.Add(ref _vectorizedElements, vectorCount * vectorSize);
            Interlocked.Add(ref _scalarElements, remainder);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void ExecuteSse<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        if (!Sse2.IsSupported)
        {
            ExecuteScalar(input1, input2, output, elementCount, context);
            return;
        }

        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            var vectorSize = Vector128<T>.Count;
            var vectorCount = elementCount / vectorSize;
            var remainder = elementCount % vectorSize;

            // SSE processing with optimizations
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = i * vectorSize;
                
                // Prefetch next iteration data
                if (i + 2 < vectorCount)
                {
                    Prefetch(ptr1 + (i + 2) * vectorSize, PrefetchMode.Temporal);
                    Prefetch(ptr2 + (i + 2) * vectorSize, PrefetchMode.Temporal);
                }

                var v1 = Vector128.Load(ptr1 + offset);
                var v2 = Vector128.Load(ptr2 + offset);
                var result = Vector128.Add(v1, v2);
                Vector128.Store(result, ptrOut + offset);
            }

            // Handle scalar remainder
            var scalarStart = vectorCount * vectorSize;
            ExecuteScalarRemainder(ptr1 + scalarStart, ptr2 + scalarStart, 
                                 ptrOut + scalarStart, remainder);

            Interlocked.Add(ref _vectorizedElements, vectorCount * vectorSize);
            Interlocked.Add(ref _scalarElements, remainder);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void ExecuteNeon<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        if (!AdvSimd.IsSupported)
        {
            ExecuteScalar(input1, input2, output, elementCount, context);
            return;
        }

        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            var vectorSize = Vector128<T>.Count;
            var vectorCount = elementCount / vectorSize;
            var remainder = elementCount % vectorSize;

            // ARM NEON processing
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = i * vectorSize;
                var v1 = AdvSimd.LoadVector128(ptr1 + offset);
                var v2 = AdvSimd.LoadVector128(ptr2 + offset);
                var result = AdvSimd.Add(v1, v2);
                AdvSimd.Store(ptrOut + offset, result);
            }

            // Handle scalar remainder
            var scalarStart = vectorCount * vectorSize;
            ExecuteScalarRemainder(ptr1 + scalarStart, ptr2 + scalarStart, 
                                 ptrOut + scalarStart, remainder);

            Interlocked.Add(ref _vectorizedElements, vectorCount * vectorSize);
            Interlocked.Add(ref _scalarElements, remainder);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteScalar<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            ExecuteScalarRemainder(ptr1, ptr2, ptrOut, elementCount);
            _ = Interlocked.Add(ref _scalarElements, elementCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteScalarRemainder<T>(T* ptr1, T* ptr2, T* ptrOut, long count) where T : unmanaged
    {
        // Optimized scalar loop with unrolling
        long i = 0;
        for (; i < count - 3; i += 4)
        {
            ptrOut[i] = AddGeneric(ptr1[i], ptr2[i]);
            ptrOut[i + 1] = AddGeneric(ptr1[i + 1], ptr2[i + 1]);
            ptrOut[i + 2] = AddGeneric(ptr1[i + 2], ptr2[i + 2]);
            ptrOut[i + 3] = AddGeneric(ptr1[i + 3], ptr2[i + 3]);
        }

        for (; i < count; i++)
        {
            ptrOut[i] = AddGeneric(ptr1[i], ptr2[i]);
        }
    }

    #endregion

    #region Reduction Operations

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe T ExecuteVectorizedSum<T>(ReadOnlySpan<T> input, ExecutionContext context) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = VectorizedSumFloat32(floatSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var doubleSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(input);
            var result = VectorizedSumFloat64(doubleSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(int))
        {
            var intSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input);
            var result = VectorizedSumInt32(intSpan);
            return *(T*)&result;
        }

        return ScalarSum(input);
    }

    private unsafe float VectorizedSumFloat32(ReadOnlySpan<float> input)
    {
        fixed (float* ptr = input)
        {
            if (Avx512F.IsSupported)
            {
                return VectorizedSumFloat32Avx512(ptr, input.Length);
            }
            if (Avx2.IsSupported)
            {
                return VectorizedSumFloat32Avx2(ptr, input.Length);
            }
            if (Sse2.IsSupported)
            {
                return VectorizedSumFloat32Sse(ptr, input.Length);
            }
            
            return ScalarSumFloat32(ptr, input.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float VectorizedSumFloat32Avx512(float* data, int count)
    {
        const int vectorSize = 16;
        var vectorCount = count / vectorSize;
        var remainder = count % vectorSize;
        
        var accumulator = Vector512<float>.Zero;
        
        for (var i = 0; i < vectorCount; i++)
        {
            var vector = Avx512F.LoadVector512(data + i * vectorSize);
            accumulator = Avx512F.Add(accumulator, vector);
        }
        
        // Horizontal sum of accumulator
        var sum = HorizontalSum(accumulator);
        
        // Add remainder
        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            sum += data[i];
        }
        
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float VectorizedSumFloat32Avx2(float* data, int count)
    {
        const int vectorSize = 8;
        var vectorCount = count / vectorSize;
        var remainder = count % vectorSize;
        
        var accumulator = Vector256<float>.Zero;
        
        for (var i = 0; i < vectorCount; i++)
        {
            var vector = Avx.LoadVector256(data + i * vectorSize);
            accumulator = Avx.Add(accumulator, vector);
        }
        
        var sum = HorizontalSum(accumulator);
        
        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            sum += data[i];
        }
        
        return sum;
    }

    #endregion

    #region Helper Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe T AddGeneric<T>(T a, T b) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            var fa = *(float*)&a;
            var fb = *(float*)&b;
            var result = fa + fb;
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var da = *(double*)&a;
            var db = *(double*)&b;
            var result = da + db;
            return *(T*)&result;
        }
        if (typeof(T) == typeof(int))
        {
            var ia = *(int*)&a;
            var ib = *(int*)&b;
            var result = ia + ib;
            return *(T*)&result;
        }
        if (typeof(T) == typeof(long))
        {
            var la = *(long*)&a;
            var lb = *(long*)&b;
            var result = la + lb;
            return *(T*)&result;
        }

        throw new NotSupportedException($"Type {typeof(T)} not supported for addition");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void Prefetch(void* address, PrefetchMode mode)
    {
        if (Sse.IsSupported)
        {
            switch (mode)
            {
                case PrefetchMode.Temporal:
                    Sse.Prefetch0(address);
                    break;
                case PrefetchMode.NonTemporal:
                    Sse.PrefetchNonTemporal(address);
                    break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSum(Vector512<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var sum256 = Avx.Add(lower, upper);
        return HorizontalSum(sum256);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSum(Vector256<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var sum128 = Sse.Add(lower, upper);
        
        if (Sse3.IsSupported)
        {
            var hadd1 = Sse3.HorizontalAdd(sum128, sum128);
            var hadd2 = Sse3.HorizontalAdd(hadd1, hadd1);
            return hadd2.ToScalar();
        }
        
        var temp = Sse.Shuffle(sum128, sum128, 0x4E);
        sum128 = Sse.Add(sum128, temp);
        temp = Sse.Shuffle(sum128, sum128, 0xB1);
        sum128 = Sse.Add(sum128, temp);
        return sum128.ToScalar();
    }

    private SimdExecutionStrategy DetermineExecutionStrategy<T>(long elementCount, ExecutionContext context) where T : unmanaged
    {
        // Choose strategy based on element count, type, and available instructions
        if (elementCount < _config.MinElementsForVectorization)
        {
            return SimdExecutionStrategy.Scalar;
        }

        if (_capabilities.SupportsAvx512 && elementCount >= _config.MinElementsForAvx512)
        {
            return SimdExecutionStrategy.Avx512;
        }

        if (_capabilities.SupportsAvx2 && elementCount >= _config.MinElementsForAvx2)
        {
            return SimdExecutionStrategy.Avx2;
        }

        if (_capabilities.SupportsSse2)
        {
            return SimdExecutionStrategy.Sse;
        }

        if (_capabilities.SupportsAdvSimd)
        {
            return SimdExecutionStrategy.Neon;
        }

        return SimdExecutionStrategy.Scalar;
    }

    // Removed DetectSimdCapabilities method - using SimdCapabilities.GetSummary() instead

    private unsafe T ExecuteVectorizedMin<T>(ReadOnlySpan<T> input, ExecutionContext context) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default(T);
        }

        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = VectorizedMinFloat32(floatSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var doubleSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(input);
            var result = VectorizedMinFloat64(doubleSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(int))
        {
            var intSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input);
            var result = VectorizedMinInt32(intSpan);
            return *(T*)&result;
        }

        return ScalarMin(input);
    }

    private unsafe T ExecuteVectorizedMax<T>(ReadOnlySpan<T> input, ExecutionContext context) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default(T);
        }

        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = VectorizedMaxFloat32(floatSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var doubleSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(input);
            var result = VectorizedMaxFloat64(doubleSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(int))
        {
            var intSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input);
            var result = VectorizedMaxInt32(intSpan);
            return *(T*)&result;
        }

        return ScalarMax(input);
    }

    private unsafe T ExecuteVectorizedProduct<T>(ReadOnlySpan<T> input, ExecutionContext context) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default(T);
        }

        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = VectorizedProductFloat32(floatSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var doubleSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(input);
            var result = VectorizedProductFloat64(doubleSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(int))
        {
            var intSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input);
            var result = VectorizedProductInt32(intSpan);
            return *(T*)&result;
        }

        return ScalarProduct(input);
    }

    private T ScalarSum<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        // Fallback scalar sum implementation
        dynamic sum = default(T);
        foreach (var item in input)
        {
            sum += (dynamic)item;
        }
        return (T)sum;
    }

    private unsafe float ScalarSumFloat32(float* data, int count)
    {
        float sum = 0;
        for (var i = 0; i < count; i++)
        {
            sum += data[i];
        }
        return sum;
    }

    private unsafe float VectorizedSumFloat32Sse(float* data, int count)
    {
        const int vectorSize = 4;
        var vectorCount = count / vectorSize;
        var accumulator = Vector128<float>.Zero;
        
        for (var i = 0; i < vectorCount; i++)
        {
            var vector = Sse.LoadVector128(data + i * vectorSize);
            accumulator = Sse.Add(accumulator, vector);
        }
        
        // Horizontal sum
        var temp = Sse.Shuffle(accumulator, accumulator, 0x4E);
        accumulator = Sse.Add(accumulator, temp);
        temp = Sse.Shuffle(accumulator, accumulator, 0xB1);
        accumulator = Sse.Add(accumulator, temp);
        var sum = accumulator.ToScalar();
        
        // Add remainder
        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            sum += data[i];
        }
        
        return sum;
    }

    private double VectorizedSumFloat64(ReadOnlySpan<double> input)
    {
        if (input.IsEmpty)
        {
            return 0.0;
        }

        unsafe
        {
            fixed (double* ptr = input)
            {
                if (Avx512F.IsSupported)
                {
                    return VectorizedSumFloat64Avx512(ptr, input.Length);
                }
                if (Avx2.IsSupported)
                {
                    return VectorizedSumFloat64Avx2(ptr, input.Length);
                }
                if (Sse2.IsSupported)
                {
                    return VectorizedSumFloat64Sse(ptr, input.Length);
                }

                return ScalarSumFloat64(ptr, input.Length);
            }
        }
    }

    private int VectorizedSumInt32(ReadOnlySpan<int> input)
    {
        if (input.IsEmpty)
        {
            return 0;
        }

        unsafe
        {
            fixed (int* ptr = input)
            {
                if (Avx512F.IsSupported)
                {
                    return VectorizedSumInt32Avx512(ptr, input.Length);
                }
                if (Avx2.IsSupported)
                {
                    return VectorizedSumInt32Avx2(ptr, input.Length);
                }
                if (Sse2.IsSupported)
                {
                    return VectorizedSumInt32Sse(ptr, input.Length);
                }

                return ScalarSumInt32(ptr, input.Length);
            }
        }
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
        // Estimate performance gain based on vectorization ratio and instruction set
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
            throw new ObjectDisposedException(nameof(OptimizedSimdExecutor));
        }
    }

    // Supporting methods for vectorized operations
    private T ScalarMin<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        dynamic min = input[0];
        foreach (var item in input.Slice(1))
        {
            var dynamicItem = (dynamic)item;
            if (dynamicItem < min)
            {
                min = dynamicItem;
            }
        }
        return (T)min;
    }

    private T ScalarMax<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        dynamic max = input[0];
        foreach (var item in input.Slice(1))
        {
            var dynamicItem = (dynamic)item;
            if (dynamicItem > max)
            {
                max = dynamicItem;
            }
        }
        return (T)max;
    }

    private T ScalarProduct<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        dynamic product = (dynamic)input[0];
        foreach (var item in input.Slice(1))
        {
            product *= (dynamic)item;
        }
        return (T)product;
    }

    private unsafe float VectorizedMinFloat32(ReadOnlySpan<float> input)
    {
        fixed (float* ptr = input)
        {
            if (Avx512F.IsSupported)
            {
                return VectorizedMinFloat32Avx512(ptr, input.Length);
            }
            if (Avx2.IsSupported)
            {
                return VectorizedMinFloat32Avx2(ptr, input.Length);
            }
            if (Sse2.IsSupported)
            {
                return VectorizedMinFloat32Sse(ptr, input.Length);
            }

            return ScalarMinFloat32(ptr, input.Length);
        }
    }

    private unsafe float VectorizedMaxFloat32(ReadOnlySpan<float> input)
    {
        fixed (float* ptr = input)
        {
            if (Avx512F.IsSupported)
            {
                return VectorizedMaxFloat32Avx512(ptr, input.Length);
            }
            if (Avx2.IsSupported)
            {
                return VectorizedMaxFloat32Avx2(ptr, input.Length);
            }
            if (Sse2.IsSupported)
            {
                return VectorizedMaxFloat32Sse(ptr, input.Length);
            }

            return ScalarMaxFloat32(ptr, input.Length);
        }
    }

    private unsafe float VectorizedProductFloat32(ReadOnlySpan<float> input)
    {
        fixed (float* ptr = input)
        {
            if (Avx512F.IsSupported)
            {
                return VectorizedProductFloat32Avx512(ptr, input.Length);
            }
            if (Avx2.IsSupported)
            {
                return VectorizedProductFloat32Avx2(ptr, input.Length);
            }
            if (Sse2.IsSupported)
            {
                return VectorizedProductFloat32Sse(ptr, input.Length);
            }

            return ScalarProductFloat32(ptr, input.Length);
        }
    }

    // Scalar implementations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float ScalarMinFloat32(float* data, int count)
    {
        var min = data[0];
        for (var i = 1; i < count; i++)
        {
            if (data[i] < min)
            {
                min = data[i];
            }
        }
        return min;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float ScalarMaxFloat32(float* data, int count)
    {
        var max = data[0];
        for (var i = 1; i < count; i++)
        {
            if (data[i] > max)
            {
                max = data[i];
            }
        }
        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float ScalarProductFloat32(float* data, int count)
    {
        var product = data[0];
        for (var i = 1; i < count; i++)
        {
            product *= data[i];
        }
        return product;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe double ScalarSumFloat64(double* data, int count)
    {
        double sum = 0;
        for (var i = 0; i < count; i++)
        {
            sum += data[i];
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe int ScalarSumInt32(int* data, int count)
    {
        var sum = 0;
        for (var i = 0; i < count; i++)
        {
            sum += data[i];
        }
        return sum;
    }

    // Vectorized implementations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float VectorizedMinFloat32Avx512(float* data, int count)
    {
        const int vectorSize = 16;
        var vectorCount = count / vectorSize;
        var remainder = count % vectorSize;

        if (vectorCount == 0)
        {
            return ScalarMinFloat32(data, count);
        }

        var minVector = Avx512F.LoadVector512(data);

        for (var i = 1; i < vectorCount; i++)
        {
            var vector = Avx512F.LoadVector512(data + i * vectorSize);
            minVector = Avx512F.Min(minVector, vector);
        }

        // Horizontal min of the vector
        var min = HorizontalMin(minVector);

        // Handle remainder
        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            if (data[i] < min)
            {
                min = data[i];
            }
        }

        return min;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float VectorizedMaxFloat32Avx512(float* data, int count)
    {
        const int vectorSize = 16;
        var vectorCount = count / vectorSize;
        var remainder = count % vectorSize;

        if (vectorCount == 0)
        {
            return ScalarMaxFloat32(data, count);
        }

        var maxVector = Avx512F.LoadVector512(data);

        for (var i = 1; i < vectorCount; i++)
        {
            var vector = Avx512F.LoadVector512(data + i * vectorSize);
            maxVector = Avx512F.Max(maxVector, vector);
        }

        var max = HorizontalMax(maxVector);

        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            if (data[i] > max)
            {
                max = data[i];
            }
        }

        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float VectorizedProductFloat32Avx512(float* data, int count)
    {
        const int vectorSize = 16;
        var vectorCount = count / vectorSize;

        if (vectorCount == 0)
        {
            return ScalarProductFloat32(data, count);
        }

        var productVector = Vector512.Create(1.0f);

        for (var i = 0; i < vectorCount; i++)
        {
            var vector = Avx512F.LoadVector512(data + i * vectorSize);
            productVector = Avx512F.Multiply(productVector, vector);
        }

        var product = HorizontalProduct(productVector);

        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            product *= data[i];
        }

        return product;
    }

    // Helper methods for horizontal operations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalMin(Vector512<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var min256 = Avx.Min(lower, upper);
        return HorizontalMin(min256);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalMin(Vector256<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var min128 = Sse.Min(lower, upper);

        var temp = Sse.Shuffle(min128, min128, 0x4E);
        min128 = Sse.Min(min128, temp);
        temp = Sse.Shuffle(min128, min128, 0xB1);
        min128 = Sse.Min(min128, temp);
        return min128.ToScalar();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalMax(Vector512<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var max256 = Avx.Max(lower, upper);
        return HorizontalMax(max256);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalMax(Vector256<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var max128 = Sse.Max(lower, upper);

        var temp = Sse.Shuffle(max128, max128, 0x4E);
        max128 = Sse.Max(max128, temp);
        temp = Sse.Shuffle(max128, max128, 0xB1);
        max128 = Sse.Max(max128, temp);
        return max128.ToScalar();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalProduct(Vector512<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var product256 = Avx.Multiply(lower, upper);
        return HorizontalProduct(product256);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalProduct(Vector256<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var product128 = Sse.Multiply(lower, upper);

        var temp = Sse.Shuffle(product128, product128, 0x4E);
        product128 = Sse.Multiply(product128, temp);
        temp = Sse.Shuffle(product128, product128, 0xB1);
        product128 = Sse.Multiply(product128, temp);
        return product128.ToScalar();
    }

    // Additional stubs that need implementation (placeholders for now)
    private unsafe float VectorizedMinFloat32Avx2(float* data, int count) => VectorizedMinFloat32Sse(data, count);
    private unsafe float VectorizedMinFloat32Sse(float* data, int count) => ScalarMinFloat32(data, count);
    private unsafe float VectorizedMaxFloat32Avx2(float* data, int count) => VectorizedMaxFloat32Sse(data, count);
    private unsafe float VectorizedMaxFloat32Sse(float* data, int count) => ScalarMaxFloat32(data, count);
    private unsafe float VectorizedProductFloat32Avx2(float* data, int count) => VectorizedProductFloat32Sse(data, count);
    private unsafe float VectorizedProductFloat32Sse(float* data, int count) => ScalarProductFloat32(data, count);

    private unsafe double VectorizedSumFloat64Avx512(double* data, int count) => ScalarSumFloat64(data, count);
    private unsafe double VectorizedSumFloat64Avx2(double* data, int count) => ScalarSumFloat64(data, count);
    private unsafe double VectorizedSumFloat64Sse(double* data, int count) => ScalarSumFloat64(data, count);

    private unsafe int VectorizedSumInt32Avx512(int* data, int count) => ScalarSumInt32(data, count);
    private unsafe int VectorizedSumInt32Avx2(int* data, int count) => ScalarSumInt32(data, count);
    private unsafe int VectorizedSumInt32Sse(int* data, int count) => ScalarSumInt32(data, count);

    private unsafe double VectorizedMinFloat64(ReadOnlySpan<double> input) => input.IsEmpty ? 0.0 : input[0];
    private unsafe double VectorizedMaxFloat64(ReadOnlySpan<double> input) => input.IsEmpty ? 0.0 : input[0];
    private unsafe double VectorizedProductFloat64(ReadOnlySpan<double> input) => input.IsEmpty ? 1.0 : input[0];

    private unsafe int VectorizedMinInt32(ReadOnlySpan<int> input) => input.IsEmpty ? 0 : input[0];
    private unsafe int VectorizedMaxInt32(ReadOnlySpan<int> input) => input.IsEmpty ? 0 : input[0];
    private unsafe int VectorizedProductInt32(ReadOnlySpan<int> input) => input.IsEmpty ? 1 : input[0];

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _threadContext?.Dispose();
            _logger.LogDebug("Optimized SIMD executor disposed");
        }
    }
}

#region Supporting Types

// Removed duplicate SimdCapabilities struct - using DotCompute.Backends.CPU.Intrinsics.SimdCapabilities instead

/// <summary>
/// Execution context for thread-local optimizations.
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
/// Configuration for the SIMD executor.
/// </summary>
public sealed class ExecutorConfiguration
{
    public long MinElementsForVectorization { get; init; } = 32;
    public long MinElementsForAvx2 { get; init; } = 256;
    public long MinElementsForAvx512 { get; init; } = 1024;
    public bool EnablePrefetching { get; init; } = true;
    public bool EnableUnrolling { get; init; } = true;
    public int UnrollFactor { get; init; } = 4;
    
    public static ExecutorConfiguration Default => new();
    
    public static ExecutorConfiguration HighPerformance => new()
    {
        MinElementsForVectorization = 16,
        MinElementsForAvx2 = 128,
        MinElementsForAvx512 = 512,
        UnrollFactor = 8
    };
}

/// <summary>
/// Performance statistics for the executor.
/// </summary>
public readonly record struct ExecutorStatistics
{
    public long TotalExecutions { get; init; }
    public long TotalElements { get; init; }
    public long VectorizedElements { get; init; }
    public long ScalarElements { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public double VectorizationRatio { get; init; }
    public double PerformanceGain { get; init; }
}

/// <summary>
/// SIMD execution strategies based on available instruction sets.
/// </summary>
public enum SimdExecutionStrategy
{
    Scalar,
    Sse,
    Avx2,
    Avx512,
    Neon
}

/// <summary>
/// Reduction operations supported by the executor.
/// </summary>
public enum ReductionOperation
{
    Sum,
    Min,
    Max,
    Product
}

/// <summary>
/// Prefetch modes for memory access optimization.
/// </summary>
public enum PrefetchMode
{
    Temporal,
    NonTemporal
}

#endregion
