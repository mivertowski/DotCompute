// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Native.Exceptions;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Hopper
{
    /// <summary>
    /// Thin async-friendly wrapper around <c>cuMemAllocAsync</c>/<c>cuMemFreeAsync</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mirrors RustCompute's <c>hopper/async_mem.rs</c>. Stream-ordered allocation avoids the
    /// global synchronization penalty of <c>cuMemAlloc</c> and is well-suited for persistent
    /// actors that allocate/free scratch buffers from many streams concurrently.
    /// </para>
    /// <para>
    /// Memory pools themselves are supported from compute capability 6.0 upward. The async
    /// entry points require CUDA 11.2+ on the host side — that's implicit in this project's
    /// supported toolchain (CUDA 12+).
    /// </para>
    /// <para>
    /// <b>Ownership semantics:</b> This type does not own a custom pool — it simply forwards
    /// allocations to the device's default stream-ordered pool. Callers who need bespoke pool
    /// configuration (release threshold, tracking) should build on top of this layer.
    /// </para>
    /// </remarks>
    public sealed partial class AsyncMemoryPool : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<IntPtr, nuint> _outstanding = new();
        private long _totalAllocated;
        private int _disposed;

        #region LoggerMessage delegates

        [LoggerMessage(
            EventId = 9500,
            Level = LogLevel.Debug,
            Message = "AsyncMemoryPool initialized (preferred stream: {Stream})")]
        private static partial void LogInitialized(ILogger logger, IntPtr stream);

        [LoggerMessage(
            EventId = 9501,
            Level = LogLevel.Trace,
            Message = "cuMemAllocAsync size={Bytes} stream={Stream} -> 0x{Ptr:X}")]
        private static partial void LogAllocated(ILogger logger, ulong bytes, IntPtr stream, IntPtr ptr);

        [LoggerMessage(
            EventId = 9502,
            Level = LogLevel.Trace,
            Message = "cuMemFreeAsync 0x{Ptr:X} stream={Stream}")]
        private static partial void LogFreed(ILogger logger, IntPtr ptr, IntPtr stream);

        [LoggerMessage(
            EventId = 9503,
            Level = LogLevel.Warning,
            Message = "AsyncMemoryPool disposed with {Count} outstanding allocations totalling {Bytes} bytes — attempting synchronous cleanup")]
        private static partial void LogOutstandingOnDispose(ILogger logger, int count, ulong bytes);

        [LoggerMessage(
            EventId = 9504,
            Level = LogLevel.Error,
            Message = "cuMemFreeAsync during dispose failed for 0x{Ptr:X}: {Error}")]
        private static partial void LogDisposeFreeFailure(ILogger logger, IntPtr ptr, CudaError error);

        #endregion

        /// <summary>
        /// Initializes a new <see cref="AsyncMemoryPool"/>.
        /// </summary>
        /// <param name="preferredStream">
        /// Stream handle used when no explicit stream is supplied to <see cref="AllocAsync"/>/
        /// <see cref="FreeAsync"/>. <see cref="IntPtr.Zero"/> selects the default stream.
        /// </param>
        /// <param name="logger">Optional logger. <c>null</c> defaults to <see cref="NullLogger.Instance"/>.</param>
        public AsyncMemoryPool(IntPtr preferredStream = default, ILogger? logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
            PreferredStream = preferredStream;
            LogInitialized(_logger, preferredStream);
        }

        /// <summary>Stream handle used when callers don't supply one.</summary>
        public IntPtr PreferredStream { get; }

        /// <summary>Total bytes currently outstanding through this pool instance.</summary>
        public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocated);

        /// <summary>Number of outstanding allocations tracked by this wrapper.</summary>
        public int OutstandingAllocationCount => _outstanding.Count;

        /// <summary>
        /// Whether stream-ordered async allocation is supported on the given device.
        /// </summary>
        /// <remarks>
        /// Memory pools are available from CC 6.0 (Pascal) onward — see
        /// <see cref="HopperFeatures.IsAsyncMemPoolSupported(int, int)"/>.
        /// </remarks>
        public static bool IsSupported(int deviceMajor, int deviceMinor)
            => HopperFeatures.IsAsyncMemPoolSupported(deviceMajor, deviceMinor);

        /// <summary>
        /// Allocates <paramref name="sizeBytes"/> of device memory on the given stream.
        /// </summary>
        /// <param name="sizeBytes">Allocation size in bytes. Must be positive.</param>
        /// <param name="stream">
        /// Stream on which the allocation is ordered. Pass <see cref="IntPtr.Zero"/> to use
        /// <see cref="PreferredStream"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token — observed before issuing the allocation.</param>
        /// <returns>A device pointer usable on the supplied stream (and any stream ordered after it).</returns>
        /// <exception cref="ArgumentOutOfRangeException">Size is zero.</exception>
        /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
        /// <exception cref="CudaException">The CUDA driver returned an error.</exception>
        public Task<IntPtr> AllocAsync(nuint sizeBytes, IntPtr stream = default, CancellationToken cancellationToken = default)
        {
            if (sizeBytes == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Allocation size must be positive.");
            }

            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            cancellationToken.ThrowIfCancellationRequested();

            var actualStream = stream == IntPtr.Zero ? PreferredStream : stream;

            IntPtr dptr = IntPtr.Zero;
            var err = CudaRuntime.cuMemAllocAsync(ref dptr, sizeBytes, actualStream);
            if (err != CudaError.Success)
            {
                throw new CudaException(
                    string.Format(CultureInfo.InvariantCulture,
                        "cuMemAllocAsync({0} bytes) failed on stream 0x{1:X}.",
                        sizeBytes, (long)actualStream),
                    err);
            }

            _outstanding[dptr] = sizeBytes;
            Interlocked.Add(ref _totalAllocated, (long)sizeBytes);
            LogAllocated(_logger, (ulong)sizeBytes, actualStream, dptr);

            return Task.FromResult(dptr);
        }

        /// <summary>
        /// Frees a device pointer on the supplied stream.
        /// </summary>
        /// <param name="ptr">Device pointer returned by <see cref="AllocAsync"/>.</param>
        /// <param name="stream">
        /// Stream on which the free is ordered. Pass <see cref="IntPtr.Zero"/> to use
        /// <see cref="PreferredStream"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token — observed before issuing the free.</param>
        /// <exception cref="ArgumentException"><paramref name="ptr"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
        /// <exception cref="CudaException">The CUDA driver returned an error.</exception>
        public Task FreeAsync(IntPtr ptr, IntPtr stream = default, CancellationToken cancellationToken = default)
        {
            if (ptr == IntPtr.Zero)
            {
                throw new ArgumentException("Device pointer must not be null.", nameof(ptr));
            }

            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            cancellationToken.ThrowIfCancellationRequested();

            var actualStream = stream == IntPtr.Zero ? PreferredStream : stream;

            var err = CudaRuntime.cuMemFreeAsync(ptr, actualStream);
            if (err != CudaError.Success)
            {
                throw new CudaException(
                    string.Format(CultureInfo.InvariantCulture,
                        "cuMemFreeAsync(0x{0:X}) failed on stream 0x{1:X}.",
                        (long)ptr, (long)actualStream),
                    err);
            }

            if (_outstanding.TryRemove(ptr, out var size))
            {
                Interlocked.Add(ref _totalAllocated, -(long)size);
            }

            LogFreed(_logger, ptr, actualStream);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes the pool. Any outstanding allocations tracked by this wrapper are freed on
        /// the preferred stream; callers should normally free pointers explicitly before dispose.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (!_outstanding.IsEmpty)
            {
                LogOutstandingOnDispose(_logger, _outstanding.Count, (ulong)Interlocked.Read(ref _totalAllocated));
                foreach (var kvp in _outstanding)
                {
                    var err = CudaRuntime.cuMemFreeAsync(kvp.Key, PreferredStream);
                    if (err != CudaError.Success)
                    {
                        LogDisposeFreeFailure(_logger, kvp.Key, err);
                    }
                }
                _outstanding.Clear();
                Interlocked.Exchange(ref _totalAllocated, 0);
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>Finaliser — guards against leaked GPU memory if <see cref="Dispose"/> is missed.</summary>
        ~AsyncMemoryPool()
        {
            Dispose();
        }
    }
}
