// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.OpenCL.Types.Native;
using DotCompute.Backends.OpenCL.Interop;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.RingKernels;

/// <summary>
/// OpenCL-specific telemetry buffer using pinned host memory for zero-copy CPU polling.
/// Allocates a single RingKernelTelemetry struct in page-locked host memory accessible
/// from both CPU and GPU without explicit transfers.
/// </summary>
/// <remarks>
/// <para><b>Memory Layout</b>:</para>
/// <list type="bullet">
/// <item>Host memory: Allocated via clCreateBuffer with CL_MEM_ALLOC_HOST_PTR</item>
/// <item>Device pointer: Mapped via clEnqueueMapBuffer for GPU access</item>
/// <item>Zero-copy: CPU reads directly from pinned memory via clEnqueueMapBuffer</item>
/// </list>
///
/// <para><b>Performance Characteristics</b>:</para>
/// <list type="bullet">
/// <item>CPU polling latency: &lt;1μs (mapped pinned memory read)</item>
/// <item>GPU update overhead: &lt;50ns (atomic increment, single write)</item>
/// <item>Memory footprint: 64 bytes per kernel (cache-line aligned)</item>
/// </list>
///
/// <para><b>Thread Safety</b>:</para>
/// GPU updates use atomic operations (atomic_add for counters).
/// CPU polling is lock-free (reads volatile struct from mapped memory).
/// </remarks>
internal sealed class OpenCLTelemetryBuffer : IDisposable
{
    private readonly ILogger<OpenCLTelemetryBuffer> _logger;
    private readonly IntPtr _context;
    private readonly IntPtr _commandQueue;
    private IntPtr _buffer;  // cl_mem buffer object
    private IntPtr _mappedPtr;  // Mapped host pointer for CPU access
    private bool _disposed;

    /// <summary>
    /// Gets the buffer object for GPU access (cl_mem).
    /// This is passed as a kernel parameter.
    /// </summary>
    public IntPtr BufferObject => _buffer;

    /// <summary>
    /// Gets the mapped host pointer for CPU access to the telemetry buffer.
    /// Used for zero-copy polling without clEnqueueReadBuffer.
    /// </summary>
    public IntPtr MappedPointer => _mappedPtr;

    /// <summary>
    /// Initializes a new instance of <see cref="OpenCLTelemetryBuffer"/>.
    /// </summary>
    /// <param name="context">OpenCL context.</param>
    /// <param name="commandQueue">OpenCL command queue for mapping operations.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public OpenCLTelemetryBuffer(IntPtr context, IntPtr commandQueue, ILogger<OpenCLTelemetryBuffer> logger)
    {
        _context = context;
        _commandQueue = commandQueue;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Allocates pinned host memory for the telemetry buffer.
    /// The memory is accessible from both CPU and GPU (zero-copy).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if OpenCL allocation fails.</exception>
    public void Allocate()
    {
        if (_buffer != IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer already allocated.");
        }

        try
        {
            // Allocate 64 bytes of pinned host memory (cache-line aligned)
            const int size = 64;  // sizeof(RingKernelTelemetry)

            // CL_MEM_ALLOC_HOST_PTR: Allocate in pinned host memory for zero-copy access
            // CL_MEM_READ_WRITE: Kernel can read/write, host can read
            const MemoryFlags flags = MemoryFlags.AllocHostPtr | MemoryFlags.ReadWrite;

            _buffer = OpenCLRuntime.clCreateBuffer(_context, flags, (nuint)size, IntPtr.Zero, out var errorCode);
            OpenCLException.ThrowIfError(errorCode, "Create telemetry buffer");

            // Map the buffer to get host pointer for CPU access
            // CL_MAP_READ | CL_MAP_WRITE = (1 << 0) | (1 << 1)
            const ulong mapFlags = (1ul << 0) | (1ul << 1);

            _mappedPtr = OpenCLNative.clEnqueueMapBuffer(
                _commandQueue,
                _buffer,
                1u, // blocking = true
                mapFlags,
                UIntPtr.Zero, // offset
                (UIntPtr)size,
                0, // numEventsInWaitList
                null, // eventWaitList
                out var mapEvent,
                out var mapErrorCode);

            if (mapErrorCode != CLResultCode.Success)
            {
                throw new InvalidOperationException($"Failed to map telemetry buffer: {mapErrorCode}");
            }

            // Release map event
            if (mapEvent != IntPtr.Zero)
            {
                OpenCLRuntime.clReleaseEvent(mapEvent);
            }

            // Initialize telemetry struct to default values
            var telemetry = new RingKernelTelemetry();
            Marshal.StructureToPtr(telemetry, _mappedPtr, fDeleteOld: false);

            _logger.LogDebug(
                "Allocated OpenCL telemetry buffer: buffer={Buffer:X16}, mapped={MappedPtr:X16}, size=64 bytes",
                _buffer.ToInt64(),
                _mappedPtr.ToInt64());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate OpenCL telemetry buffer");
            throw;
        }
    }

    /// <summary>
    /// Polls the current telemetry data from the GPU.
    /// This is a zero-copy operation that reads directly from mapped pinned memory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current telemetry snapshot.</returns>
    /// <exception cref="InvalidOperationException">Thrown if buffer not allocated.</exception>
    public Task<RingKernelTelemetry> PollAsync(CancellationToken cancellationToken = default)
    {
        if (_buffer == IntPtr.Zero || _mappedPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer not allocated.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Zero-copy read: Marshal struct directly from mapped pinned memory
            // No clEnqueueReadBuffer required - this is the key performance advantage
            var telemetry = Marshal.PtrToStructure<RingKernelTelemetry>(_mappedPtr);

            _logger.LogTrace(
                "Polled telemetry: processed={MessagesProcessed}, dropped={MessagesDropped}, queueDepth={QueueDepth}",
                telemetry.MessagesProcessed,
                telemetry.MessagesDropped,
                telemetry.QueueDepth);

            return Task.FromResult(telemetry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll OpenCL telemetry");
            throw;
        }
    }

    /// <summary>
    /// Resets the telemetry buffer to initial values (zeros).
    /// Useful for restarting measurements or testing.
    /// </summary>
    public void Reset()
    {
        if (_buffer == IntPtr.Zero || _mappedPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer not allocated.");
        }

        var telemetry = new RingKernelTelemetry();
        Marshal.StructureToPtr(telemetry, _mappedPtr, fDeleteOld: true);

        _logger.LogDebug("Reset OpenCL telemetry buffer to initial values");
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

        if (_mappedPtr != IntPtr.Zero && _buffer != IntPtr.Zero)
        {
            try
            {
                // Unmap the buffer
                var errorCode = OpenCLNative.clEnqueueUnmapMemObject(
                    _commandQueue,
                    _buffer,
                    _mappedPtr,
                    0, // numEventsInWaitList
                    null, // eventWaitList
                    out var unmapEvent);

                if (errorCode != CLResultCode.Success)
                {
                    _logger.LogWarning("Failed to unmap telemetry buffer: {ErrorCode}", errorCode);
                }

                // Release unmap event
                if (unmapEvent != IntPtr.Zero)
                {
                    OpenCLRuntime.clReleaseEvent(unmapEvent);
                }

                _mappedPtr = IntPtr.Zero;
                _logger.LogDebug("Unmapped OpenCL telemetry buffer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while unmapping OpenCL telemetry buffer");
            }
        }

        if (_buffer != IntPtr.Zero)
        {
            try
            {
                var errorCode = OpenCLRuntime.clReleaseMemObject(_buffer);
                if (errorCode != OpenCLError.Success)
                {
                    _logger.LogWarning("Failed to release telemetry buffer: {ErrorCode}", errorCode);
                }

                _buffer = IntPtr.Zero;
                _logger.LogDebug("Released OpenCL telemetry buffer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while releasing OpenCL telemetry buffer");
            }
        }

        _disposed = true;
    }
}
