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
    private IntPtr _hostPtr;  // Pinned host memory pointer (zero-copy mode)
    private IntPtr _devicePtr;  // Device-accessible pointer
    private IntPtr _stagingPtr;  // Staging buffer for WSL2 fallback mode
    private bool _isPinned;  // True if using pinned memory (zero-copy), false for WSL2 fallback
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
    /// Allocates memory for the telemetry buffer.
    /// Attempts pinned host memory for zero-copy, falls back to device memory in WSL2.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if CUDA allocation fails.</exception>
    public void Allocate()
    {
        if (_devicePtr != IntPtr.Zero)
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

        // Allocate 64 bytes (cache-line aligned) for RingKernelTelemetry
        const int size = 64;  // sizeof(RingKernelTelemetry)

        // Try pinned memory allocation first (zero-copy mode)
        const CudaHostAllocFlags flags = CudaHostAllocFlags.Mapped | CudaHostAllocFlags.Portable;
        var pinnedResult = CudaRuntime.cudaHostAlloc(ref _hostPtr, (ulong)size, (uint)flags);

        if (pinnedResult == CudaError.Success)
        {
            // Get device pointer for the same physical memory
            var devPtrResult = CudaRuntime.cudaHostGetDevicePointer(ref _devicePtr, _hostPtr, 0);
            if (devPtrResult == CudaError.Success)
            {
                _isPinned = true;

                // Initialize telemetry struct to default values
                var telemetry = new RingKernelTelemetry();
                Marshal.StructureToPtr(telemetry, _hostPtr, fDeleteOld: false);

                _logger.LogDebug(
                    "Allocated pinned CUDA telemetry buffer (zero-copy): host={HostPtr:X16}, device={DevicePtr:X16}",
                    _hostPtr.ToInt64(),
                    _devicePtr.ToInt64());
                return;
            }

            // Failed to get device pointer, free pinned memory and fall through to fallback
            CudaRuntime.cudaFreeHost(_hostPtr);
            _hostPtr = IntPtr.Zero;
        }

        // WSL2 fallback: Use device memory with staging buffer for reads
        _logger.LogDebug("Pinned allocation failed ({Error}), using WSL2 fallback mode with device memory",
            pinnedResult);

        // Allocate device memory
        var deviceAllocResult = CudaRuntime.cudaMalloc(ref _devicePtr, (ulong)size);
        if (deviceAllocResult != CudaError.Success)
        {
            throw new InvalidOperationException(
                $"Failed to allocate device memory for telemetry buffer: {deviceAllocResult}");
        }

        // Allocate staging buffer for CPU reads
        _stagingPtr = Marshal.AllocHGlobal(size);
        _isPinned = false;

        // Initialize telemetry on device
        var initTelemetry = new RingKernelTelemetry();
        Marshal.StructureToPtr(initTelemetry, _stagingPtr, fDeleteOld: false);
        var initResult = CudaRuntime.cudaMemcpy(
            _devicePtr, _stagingPtr, (nuint)size, CudaMemcpyKind.HostToDevice);

        if (initResult != CudaError.Success)
        {
            // Cleanup on failure
            CudaRuntime.cudaFree(_devicePtr);
            Marshal.FreeHGlobal(_stagingPtr);
            _devicePtr = IntPtr.Zero;
            _stagingPtr = IntPtr.Zero;
            throw new InvalidOperationException($"Failed to initialize telemetry buffer: {initResult}");
        }

        _logger.LogDebug(
            "Allocated CUDA telemetry buffer (WSL2 fallback): device={DevicePtr:X16}, staging={StagingPtr:X16}",
            _devicePtr.ToInt64(),
            _stagingPtr.ToInt64());
    }

    /// <summary>
    /// Polls the current telemetry data from the GPU.
    /// Zero-copy in pinned mode, uses cudaMemcpy in WSL2 fallback mode.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current telemetry snapshot.</returns>
    /// <exception cref="InvalidOperationException">Thrown if buffer not allocated.</exception>
    public Task<RingKernelTelemetry> PollAsync(CancellationToken cancellationToken = default)
    {
        if (_devicePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer not allocated.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            RingKernelTelemetry telemetry;

            if (_isPinned)
            {
                // Zero-copy read: Marshal struct directly from pinned host memory
                // No cuMemcpy required - this is the key performance advantage
                telemetry = Marshal.PtrToStructure<RingKernelTelemetry>(_hostPtr);
            }
            else
            {
                // WSL2 fallback: Copy from device to staging buffer first
                const int size = 64;  // sizeof(RingKernelTelemetry)
                var copyResult = CudaRuntime.cudaMemcpy(
                    _stagingPtr, _devicePtr, (nuint)size, CudaMemcpyKind.DeviceToHost);

                if (copyResult != CudaError.Success)
                {
                    throw new InvalidOperationException($"Failed to copy telemetry from device: {copyResult}");
                }

                telemetry = Marshal.PtrToStructure<RingKernelTelemetry>(_stagingPtr);
            }

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
        if (_devicePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer not allocated.");
        }

        var telemetry = new RingKernelTelemetry();

        if (_isPinned)
        {
            // Zero-copy: Write directly to pinned host memory
            Marshal.StructureToPtr(telemetry, _hostPtr, fDeleteOld: true);
        }
        else
        {
            // WSL2 fallback: Write to staging then copy to device
            const int size = 64;  // sizeof(RingKernelTelemetry)
            Marshal.StructureToPtr(telemetry, _stagingPtr, fDeleteOld: true);
            var copyResult = CudaRuntime.cudaMemcpy(
                _devicePtr, _stagingPtr, (nuint)size, CudaMemcpyKind.HostToDevice);

            if (copyResult != CudaError.Success)
            {
                _logger.LogWarning("Failed to reset telemetry buffer on device: {Error}", copyResult);
            }
        }

        _logger.LogDebug("Reset CUDA telemetry buffer to initial values");
    }

    /// <summary>
    /// Disposes the telemetry buffer and frees allocated memory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_isPinned && _hostPtr != IntPtr.Zero)
            {
                // Free pinned host memory (device pointer is same physical memory)
                var result = CudaRuntime.cudaFreeHost(_hostPtr);
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to free pinned telemetry buffer: {Error}", result);
                }
                _logger.LogDebug("Freed pinned CUDA telemetry buffer");
            }
            else
            {
                // WSL2 fallback: Free device memory and staging buffer separately
                if (_devicePtr != IntPtr.Zero)
                {
                    var result = CudaRuntime.cudaFree(_devicePtr);
                    if (result != CudaError.Success)
                    {
                        _logger.LogWarning("Failed to free device telemetry buffer: {Error}", result);
                    }
                }

                if (_stagingPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_stagingPtr);
                }

                _logger.LogDebug("Freed CUDA telemetry buffer (WSL2 fallback mode)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while freeing CUDA telemetry buffer");
        }
        finally
        {
            _hostPtr = IntPtr.Zero;
            _devicePtr = IntPtr.Zero;
            _stagingPtr = IntPtr.Zero;
        }

        _disposed = true;
    }
}
