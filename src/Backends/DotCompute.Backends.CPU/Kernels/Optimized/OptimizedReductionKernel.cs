// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Optimized;

/// <summary>
/// Tree-based reduction kernel with logarithmic scaling that efficiently computes
/// the sum of all elements in a vector using parallel partitioning.
/// </summary>
/// <remarks>
/// This kernel implements an optimized reduction operation using parallel partitioning
/// to sum all elements in a vector. The algorithm divides the input into partitions
/// processed by different threads, then combines the results for the final sum.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="OptimizedReductionKernel"/> class.
/// </remarks>
/// <param name="name">The name of the kernel.</param>
/// <param name="options">The compilation options for the kernel.</param>
/// <param name="logger">The logger instance for diagnostics.</param>
internal class OptimizedReductionKernel(string name, CompilationOptions options, ILogger logger) : Base.OptimizedKernelBase(name, options, logger)
{

    /// <summary>
    /// Executes the reduction kernel asynchronously.
    /// </summary>
    /// <param name="arguments">The kernel arguments containing input vector and output scalar.</param>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid or insufficient.</exception>
    /// <remarks>
    /// Requires 2 arguments:
    /// - Argument 0: Input vector (IUnifiedMemoryBuffer)
    /// - Argument 1: Output scalar (IUnifiedMemoryBuffer) - single element result
    /// </remarks>
    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 2)
        {
            throw new ArgumentException("Reduction requires 2 arguments: input, output");
        }

        var inputBuffer = arguments.Arguments[0] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IUnifiedMemoryBuffer");
        var outputBuffer = arguments.Arguments[1] as IUnifiedMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IUnifiedMemoryBuffer");

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteReductionGenericAsync(inputBuffer, outputBuffer, elementCount), cancellationToken);
    }

    /// <summary>
    /// Executes the reduction operation using parallel partitioning for optimal performance.
    /// </summary>
    /// <param name="inputBuffer">The input vector buffer containing elements to sum.</param>
    /// <param name="outputBuffer">The output buffer to store the reduction result.</param>
    /// <param name="elementCount">The number of elements in the input vector.</param>
    /// <remarks>
    /// The algorithm partitions the input vector across available CPU cores, computes
    /// partial sums in parallel, then combines the results for the final sum.
    /// Uses double precision for intermediate calculations to avoid precision loss.
    /// </remarks>
    private static async Task ExecuteReductionGenericAsync(IUnifiedMemoryBuffer inputBuffer, IUnifiedMemoryBuffer outputBuffer, int elementCount)
    {
        var input = new float[elementCount];
        await inputBuffer.CopyToAsync<float>(input);

        // Parallel reduction with partitioning based on processor count
        var numPartitions = Environment.ProcessorCount;
        var partitionSums = new double[numPartitions];

        _ = Parallel.For(0, numPartitions, partition =>
        {
            var elementsPerPartition = elementCount / numPartitions;
            var start = partition * elementsPerPartition;
            var end = (partition == numPartitions - 1) ? elementCount : start + elementsPerPartition;

            var sum = 0.0;
            for (var i = start; i < end; i++)
            {
                sum += input[i];
            }
            partitionSums[partition] = sum;
        });

        // Combine partial sums to get final result
        var totalSum = partitionSums.Sum();
        await outputBuffer.CopyFromAsync<float>(new[] { (float)totalSum });
    }
}
