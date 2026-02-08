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
    /// Thrown when stream synchronization fails or device context cannot be set.
    /// </exception>
    /// <remarks>
    /// This method captures the current CUDA device before Task.Run and ensures the device
    /// context is current on the thread pool thread. This is necessary because CUDA contexts
    /// are thread-local and Task.Run switches to a thread pool thread.
    /// </remarks>
    public static async Task SynchronizeAsync(this CudaStream stream, CancellationToken cancellationToken = default)
    {
        // Capture current device before Task.Run (CUDA contexts are thread-local)
        var deviceResult = CudaRuntime.cudaGetDevice(out var currentDevice);
        if (deviceResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to get current CUDA device: {deviceResult}");
        }

        await Task.Run(() =>
        {
            // Ensure device context is current on thread pool thread
            var setDeviceResult = CudaRuntime.cudaSetDevice(currentDevice);
            if (setDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set CUDA device {currentDevice}: {setDeviceResult}");
            }

            // Synchronize stream
            var result = CudaRuntime.cudaStreamSynchronize(stream.Handle);
            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Stream synchronization failed: {result}");
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}
