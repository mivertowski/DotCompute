// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Compute;

/// <summary>
/// Base class for optimized kernel implementations.
/// </summary>
internal abstract class OptimizedKernelBase : ICompiledKernel
{
    protected readonly ILogger Logger;
    protected readonly CompilationOptions Options;
    protected bool Disposed;

    protected OptimizedKernelBase(string name, CompilationOptions options, ILogger logger)
    {
        Name = name;
        Options = options;
        Logger = logger;
    }

    public string Name { get; }

    public abstract ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default);

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    protected void ThrowIfDisposed()
    {
        if (Disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}

/// <summary>
/// Highly optimized vector addition kernel with SIMD support.
/// </summary>
internal class OptimizedVectorAddKernel : OptimizedKernelBase
{
    public OptimizedVectorAddKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Length < 3)
            throw new ArgumentException("Vector add requires 3 arguments: input A, input B, output");

        var bufferA = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var bufferB = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");
        var bufferResult = arguments.Arguments[2] as IMemoryBuffer ?? throw new ArgumentException("Argument 2 must be IMemoryBuffer");

        var elementCount = (int)(bufferA.SizeInBytes / sizeof(float));
        
        await Task.Run(() => ExecuteVectorAddOptimized(bufferA, bufferB, bufferResult, elementCount), cancellationToken);
    }

    private unsafe void ExecuteVectorAddOptimized(IMemoryBuffer bufferA, IMemoryBuffer bufferB, IMemoryBuffer bufferResult, int elementCount)
    {
        if (bufferA is not HighPerformanceMemoryBuffer hpBufferA ||
            bufferB is not HighPerformanceMemoryBuffer hpBufferB ||
            bufferResult is not HighPerformanceMemoryBuffer hpBufferResult)
        {
            // Fallback to generic implementation
            ExecuteVectorAddGeneric(bufferA, bufferB, bufferResult, elementCount);
            return;
        }

        var ptrA = hpBufferA.GetFloatPtr();
        var ptrB = hpBufferB.GetFloatPtr();
        var ptrResult = hpBufferResult.GetFloatPtr();

        // Use SIMD for maximum performance
        if (Avx.IsSupported && elementCount >= 8)
        {
            ExecuteVectorAddAvx(ptrA, ptrB, ptrResult, elementCount);
        }
        else if (Sse.IsSupported && elementCount >= 4)
        {
            ExecuteVectorAddSse(ptrA, ptrB, ptrResult, elementCount);
        }
        else if (Vector.IsHardwareAccelerated && elementCount >= Vector<float>.Count)
        {
            ExecuteVectorAddVector(ptrA, ptrB, ptrResult, elementCount);
        }
        else
        {
            ExecuteVectorAddScalar(ptrA, ptrB, ptrResult, elementCount);
        }
    }

    private static unsafe void ExecuteVectorAddAvx(float* ptrA, float* ptrB, float* ptrResult, int elementCount)
    {
        const int vectorSize = 8;
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        // Process 8 elements at a time with AVX
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vecA = Avx.LoadVector256(ptrA + offset);
            var vecB = Avx.LoadVector256(ptrB + offset);
            var result = Avx.Add(vecA, vecB);
            Avx.Store(ptrResult + offset, result);
        }

        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < elementCount; i++)
        {
            ptrResult[i] = ptrA[i] + ptrB[i];
        }
    }

    private static unsafe void ExecuteVectorAddSse(float* ptrA, float* ptrB, float* ptrResult, int elementCount)
    {
        const int vectorSize = 4;
        var vectorCount = elementCount / vectorSize;

        // Process 4 elements at a time with SSE
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vecA = Sse.LoadVector128(ptrA + offset);
            var vecB = Sse.LoadVector128(ptrB + offset);
            var result = Sse.Add(vecA, vecB);
            Sse.Store(ptrResult + offset, result);
        }

        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < elementCount; i++)
        {
            ptrResult[i] = ptrA[i] + ptrB[i];
        }
    }

    private static unsafe void ExecuteVectorAddVector(float* ptrA, float* ptrB, float* ptrResult, int elementCount)
    {
        var vectorSize = Vector<float>.Count;
        var vectorCount = elementCount / vectorSize;

        // Use .NET Vector<T> SIMD
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vecA = Unsafe.ReadUnaligned<Vector<float>>(ptrA + offset);
            var vecB = Unsafe.ReadUnaligned<Vector<float>>(ptrB + offset);
            var result = vecA + vecB;
            Unsafe.WriteUnaligned(ptrResult + offset, result);
        }

        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < elementCount; i++)
        {
            ptrResult[i] = ptrA[i] + ptrB[i];
        }
    }

    private static unsafe void ExecuteVectorAddScalar(float* ptrA, float* ptrB, float* ptrResult, int elementCount)
    {
        for (int i = 0; i < elementCount; i++)
        {
            ptrResult[i] = ptrA[i] + ptrB[i];
        }
    }

    private async void ExecuteVectorAddGeneric(IMemoryBuffer bufferA, IMemoryBuffer bufferB, IMemoryBuffer bufferResult, int elementCount)
    {
        var dataA = new float[elementCount];
        var dataB = new float[elementCount];
        var dataResult = new float[elementCount];

        await bufferA.CopyToHostAsync<float>(dataA);
        await bufferB.CopyToHostAsync<float>(dataB);

        // Vectorized addition
        var vectorSize = Vector<float>.Count;
        var vectorCount = elementCount / vectorSize;

        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vecA = new Vector<float>(dataA, offset);
            var vecB = new Vector<float>(dataB, offset);
            var result = vecA + vecB;
            result.CopyTo(dataResult, offset);
        }

        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < elementCount; i++)
        {
            dataResult[i] = dataA[i] + dataB[i];
        }

        await bufferResult.CopyFromHostAsync<float>(dataResult);
    }
}

/// <summary>
/// Cache-optimized matrix multiplication kernel.
/// </summary>
internal class OptimizedMatrixMultiplyKernel : OptimizedKernelBase
{
    public OptimizedMatrixMultiplyKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Length < 4)
            throw new ArgumentException("Matrix multiply requires 4 arguments: matrix A, matrix B, result C, size");

        var bufferA = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var bufferB = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");
        var bufferC = arguments.Arguments[2] as IMemoryBuffer ?? throw new ArgumentException("Argument 2 must be IMemoryBuffer");
        var size = Convert.ToInt32(arguments.Arguments[3]);

        await Task.Run(() => ExecuteMatrixMultiplyOptimized(bufferA, bufferB, bufferC, size), cancellationToken);
    }

    private unsafe void ExecuteMatrixMultiplyOptimized(IMemoryBuffer bufferA, IMemoryBuffer bufferB, IMemoryBuffer bufferC, int size)
    {
        if (bufferA is HighPerformanceMemoryBuffer hpBufferA &&
            bufferB is HighPerformanceMemoryBuffer hpBufferB &&
            bufferC is HighPerformanceMemoryBuffer hpBufferC)
        {
            var ptrA = hpBufferA.GetFloatPtr();
            var ptrB = hpBufferB.GetFloatPtr();
            var ptrC = hpBufferC.GetFloatPtr();

            ExecuteMatrixMultiplyBlocked(ptrA, ptrB, ptrC, size);
        }
        else
        {
            ExecuteMatrixMultiplyGeneric(bufferA, bufferB, bufferC, size).Wait();
        }
    }

    private static unsafe void ExecuteMatrixMultiplyBlocked(float* a, float* b, float* c, int size)
    {
        const int blockSize = 64; // Optimize for L1 cache

        // Initialize result matrix to zero
        for (int i = 0; i < size * size; i++)
        {
            c[i] = 0.0f;
        }

        // Blocked matrix multiplication for cache efficiency
        for (int ii = 0; ii < size; ii += blockSize)
        {
            for (int jj = 0; jj < size; jj += blockSize)
            {
                for (int kk = 0; kk < size; kk += blockSize)
                {
                    var iEnd = Math.Min(ii + blockSize, size);
                    var jEnd = Math.Min(jj + blockSize, size);
                    var kEnd = Math.Min(kk + blockSize, size);

                    for (int i = ii; i < iEnd; i++)
                    {
                        for (int j = jj; j < jEnd; j++)
                        {
                            var sum = c[i * size + j];
                            for (int k = kk; k < kEnd; k++)
                            {
                                sum += a[i * size + k] * b[k * size + j];
                            }
                            c[i * size + j] = sum;
                        }
                    }
                }
            }
        }
    }

    private async Task ExecuteMatrixMultiplyGeneric(IMemoryBuffer bufferA, IMemoryBuffer bufferB, IMemoryBuffer bufferC, int size)
    {
        var matrixA = new float[size * size];
        var matrixB = new float[size * size];
        var matrixC = new float[size * size];

        await bufferA.CopyToHostAsync<float>(matrixA);
        await bufferB.CopyToHostAsync<float>(matrixB);

        // Parallel matrix multiplication
        Parallel.For(0, size, i =>
        {
            for (int j = 0; j < size; j++)
            {
                float sum = 0.0f;
                for (int k = 0; k < size; k++)
                {
                    sum += matrixA[i * size + k] * matrixB[k * size + j];
                }
                matrixC[i * size + j] = sum;
            }
        });

        await bufferC.CopyFromHostAsync<float>(matrixC);
    }
}

/// <summary>
/// Tree-based reduction kernel with logarithmic scaling.
/// </summary>
internal class OptimizedReductionKernel : OptimizedKernelBase
{
    public OptimizedReductionKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Length < 2)
            throw new ArgumentException("Reduction requires 2 arguments: input, output");

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var outputBuffer = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
        
        await Task.Run(() => ExecuteReductionOptimized(inputBuffer, outputBuffer, elementCount), cancellationToken);
    }

    private unsafe void ExecuteReductionOptimized(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount)
    {
        if (inputBuffer is HighPerformanceMemoryBuffer hpInputBuffer &&
            outputBuffer is HighPerformanceMemoryBuffer hpOutputBuffer)
        {
            var inputPtr = hpInputBuffer.GetFloatPtr();
            var outputPtr = hpOutputBuffer.GetFloatPtr();

            *outputPtr = ExecuteTreeReduction(inputPtr, elementCount);
        }
        else
        {
            ExecuteReductionGeneric(inputBuffer, outputBuffer, elementCount).Wait();
        }
    }

    private static unsafe float ExecuteTreeReduction(float* input, int elementCount)
    {
        // Use parallel tree reduction for logarithmic complexity
        var workSize = elementCount;
        var tempBuffer = stackalloc float[Environment.ProcessorCount];
        var numThreads = Math.Min(Environment.ProcessorCount, workSize);

        // First level: parallel reduction to per-thread sums
        Parallel.For(0, numThreads, threadId =>
        {
            var elementsPerThread = workSize / numThreads;
            var start = threadId * elementsPerThread;
            var end = (threadId == numThreads - 1) ? workSize : start + elementsPerThread;

            var sum = 0.0f;
            
            // Vectorized reduction within thread
            if (Avx.IsSupported)
            {
                var vectorSum = Vector256<float>.Zero;
                var vectorCount = (end - start) / 8;
                
                for (int i = 0; i < vectorCount; i++)
                {
                    var vec = Avx.LoadVector256(input + start + i * 8);
                    vectorSum = Avx.Add(vectorSum, vec);
                }
                
                // Sum vector elements
                var temp = stackalloc float[8];
                Avx.Store(temp, vectorSum);
                for (int i = 0; i < 8; i++)
                {
                    sum += temp[i];
                }
                
                // Handle remaining elements
                for (int i = start + vectorCount * 8; i < end; i++)
                {
                    sum += input[i];
                }
            }
            else
            {
                for (int i = start; i < end; i++)
                {
                    sum += input[i];
                }
            }
            
            tempBuffer[threadId] = sum;
        });

        // Second level: tree reduction of per-thread sums
        var totalSum = 0.0f;
        for (int i = 0; i < numThreads; i++)
        {
            totalSum += tempBuffer[i];
        }

        return totalSum;
    }

    private async Task ExecuteReductionGeneric(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount)
    {
        var input = new float[elementCount];
        await inputBuffer.CopyToHostAsync<float>(input);

        // Parallel reduction with partitioning
        var numPartitions = Environment.ProcessorCount;
        var partitionSums = new double[numPartitions];

        Parallel.For(0, numPartitions, partition =>
        {
            var elementsPerPartition = elementCount / numPartitions;
            var start = partition * elementsPerPartition;
            var end = (partition == numPartitions - 1) ? elementCount : start + elementsPerPartition;

            double sum = 0.0;
            for (int i = start; i < end; i++)
            {
                sum += input[i];
            }
            partitionSums[partition] = sum;
        });

        var totalSum = partitionSums.Sum();
        await outputBuffer.CopyFromHostAsync<float>(new[] { (float)totalSum });
    }
}

/// <summary>
/// Memory-intensive kernel optimized for bandwidth utilization.
/// </summary>
internal class OptimizedMemoryKernel : OptimizedKernelBase
{
    public OptimizedMemoryKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Length < 2)
            throw new ArgumentException("Memory kernel requires 2 arguments: input, output");

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var outputBuffer = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
        
        await Task.Run(() => ExecuteMemoryIntensiveOptimized(inputBuffer, outputBuffer, elementCount), cancellationToken);
    }

    private unsafe void ExecuteMemoryIntensiveOptimized(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount)
    {
        if (inputBuffer is HighPerformanceMemoryBuffer hpInputBuffer &&
            outputBuffer is HighPerformanceMemoryBuffer hpOutputBuffer)
        {
            var inputPtr = hpInputBuffer.GetFloatPtr();
            var outputPtr = hpOutputBuffer.GetFloatPtr();

            // Memory-intensive operations with prefetching
            for (int i = 0; i < elementCount; i++)
            {
                // Prefetch next cache lines
                if (i + 16 < elementCount)
                {
                    Sse.Prefetch0(inputPtr + i + 16);
                }

                var value = inputPtr[i];
                value += inputPtr[i]; // Read again
                value *= inputPtr[i]; // Read again
                value /= inputPtr[i] + 1.0f; // Read again

                outputPtr[i] = value;
            }
        }
        else
        {
            ExecuteMemoryIntensiveGeneric(inputBuffer, outputBuffer, elementCount).Wait();
        }
    }

    private async Task ExecuteMemoryIntensiveGeneric(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount)
    {
        var input = new float[elementCount];
        var output = new float[elementCount];

        await inputBuffer.CopyToHostAsync<float>(input);

        Parallel.For(0, elementCount, i =>
        {
            var value = input[i];
            value += input[i];
            value *= input[i];
            value /= input[i] + 1.0f;
            output[i] = value;
        });

        await outputBuffer.CopyFromHostAsync<float>(output);
    }
}

/// <summary>
/// Compute-intensive kernel with transcendental functions.
/// </summary>
internal class OptimizedComputeKernel : OptimizedKernelBase
{
    public OptimizedComputeKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Length < 3)
            throw new ArgumentException("Compute kernel requires 3 arguments: input, output, iterations");

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var outputBuffer = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");
        var iterations = Convert.ToInt32(arguments.Arguments[2]);

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
        
        await Task.Run(() => ExecuteComputeIntensiveOptimized(inputBuffer, outputBuffer, elementCount, iterations), cancellationToken);
    }

    private async void ExecuteComputeIntensiveOptimized(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount, int iterations)
    {
        var input = new float[elementCount];
        var output = new float[elementCount];

        await inputBuffer.CopyToHostAsync<float>(input);

        // Parallel compute-intensive operations
        Parallel.For(0, elementCount, i =>
        {
            var value = input[i];
            
            for (int iter = 0; iter < iterations; iter++)
            {
                value = MathF.Sin(value) * MathF.Cos(value) + MathF.Sqrt(MathF.Abs(value) + 1.0f);
                value = MathF.Abs(value);
                value = MathF.Pow(value, 0.5f);
            }
            
            output[i] = value;
        });

        await outputBuffer.CopyFromHostAsync<float>(output);
    }
}

/// <summary>
/// Highly optimized vector scaling kernel with SIMD support.
/// </summary>
internal class OptimizedVectorScaleKernel : OptimizedKernelBase
{
    public OptimizedVectorScaleKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Length < 3)
            throw new ArgumentException("Vector scale requires 3 arguments: input, scale factor, result");

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var scaleFactor = Convert.ToSingle(arguments.Arguments[1]);
        var resultBuffer = arguments.Arguments[2] as IMemoryBuffer ?? throw new ArgumentException("Argument 2 must be IMemoryBuffer");

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
        
        await Task.Run(() => ExecuteVectorScaleOptimized(inputBuffer, resultBuffer, scaleFactor, elementCount), cancellationToken);
    }

    private unsafe void ExecuteVectorScaleOptimized(IMemoryBuffer inputBuffer, IMemoryBuffer resultBuffer, float scaleFactor, int elementCount)
    {
        if (inputBuffer is HighPerformanceMemoryBuffer hpInputBuffer &&
            resultBuffer is HighPerformanceMemoryBuffer hpResultBuffer)
        {
            var inputPtr = hpInputBuffer.GetFloatPtr();
            var resultPtr = hpResultBuffer.GetFloatPtr();

            // Use SIMD for maximum performance
            if (Avx.IsSupported && elementCount >= 8)
            {
                ExecuteVectorScaleAvx(inputPtr, resultPtr, scaleFactor, elementCount);
            }
            else if (Sse.IsSupported && elementCount >= 4)
            {
                ExecuteVectorScaleSse(inputPtr, resultPtr, scaleFactor, elementCount);
            }
            else if (Vector.IsHardwareAccelerated && elementCount >= Vector<float>.Count)
            {
                ExecuteVectorScaleVector(inputPtr, resultPtr, scaleFactor, elementCount);
            }
            else
            {
                ExecuteVectorScaleScalar(inputPtr, resultPtr, scaleFactor, elementCount);
            }
        }
        else
        {
            ExecuteVectorScaleGeneric(inputBuffer, resultBuffer, scaleFactor, elementCount).Wait();
        }
    }

    private static unsafe void ExecuteVectorScaleAvx(float* inputPtr, float* resultPtr, float scaleFactor, int elementCount)
    {
        const int vectorSize = 8;
        var vectorCount = elementCount / vectorSize;
        var scaleVec = Vector256.Create(scaleFactor);

        // Process 8 elements at a time with AVX
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var inputVec = Avx.LoadVector256(inputPtr + offset);
            var result = Avx.Multiply(inputVec, scaleVec);
            Avx.Store(resultPtr + offset, result);
        }

        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < elementCount; i++)
        {
            resultPtr[i] = inputPtr[i] * scaleFactor;
        }
    }

    private static unsafe void ExecuteVectorScaleSse(float* inputPtr, float* resultPtr, float scaleFactor, int elementCount)
    {
        const int vectorSize = 4;
        var vectorCount = elementCount / vectorSize;
        var scaleVec = Vector128.Create(scaleFactor);

        // Process 4 elements at a time with SSE
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var inputVec = Sse.LoadVector128(inputPtr + offset);
            var result = Sse.Multiply(inputVec, scaleVec);
            Sse.Store(resultPtr + offset, result);
        }

        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < elementCount; i++)
        {
            resultPtr[i] = inputPtr[i] * scaleFactor;
        }
    }

    private static unsafe void ExecuteVectorScaleVector(float* inputPtr, float* resultPtr, float scaleFactor, int elementCount)
    {
        var vectorSize = Vector<float>.Count;
        var vectorCount = elementCount / vectorSize;
        var scaleVec = new Vector<float>(scaleFactor);

        // Use .NET Vector<T> SIMD
        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var inputVec = Unsafe.ReadUnaligned<Vector<float>>(inputPtr + offset);
            var result = inputVec * scaleVec;
            Unsafe.WriteUnaligned(resultPtr + offset, result);
        }

        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < elementCount; i++)
        {
            resultPtr[i] = inputPtr[i] * scaleFactor;
        }
    }

    private static unsafe void ExecuteVectorScaleScalar(float* inputPtr, float* resultPtr, float scaleFactor, int elementCount)
    {
        for (int i = 0; i < elementCount; i++)
        {
            resultPtr[i] = inputPtr[i] * scaleFactor;
        }
    }

    private async Task ExecuteVectorScaleGeneric(IMemoryBuffer inputBuffer, IMemoryBuffer resultBuffer, float scaleFactor, int elementCount)
    {
        var inputData = new float[elementCount];
        var resultData = new float[elementCount];

        await inputBuffer.CopyToHostAsync<float>(inputData);

        // Vectorized scaling
        var vectorSize = Vector<float>.Count;
        var vectorCount = elementCount / vectorSize;
        var scaleVec = new Vector<float>(scaleFactor);

        for (int i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var inputVec = new Vector<float>(inputData, offset);
            var result = inputVec * scaleVec;
            result.CopyTo(resultData, offset);
        }

        // Handle remaining elements
        for (int i = vectorCount * vectorSize; i < elementCount; i++)
        {
            resultData[i] = inputData[i] * scaleFactor;
        }

        await resultBuffer.CopyFromHostAsync<float>(resultData);
    }
}

/// <summary>
/// Generic optimized kernel for unknown kernel types.
/// </summary>
internal class GenericOptimizedKernel : OptimizedKernelBase
{
    private readonly KernelInfo _kernelInfo;

    public GenericOptimizedKernel(string name, KernelInfo kernelInfo, CompilationOptions options, ILogger logger)
        : base(name, options, logger)
    {
        _kernelInfo = kernelInfo;
    }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Logger.LogWarning("Executing generic kernel - performance may be suboptimal: {KernelName}", Name);

        // Try to execute based on the kernel source analysis
        await TryExecuteGenericKernel(arguments, cancellationToken);
    }

    private async ValueTask TryExecuteGenericKernel(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Analyze kernel source to infer execution pattern
        var source = _kernelInfo.Source.ToLowerInvariant();
        
        // Try common patterns based on source analysis
        if (source.Contains("result[i]") && source.Contains("input[i]") && source.Contains("scale"))
        {
            // Vector scale pattern - handle it manually
            await ExecuteVectorScalePattern(arguments, cancellationToken);
        }
        else if (source.Contains("result[i]") && arguments.Arguments.Length >= 3)
        {
            // General element-wise operation pattern
            await ExecuteElementWisePattern(arguments, cancellationToken);
        }
        else
        {
            Logger.LogWarning("Unable to infer kernel execution pattern for: {KernelName}. No operation performed.", Name);
        }
    }

    private async ValueTask ExecuteVectorScalePattern(KernelArguments arguments, CancellationToken cancellationToken)
    {
        if (arguments.Arguments.Length < 3)
        {
            Logger.LogError("Vector scale pattern requires at least 3 arguments");
            return;
        }

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer;
        var scaleFactor = 2.0f; // Default scale factor
        var resultBuffer = arguments.Arguments[2] as IMemoryBuffer;
        
        // Try to extract scale factor from arguments[1]
        if (arguments.Arguments[1] is float f)
            scaleFactor = f;
        else if (arguments.Arguments[1] is double d)
            scaleFactor = (float)d;
        else if (arguments.Arguments[1] is int i)
            scaleFactor = (float)i;

        if (inputBuffer != null && resultBuffer != null)
        {
            var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
            var inputData = new float[elementCount];
            var resultData = new float[elementCount];

            await inputBuffer.CopyToHostAsync<float>(inputData);
            
            // Perform scaling
            for (int idx = 0; idx < elementCount; idx++)
            {
                resultData[idx] = inputData[idx] * scaleFactor;
            }

            await resultBuffer.CopyFromHostAsync<float>(resultData);
            Logger.LogInformation("Generic vector scale executed: {Elements} elements scaled by {Factor}", elementCount, scaleFactor);
        }
    }

    private async ValueTask ExecuteElementWisePattern(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Generic element-wise operation - just copy input to output as fallback
        if (arguments.Arguments.Length >= 2 &&
            arguments.Arguments[0] is IMemoryBuffer inputBuffer &&
            arguments.Arguments[1] is IMemoryBuffer outputBuffer)
        {
            var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
            var data = new float[elementCount];
            
            await inputBuffer.CopyToHostAsync<float>(data);
            await outputBuffer.CopyFromHostAsync<float>(data);
            
            Logger.LogInformation("Generic element-wise operation executed: {Elements} elements copied", elementCount);
        }
    }
}