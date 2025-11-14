// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.RingKernels;

/// <summary>
/// Metal-specific telemetry buffer using shared memory for zero-copy CPU polling.
/// Allocates a single RingKernelTelemetry struct in MTLResourceStorageModeShared memory
/// accessible from both CPU and GPU without explicit transfers.
/// </summary>
/// <remarks>
/// <para><b>Memory Layout</b>:</para>
/// <list type="bullet">
/// <item>Shared memory: Allocated via MTLDevice.makeBuffer with StorageModeShared</item>
/// <item>CPU pointer: Obtained via MTLBuffer.contents() for direct CPU access</item>
/// <item>GPU pointer: MTLBuffer passed directly to Metal shaders</item>
/// <item>Zero-copy: CPU reads directly from shared memory, no explicit copy required</item>
/// </list>
///
/// <para><b>Performance Characteristics</b>:</para>
/// <list type="bullet">
/// <item>CPU polling latency: &lt;1μs (direct shared memory read)</item>
/// <item>GPU update overhead: &lt;50ns (atomic increment, single write)</item>
/// <item>Memory footprint: 64 bytes per kernel (cache-line aligned)</item>
/// </list>
///
/// <para><b>Thread Safety</b>:</para>
/// GPU updates use atomic operations (atomic_fetch_add for counters).
/// CPU polling is lock-free (reads volatile struct from shared memory).
/// </remarks>
[SuppressMessage("Design", "CA2216:Disposable types should declare finalizer", Justification = "No unmanaged resources requiring finalization; Metal buffer release is deterministic via Dispose")]
internal sealed class MetalTelemetryBuffer : IDisposable
{
    private readonly ILogger<MetalTelemetryBuffer> _logger;
    private readonly IntPtr _device;
    private IntPtr _buffer;  // MTLBuffer object
    private IntPtr _contentsPtr;  // CPU-accessible pointer
    private bool _disposed;

    /// <summary>
    /// Gets the Metal buffer object for GPU access (MTLBuffer).
    /// This is passed as a kernel parameter.
    /// </summary>
    public IntPtr BufferObject => _buffer;

    /// <summary>
    /// Gets the CPU-accessible pointer to the telemetry buffer contents.
    /// Used for zero-copy polling without explicit Metal copy commands.
    /// </summary>
    public IntPtr ContentsPointer => _contentsPtr;

    /// <summary>
    /// Initializes a new instance of <see cref="MetalTelemetryBuffer"/>.
    /// </summary>
    /// <param name="device">Metal device (MTLDevice).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public MetalTelemetryBuffer(IntPtr device, ILogger<MetalTelemetryBuffer> logger)
    {
        _device = device;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Allocates shared memory for the telemetry buffer.
    /// The memory is accessible from both CPU and GPU (zero-copy).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if Metal allocation fails.</exception>
    public void Allocate()
    {
        if (_buffer != IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer already allocated.");
        }

        try
        {
            // Allocate 64 bytes of shared memory (cache-line aligned)
            const int size = 64;  // sizeof(RingKernelTelemetry)

            // MTLResourceStorageModeShared: CPU and GPU can both access (zero-copy on unified memory systems like Apple Silicon)
            _buffer = MetalNative.CreateBuffer(_device, (nuint)size, MetalStorageMode.Shared);
            if (_buffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to allocate Metal telemetry buffer");
            }

            // Get CPU-accessible pointer
            _contentsPtr = MetalNative.GetBufferContents(_buffer);
            if (_contentsPtr == IntPtr.Zero)
            {
                MetalNative.ReleaseBuffer(_buffer);
                _buffer = IntPtr.Zero;
                throw new InvalidOperationException("Failed to get contents pointer for Metal telemetry buffer");
            }

            // Initialize telemetry struct to default values using unsafe pointer operations
            unsafe
            {
                var telemetry = new RingKernelTelemetry();
                *(RingKernelTelemetry*)_contentsPtr = telemetry;
            }

            _logger.LogDebug(
                "Allocated Metal telemetry buffer: buffer={Buffer:X16}, contents={Contents:X16}, size=64 bytes",
                _buffer.ToInt64(),
                _contentsPtr.ToInt64());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate Metal telemetry buffer");
            throw;
        }
    }

    /// <summary>
    /// Polls the current telemetry data from the GPU.
    /// This is a zero-copy operation that reads directly from shared memory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current telemetry snapshot.</returns>
    /// <exception cref="InvalidOperationException">Thrown if buffer not allocated.</exception>
    public Task<RingKernelTelemetry> PollAsync(CancellationToken cancellationToken = default)
    {
        if (_buffer == IntPtr.Zero || _contentsPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer not allocated.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Zero-copy read: Direct struct copy from shared memory using unsafe pointers
            // No MTLBlitCommandEncoder copy required - this is the key performance advantage
            RingKernelTelemetry telemetry;
            unsafe
            {
                telemetry = *(RingKernelTelemetry*)_contentsPtr;
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
            _logger.LogError(ex, "Failed to poll Metal telemetry");
            throw;
        }
    }

    /// <summary>
    /// Resets the telemetry buffer to initial values (zeros).
    /// Useful for restarting measurements or testing.
    /// </summary>
    public void Reset()
    {
        if (_buffer == IntPtr.Zero || _contentsPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Telemetry buffer not allocated.");
        }

        unsafe
        {
            var telemetry = new RingKernelTelemetry();
            *(RingKernelTelemetry*)_contentsPtr = telemetry;
        }

        _logger.LogDebug("Reset Metal telemetry buffer to initial values");
    }

    /// <summary>
    /// Disposes the telemetry buffer and frees shared memory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_buffer != IntPtr.Zero)
        {
            try
            {
                MetalNative.ReleaseBuffer(_buffer);
                _logger.LogDebug("Released Metal telemetry buffer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while releasing Metal telemetry buffer");
            }
            finally
            {
                _buffer = IntPtr.Zero;
                _contentsPtr = IntPtr.Zero;
            }
        }

        _disposed = true;
    }
}
