// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Types;

/// <summary>
/// Extension methods for <see cref="CudaStream"/>.
/// </summary>
public static class CudaStreamExtensions
{
    /// <summary>
    /// Asynchronously synchronizes the CUDA stream, waiting for all operations to complete.
    /// </summary>
    /// <param name="stream">The CUDA stream to synchronize.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the stream is synchronized.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when stream synchronization fails.
    /// </exception>
    public static async Task SynchronizeAsync(this CudaStream stream, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var result = CudaRuntime.cudaStreamSynchronize(stream.Handle);
            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Stream synchronization failed: {result}");
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}
