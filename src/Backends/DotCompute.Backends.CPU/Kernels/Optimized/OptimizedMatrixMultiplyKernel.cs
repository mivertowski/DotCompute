// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Optimized;

/// <summary>
/// Cache-optimized matrix multiplication kernel that performs efficient matrix-matrix
/// multiplication using parallel processing.
/// </summary>
/// <remarks>
/// This kernel implements cache-optimized matrix multiplication using parallel processing
/// to maximize CPU utilization. The implementation uses row-major order and leverages
/// parallel loops for optimal performance on multi-core systems.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="OptimizedMatrixMultiplyKernel"/> class.
/// </remarks>
/// <param name="name">The name of the kernel.</param>
/// <param name="options">The compilation options for the kernel.</param>
/// <param name="logger">The logger instance for diagnostics.</param>
internal class OptimizedMatrixMultiplyKernel(string name, CompilationOptions options, ILogger logger) : Base.OptimizedKernelBase(name, options, logger)
{

    /// <summary>
    /// Executes the matrix multiplication kernel asynchronously.
    /// </summary>
    /// <param name="arguments">The kernel arguments containing input matrices A, B, output matrix C, and size.</param>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid or insufficient.</exception>
    /// <remarks>
    /// Requires 4 arguments:
    /// - Argument 0: Input matrix A (IUnifiedMemoryBuffer)
    /// - Argument 1: Input matrix B (IUnifiedMemoryBuffer)
    /// - Argument 2: Output matrix C (IUnifiedMemoryBuffer)
    /// - Argument 3: Matrix size (int) - assumes square matrices
    /// </remarks>
    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 4)
        {
            throw new ArgumentException("Matrix multiply requires 4 arguments: matrix A, matrix B, result C, size");
        }

        var bufferA = arguments.Arguments[0] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IUnifiedMemoryBuffer");
        var bufferB = arguments.Arguments[1] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IUnifiedMemoryBuffer");
        var bufferC = arguments.Arguments[2] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 2 must be IUnifiedMemoryBuffer");
        var size = Convert.ToInt32(arguments.Arguments[3], CultureInfo.InvariantCulture);

        await Task.Run(() => ExecuteMatrixMultiplyGenericAsync(bufferA, bufferB, bufferC, size), cancellationToken);
    }

    /// <summary>
    /// Executes the matrix multiplication using optimized parallel processing.
    /// </summary>
    /// <param name="bufferA">The first input matrix buffer (A).</param>
    /// <param name="bufferB">The second input matrix buffer (B).</param>
    /// <param name="bufferC">The output matrix buffer (C = A * B).</param>
    /// <param name="size">The size of the square matrices.</param>
    /// <remarks>
    /// Implements the standard matrix multiplication algorithm C[i,j] = Σ(A[i,k] * B[k,j])
    /// using parallel processing for the outer loop to maximize CPU utilization.
    /// </remarks>
    private static async Task ExecuteMatrixMultiplyGenericAsync(IUnifiedMemoryBuffer bufferA, IUnifiedMemoryBuffer bufferB, IUnifiedMemoryBuffer bufferC, int size)
    {
        var matrixA = new float[size * size];
        var matrixB = new float[size * size];
        var matrixC = new float[size * size];

        await bufferA.CopyToAsync<float>(matrixA);
        await bufferB.CopyToAsync<float>(matrixB);

        // Parallel matrix multiplication using cache-optimized access patterns
        _ = Parallel.For(0, size, i =>
        {
            for (var j = 0; j < size; j++)
            {
                var sum = 0.0f;
                for (var k = 0; k < size; k++)
                {
                    sum += matrixA[i * size + k] * matrixB[k * size + j];
                }
                matrixC[i * size + j] = sum;
            }
        });

        await bufferC.CopyFromAsync<float>(matrixC);
    }
}
