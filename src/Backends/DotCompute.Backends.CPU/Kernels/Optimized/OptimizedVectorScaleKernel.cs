// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Numerics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Optimized;

/// <summary>
/// Highly optimized vector scaling kernel with SIMD support that efficiently
/// multiplies all elements of a vector by a scalar value.
/// </summary>
/// <remarks>
/// This kernel provides optimized vector scaling using SIMD instructions where available.
/// It automatically falls back to scalar operations for elements that don't fit into
/// vector lanes. The implementation uses <see cref="Vector{T}"/> for hardware acceleration.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="OptimizedVectorScaleKernel"/> class.
/// </remarks>
/// <param name="name">The name of the kernel.</param>
/// <param name="options">The compilation options for the kernel.</param>
/// <param name="logger">The logger instance for diagnostics.</param>
internal class OptimizedVectorScaleKernel(string name, CompilationOptions options, ILogger logger) : Base.OptimizedKernelBase(name, options, logger)
{

    /// <summary>
    /// Executes the vector scaling kernel asynchronously.
    /// </summary>
    /// <param name="arguments">The kernel arguments containing input vector, scale factor, and output vector.</param>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid or insufficient.</exception>
    /// <remarks>
    /// Requires 3 arguments:
    /// - Argument 0: Input vector (IUnifiedMemoryBuffer)
    /// - Argument 1: Scale factor (float, double, or int)
    /// - Argument 2: Output vector (IUnifiedMemoryBuffer)
    /// </remarks>
    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 3)
        {
            throw new ArgumentException("Vector scale requires 3 arguments: input, scale factor, result");
        }

        var inputBuffer = arguments.Arguments[0] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IUnifiedMemoryBuffer");
        var scaleFactor = Convert.ToSingle(arguments.Arguments[1], CultureInfo.InvariantCulture);
        var resultBuffer = arguments.Arguments[2] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 2 must be IUnifiedMemoryBuffer");

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteVectorScaleGenericAsync(inputBuffer, resultBuffer, scaleFactor, elementCount), cancellationToken);
    }

    /// <summary>
    /// Executes the vector scaling operation using SIMD optimization where available.
    /// </summary>
    /// <param name="inputBuffer">The input vector buffer.</param>
    /// <param name="resultBuffer">The output vector buffer.</param>
    /// <param name="scaleFactor">The scalar value to multiply each element by.</param>
    /// <param name="elementCount">The number of elements to process.</param>
    /// <remarks>
    /// Uses SIMD instructions to process multiple elements simultaneously when possible,
    /// falling back to scalar operations for remaining elements. The algorithm creates
    /// a vector of scale factors for efficient SIMD multiplication.
    /// </remarks>
    private static async Task ExecuteVectorScaleGenericAsync(IUnifiedMemoryBuffer inputBuffer, IUnifiedMemoryBuffer resultBuffer, float scaleFactor, int elementCount)
    {
        var inputData = new float[elementCount];
        var resultData = new float[elementCount];

        await inputBuffer.CopyToAsync<float>(inputData);

        // Vectorized scaling using SIMD when available
        var vectorSize = Vector<float>.Count;
        var vectorCount = elementCount / vectorSize;
        var scaleVec = new Vector<float>(scaleFactor);

        // Process full vectors with SIMD operations
        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var inputVec = new Vector<float>(inputData, offset);
            var result = inputVec * scaleVec;
            result.CopyTo(resultData, offset);
        }

        // Handle remaining elements with scalar operations
        for (var i = vectorCount * vectorSize; i < elementCount; i++)
        {
            resultData[i] = inputData[i] * scaleFactor;
        }

        await resultBuffer.CopyFromAsync<float>(resultData);
    }
}
