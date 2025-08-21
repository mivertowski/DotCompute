// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Base;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Optimized;

/// <summary>
/// Compute-intensive kernel with transcendental functions that performs
/// mathematically complex operations to stress computational units.
/// </summary>
/// <remarks>
/// This kernel is designed to test computational performance by executing
/// iterative mathematical operations including transcendental functions
/// (sin, cos, sqrt, pow) that stress the CPU's floating-point units.
/// </remarks>
internal class OptimizedComputeKernel : OptimizedKernelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizedComputeKernel"/> class.
    /// </summary>
    /// <param name="name">The name of the kernel.</param>
    /// <param name="options">The compilation options for the kernel.</param>
    /// <param name="logger">The logger instance for diagnostics.</param>
    public OptimizedComputeKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    /// <summary>
    /// Executes the compute-intensive kernel asynchronously.
    /// </summary>
    /// <param name="arguments">The kernel arguments containing input, output buffers, and iteration count.</param>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid or insufficient.</exception>
    /// <remarks>
    /// Requires 3 arguments:
    /// - Argument 0: Input vector (IMemoryBuffer)
    /// - Argument 1: Output vector (IMemoryBuffer)
    /// - Argument 2: Number of iterations (int)
    /// </remarks>
    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 3)
        {
            throw new ArgumentException("Compute kernel requires 3 arguments: input, output, iterations");
        }

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var outputBuffer = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");
        var iterations = Convert.ToInt32(arguments.Arguments[2]);

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteComputeIntensiveOptimized(inputBuffer, outputBuffer, elementCount, iterations), cancellationToken);
    }

    /// <summary>
    /// Executes compute-intensive operations with transcendental functions.
    /// </summary>
    /// <param name="inputBuffer">The input vector buffer.</param>
    /// <param name="outputBuffer">The output vector buffer.</param>
    /// <param name="elementCount">The number of elements to process.</param>
    /// <param name="iterations">The number of iterations to perform per element.</param>
    /// <remarks>
    /// Performs iterative mathematical operations including trigonometric functions,
    /// square roots, and power operations to create a compute-bound workload that
    /// stresses the floating-point execution units.
    /// </remarks>
    private static async void ExecuteComputeIntensiveOptimized(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount, int iterations)
    {
        var input = new float[elementCount];
        var output = new float[elementCount];

        await inputBuffer.CopyToHostAsync<float>(input);

        // Parallel compute-intensive operations with transcendental functions
        _ = Parallel.For(0, elementCount, i =>
        {
            var value = input[i];

            // Iterative mathematical operations to stress compute units
            for (var iter = 0; iter < iterations; iter++)
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