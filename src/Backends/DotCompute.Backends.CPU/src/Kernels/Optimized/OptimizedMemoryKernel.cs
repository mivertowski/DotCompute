// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Base;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Optimized;

/// <summary>
/// Memory-intensive kernel optimized for bandwidth utilization that performs
/// memory-bound operations with efficient data movement patterns.
/// </summary>
/// <remarks>
/// This kernel is designed to test and optimize memory bandwidth utilization
/// by performing operations that are primarily limited by memory access patterns
/// rather than computational complexity. It uses parallel processing to maximize
/// memory throughput.
/// </remarks>
internal class OptimizedMemoryKernel : Base.OptimizedKernelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizedMemoryKernel"/> class.
    /// </summary>
    /// <param name="name">The name of the kernel.</param>
    /// <param name="options">The compilation options for the kernel.</param>
    /// <param name="logger">The logger instance for diagnostics.</param>
    public OptimizedMemoryKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    /// <summary>
    /// Executes the memory-intensive kernel asynchronously.
    /// </summary>
    /// <param name="arguments">The kernel arguments containing input and output buffers.</param>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid or insufficient.</exception>
    /// <remarks>
    /// Requires 2 arguments:
    /// - Argument 0: Input vector (IMemoryBuffer)
    /// - Argument 1: Output vector (IMemoryBuffer)
    /// </remarks>
    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 2)
        {
            throw new ArgumentException("Memory kernel requires 2 arguments: input, output");
        }

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var outputBuffer = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteMemoryIntensiveGenericAsync(inputBuffer, outputBuffer, elementCount), cancellationToken);
    }

    /// <summary>
    /// Executes memory-intensive operations that stress memory bandwidth and access patterns.
    /// </summary>
    /// <param name="inputBuffer">The input vector buffer.</param>
    /// <param name="outputBuffer">The output vector buffer.</param>
    /// <param name="elementCount">The number of elements to process.</param>
    /// <remarks>
    /// Performs a series of memory-bound operations including multiple reads and writes
    /// per element to test memory subsystem performance. The operations are designed
    /// to be memory-bandwidth limited rather than compute-limited.
    /// </remarks>
    private static async Task ExecuteMemoryIntensiveGenericAsync(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount)
    {
        var input = new float[elementCount];
        var output = new float[elementCount];

        await inputBuffer.CopyToHostAsync<float>(input);

        // Parallel memory-intensive operations with multiple memory accesses per element
        _ = Parallel.For(0, elementCount, i =>
        {
            var value = input[i];
            value += input[i];      // Additional read
            value *= input[i];      // Another read
            value /= input[i] + 1.0f; // Yet another read with computation
            output[i] = value;
        });

        await outputBuffer.CopyFromHostAsync<float>(output);
    }
}