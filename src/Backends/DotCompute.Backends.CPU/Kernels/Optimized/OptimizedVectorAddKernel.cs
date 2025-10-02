// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Optimized;

/// <summary>
/// Highly optimized vector addition kernel with SIMD support for efficient element-wise
/// addition of two vectors.
/// </summary>
/// <remarks>
/// This kernel provides optimized vector addition using SIMD instructions where available.
/// It automatically falls back to scalar operations for elements that don't fit into
/// vector lanes. The implementation uses <see cref="Vector{T}"/> for hardware acceleration.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="OptimizedVectorAddKernel"/> class.
/// </remarks>
/// <param name="name">The name of the kernel.</param>
/// <param name="options">The compilation options for the kernel.</param>
/// <param name="logger">The logger instance for diagnostics.</param>
internal class OptimizedVectorAddKernel(string name, CompilationOptions options, ILogger logger) : Base.OptimizedKernelBase(name, options, logger)
{

    /// <summary>
    /// Executes the vector addition kernel asynchronously.
    /// </summary>
    /// <param name="arguments">The kernel arguments containing input vectors A, B and output vector.</param>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid or insufficient.</exception>
    /// <remarks>
    /// Requires 3 arguments:
    /// - Argument 0: Input vector A (IUnifiedMemoryBuffer)
    /// - Argument 1: Input vector B (IUnifiedMemoryBuffer)
    /// - Argument 2: Output vector (IUnifiedMemoryBuffer)
    /// </remarks>
    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 3)
        {
            throw new ArgumentException("Vector add requires 3 arguments: input A, input B, output");
        }

        var bufferA = arguments.Arguments[0] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IUnifiedMemoryBuffer");
        var bufferB = arguments.Arguments[1] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IUnifiedMemoryBuffer");
        var bufferResult = arguments.Arguments[2] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 2 must be IUnifiedMemoryBuffer");

        var elementCount = (int)(bufferA.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteVectorAddOptimizedAsync(bufferA, bufferB, bufferResult, elementCount), cancellationToken);
    }

    /// <summary>
    /// Executes the optimized vector addition operation.
    /// </summary>
    /// <param name="bufferA">The first input vector buffer.</param>
    /// <param name="bufferB">The second input vector buffer.</param>
    /// <param name="bufferResult">The output vector buffer.</param>
    /// <param name="elementCount">The number of elements to process.</param>
    private static async Task ExecuteVectorAddOptimizedAsync(IUnifiedMemoryBuffer bufferA, IUnifiedMemoryBuffer bufferB, IUnifiedMemoryBuffer bufferResult, int elementCount)
        // Use generic implementation since we don't have direct access to HighPerformanceMemoryBuffer here
        // This could be optimized further by exposing unsafe pointers through IUnifiedMemoryBuffer
        => await ExecuteVectorAddGenericAsync(bufferA, bufferB, bufferResult, elementCount);

    /// <summary>
    /// Executes the vector addition using generic memory buffer operations with SIMD optimization.
    /// </summary>
    /// <param name="bufferA">The first input vector buffer.</param>
    /// <param name="bufferB">The second input vector buffer.</param>
    /// <param name="bufferResult">The output vector buffer.</param>
    /// <param name="elementCount">The number of elements to process.</param>
    private static async Task ExecuteVectorAddGenericAsync(IUnifiedMemoryBuffer bufferA, IUnifiedMemoryBuffer bufferB, IUnifiedMemoryBuffer bufferResult, int elementCount)
    {
        var dataA = new float[elementCount];
        var dataB = new float[elementCount];
        var dataResult = new float[elementCount];

        await bufferA.CopyToAsync<float>(dataA);
        await bufferB.CopyToAsync<float>(dataB);

        // Vectorized addition using SIMD when available
        var vectorSize = Vector<float>.Count;
        var vectorCount = elementCount / vectorSize;

        // Process full vectors
        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vecA = new Vector<float>(dataA, offset);
            var vecB = new Vector<float>(dataB, offset);
            var result = vecA + vecB;
            result.CopyTo(dataResult, offset);
        }

        // Handle remaining elements with scalar operations
        for (var i = vectorCount * vectorSize; i < elementCount; i++)
        {
            dataResult[i] = dataA[i] + dataB[i];
        }

        await bufferResult.CopyFromAsync<float>(dataResult);
    }
}