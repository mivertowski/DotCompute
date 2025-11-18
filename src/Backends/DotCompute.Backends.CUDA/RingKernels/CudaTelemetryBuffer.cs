// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// CUDA-specific telemetry buffer using pinned host memory for zero-copy CPU polling.
/// Allocates a single RingKernelTelemetry struct in page-locked host memory accessible
/// from both CPU and GPU without explicit transfers.
/// </summary>
/// <remarks>
/// <para><b>Memory Layout</b>:</para>
/// <list type="bullet">
/// <item>Host memory: Allocated via cuMemHostAlloc with CU_MEMHOSTALLOC_DEVICEMAP</item>
/// <item>Device pointer: Obtained via cuMemHostGetDevicePointer for GPU access</item>
/// <item>Zero-copy: CPU reads directly from pinned memory, no cuMemcpy required</item>
/// </list>
///
/// <para><b>Performance Characteristics</b>:</para>
/// <list type="bullet">
/// <item>CPU polling latency: &lt;1μs (direct memory read)</item>
/// <item>GPU update overhead: &lt;50ns (atomic increment, single write)</item>
/// <item>Memory footprint: 64 bytes per kernel (cache-line aligned)</item>
/// </list>
///
/// <para><b>Thread Safety</b>:</para>
/// GPU updates use atomic operations (atomicAdd for counters).
/// CPU polling is lock-free (reads volatile struct from pinned memory).
/// </remarks>
internal sealed class CudaTelemetryBuffer : IDisposable
{
    private readonly ILogger<CudaTelemetryBuffer> _logger;
    private IntPtr _hostPtr;  // Pinned host memory pointer
    private IntPtr _devicePtr;  // Device-accessible pointer (same physical memory)
    private bool _disposed;

    /// <summary>
    /// Gets the device pointer for GPU access to the telemetry buffer.
    /// This pointer is passed as a kernel parameter.
    /// </summary>
    public IntPtr DevicePointer => _devicePtr;

    /// <summary>
    /// Gets the host pointer for CPU access to the telemetry buffer.
    /// Used for zero-copy polling without cuMemcpy.
    /// </summary>
    public IntPtr HostPointer => _hostPtr;

    /// <summary>
    /// Initializes a new instance of <see cref="CudaTelemetryBuffer"/>.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public CudaTelemetryBuffer(ILogger<CudaTelemetryBuffer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Allocates pinned host memory for the telemetry buffer.
    /// The memory is accessible from both CPU and GPU (zero-copy).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if CUDA allocation fails.</exception>
    public void Allocate()
    {
        if (_hostPtr != IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer already allocated.");
        }

        // Check if CUDA is available
        var deviceCheckResult = CudaRuntime.cudaGetDeviceCount(out int deviceCount);

        if (deviceCheckResult != CudaError.Success || deviceCount == 0)
        {
            throw new InvalidOperationException(
                $"CUDA not available or no devices found. Cannot allocate telemetry buffer. " +
                $"Error: {CudaRuntime.GetErrorString(deviceCheckResult)}");
        }

        try
        {
            // Allocate 64 bytes of pinned host memory (cache-line aligned)
            const int size = 64;  // sizeof(RingKernelTelemetry)

            // Mapped | Portable: Map host memory to device address space (zero-copy) and make it accessible from all CUDA contexts
            const CudaHostAllocFlags flags = CudaHostAllocFlags.Mapped | CudaHostAllocFlags.Portable;

            var result = CudaRuntime.cudaHostAlloc(ref _hostPtr, (ulong)size, (uint)flags);
            CudaRuntime.CheckError(result, "allocating pinned telemetry buffer");

            // Get device pointer for the same physical memory
            result = CudaRuntime.cudaHostGetDevicePointer(ref _devicePtr, _hostPtr, 0);
            CudaRuntime.CheckError(result, "getting device pointer for telemetry buffer");

            // Initialize telemetry struct to default values
            var telemetry = new RingKernelTelemetry();
            Marshal.StructureToPtr(telemetry, _hostPtr, fDeleteOld: false);

            _logger.LogDebug(
                "Allocated CUDA telemetry buffer: host={HostPtr:X16}, device={DevicePtr:X16}, size=64 bytes",
                _hostPtr.ToInt64(),
                _devicePtr.ToInt64());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate CUDA telemetry buffer");
            throw;
        }
    }

    /// <summary>
    /// Polls the current telemetry data from the GPU.
    /// This is a zero-copy operation that reads directly from pinned host memory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current telemetry snapshot.</returns>
    /// <exception cref="InvalidOperationException">Thrown if buffer not allocated.</exception>
    public Task<RingKernelTelemetry> PollAsync(CancellationToken cancellationToken = default)
    {
        if (_hostPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer not allocated.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Zero-copy read: Marshal struct directly from pinned host memory
            // No cuMemcpy required - this is the key performance advantage
            var telemetry = Marshal.PtrToStructure<RingKernelTelemetry>(_hostPtr);

            _logger.LogTrace(
                "Polled telemetry: processed={MessagesProcessed}, dropped={MessagesDropped}, queueDepth={QueueDepth}",
                telemetry.MessagesProcessed,
                telemetry.MessagesDropped,
                telemetry.QueueDepth);

            return Task.FromResult(telemetry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll CUDA telemetry");
            throw;
        }
    }

    /// <summary>
    /// Resets the telemetry buffer to initial values (zeros).
    /// Useful for restarting measurements or testing.
    /// </summary>
    public void Reset()
    {
        if (_hostPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer not allocated.");
        }

        var telemetry = new RingKernelTelemetry();
        Marshal.StructureToPtr(telemetry, _hostPtr, fDeleteOld: true);

        _logger.LogDebug("Reset CUDA telemetry buffer to initial values");
    }

    /// <summary>
    /// Disposes the telemetry buffer and frees pinned host memory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_hostPtr != IntPtr.Zero)
        {
            try
            {
                var result = CudaRuntime.cudaFreeHost(_hostPtr);
                CudaRuntime.CheckError(result, "freeing telemetry buffer");
                _logger.LogDebug("Freed CUDA telemetry buffer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while freeing CUDA telemetry buffer");
            }
            finally
            {
                _hostPtr = IntPtr.Zero;
                _devicePtr = IntPtr.Zero;
            }
        }

        _disposed = true;
    }
}
