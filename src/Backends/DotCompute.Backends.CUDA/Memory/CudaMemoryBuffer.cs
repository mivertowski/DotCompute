// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Represents a CUDA memory buffer allocated on the GPU device.
    /// </summary>
    public sealed class CudaMemoryBuffer : IUnifiedMemoryBuffer, IDisposable
    {
        private readonly CudaDevice _device;
        private readonly nint _devicePointer;
        private readonly long _sizeInBytes;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaMemoryBuffer"/> class.
        /// </summary>
        /// <param name="device">The CUDA device.</param>
        /// <param name="devicePointer">The device memory pointer.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        /// <param name="options">Memory allocation options.</param>
        public CudaMemoryBuffer(CudaDevice device, nint devicePointer, long sizeInBytes, MemoryOptions options = MemoryOptions.None)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _devicePointer = devicePointer;
            _sizeInBytes = sizeInBytes;
            Options = options;
        }

        /// <summary>
        /// Gets the device memory pointer.
        /// </summary>
        public nint DevicePointer => _devicePointer;

        /// <inheritdoc/>
        public long SizeInBytes => _sizeInBytes;

        /// <inheritdoc/>
        public BufferState State => _disposed ? BufferState.Disposed : BufferState.Allocated;


        /// <inheritdoc/>
        public MemoryOptions Options { get; }


        /// <inheritdoc/>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Copies data from this buffer to another buffer.
        /// </summary>
        public unsafe void CopyTo(CudaMemoryBuffer destination)
        {
            ArgumentNullException.ThrowIfNull(destination);


            if (destination.SizeInBytes < SizeInBytes)
            {

                throw new ArgumentException("Destination buffer is too small.", nameof(destination));
            }

            // Use CUDA runtime to copy

            _ = CudaRuntime.cudaMemcpy(destination.DevicePointer, DevicePointer,

                (nuint)SizeInBytes, CudaMemcpyKind.DeviceToDevice);
        }

        /// <summary>
        /// Copies data from host memory to this device buffer.
        /// </summary>
        public unsafe void CopyFromHost<T>(ReadOnlySpan<T> source) where T : unmanaged
        {
            var bytesToCopy = source.Length * sizeof(T);
            if (bytesToCopy > SizeInBytes)
            {

                throw new ArgumentException("Source data exceeds buffer size.", nameof(source));
            }


            fixed (T* ptr = source)
            {
                _ = CudaRuntime.cudaMemcpy(DevicePointer, (nint)ptr,

                    (nuint)bytesToCopy, CudaMemcpyKind.HostToDevice);
            }
        }

        /// <summary>
        /// Copies data from this device buffer to host memory.
        /// </summary>
        public unsafe void CopyToHost<T>(Span<T> destination) where T : unmanaged
        {
            var bytesToCopy = destination.Length * sizeof(T);
            if (bytesToCopy > SizeInBytes)
            {

                throw new ArgumentException("Destination span is larger than buffer.", nameof(destination));
            }


            fixed (T* ptr = destination)
            {
                _ = CudaRuntime.cudaMemcpy((nint)ptr, DevicePointer,

                    (nuint)bytesToCopy, CudaMemcpyKind.DeviceToHost);
            }
        }

        /// <summary>
        /// Allocates a new CUDA memory buffer.
        /// </summary>
        public static CudaMemoryBuffer Allocate(CudaDevice device, long sizeInBytes, MemoryOptions options = MemoryOptions.None)
        {
            var ptr = CudaRuntime.cudaMalloc((nuint)sizeInBytes);
            return new CudaMemoryBuffer(device, ptr, sizeInBytes, options);
        }

        /// <summary>
        /// Asynchronously copies data from host memory to this buffer.
        /// </summary>
        public async ValueTask CopyFromAsync<T>(
            ReadOnlyMemory<T> source,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            await Task.Run(() =>
            {
                var sourceSpan = source.Span;
                var bytesToCopy = sourceSpan.Length * Unsafe.SizeOf<T>();


                if (offset + bytesToCopy > SizeInBytes)
                {

                    throw new ArgumentException("Source data exceeds buffer capacity.");
                }


                unsafe
                {
                    fixed (T* ptr = sourceSpan)
                    {
                        _ = CudaRuntime.cudaMemcpy(_devicePointer + (nint)offset, (nint)ptr,

                            (nuint)bytesToCopy, CudaMemcpyKind.HostToDevice);
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously copies data from this buffer to host memory.
        /// </summary>
        public async ValueTask CopyToAsync<T>(
            Memory<T> destination,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            await Task.Run(() =>
            {
                var destinationSpan = destination.Span;
                var bytesToCopy = destinationSpan.Length * Unsafe.SizeOf<T>();


                if (offset + bytesToCopy > SizeInBytes)
                {

                    throw new ArgumentException("Destination exceeds buffer capacity.");
                }


                unsafe
                {
                    fixed (T* ptr = destinationSpan)
                    {
                        _ = CudaRuntime.cudaMemcpy((nint)ptr, _devicePointer + (nint)offset,

                            (nuint)bytesToCopy, CudaMemcpyKind.DeviceToHost);
                    }
                }
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_devicePointer != nint.Zero)
                {
                    _ = CudaRuntime.cudaFree(_devicePointer);
                }
                _disposed = true;
            }
        }


        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Represents a generic CUDA memory buffer allocated on the GPU device.
    /// </summary>
    /// <typeparam name="T">The element type stored in the buffer.</typeparam>
    public sealed class CudaMemoryBuffer<T> : IUnifiedMemoryBuffer<T>, IDisposable where T : unmanaged
    {
        private readonly CudaContext _context;
        private readonly nint _devicePointer;
        private readonly long _count;
        private readonly long _sizeInBytes;
        private bool _disposed;
        private bool _isDirty;
        private readonly HashSet<(long start, long end)> _dirtyRanges;
        private readonly object _dirtyLock = new object();
        private readonly WeakReference<IAccelerator>? _acceleratorRef;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaMemoryBuffer{T}"/> class.
        /// </summary>
        /// <param name="devicePointer">The device memory pointer.</param>
        /// <param name="count">The number of elements.</param>
        /// <param name="context">The CUDA context.</param>
        /// <param name="options">Memory allocation options.</param>
        /// <param name="accelerator">Optional accelerator reference for advanced features.</param>
        public CudaMemoryBuffer(nint devicePointer, long count, CudaContext context, MemoryOptions options = MemoryOptions.None, IAccelerator? accelerator = null)
        {
            _devicePointer = devicePointer;
            _count = count;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            Options = options;
            _isDirty = false;
            _dirtyRanges = [];
            _acceleratorRef = accelerator != null ? new WeakReference<IAccelerator>(accelerator) : null;
        }

        /// <summary>
        /// Gets the device memory pointer.
        /// </summary>
        public nint DevicePointer => _devicePointer;

        /// <inheritdoc/>
        public long Count => _count;

        /// <inheritdoc/>
        public int Length => (int)_count;

        /// <inheritdoc/>
        public long SizeInBytes => _sizeInBytes;

        /// <inheritdoc/>
        public BufferState State => _disposed ? BufferState.Disposed : BufferState.Allocated;


        /// <inheritdoc/>
        public MemoryOptions Options { get; }


        /// <inheritdoc/>
        public bool IsDisposed => _disposed;

        /// <inheritdoc/>
        public IAccelerator Accelerator
        {
            get
            {
                if (_acceleratorRef?.TryGetTarget(out var accelerator) == true)
                {

                    return accelerator;
                }


                throw new InvalidOperationException("Accelerator reference is no longer available. Buffer may have been created without accelerator context.");
            }
        }

        /// <inheritdoc/>
        public bool IsOnHost => false; // CUDA buffers are primarily on device

        /// <inheritdoc/>
        public bool IsOnDevice => true; // CUDA buffers are on device

        /// <inheritdoc/>
        public bool IsDirty
        {
            get
            {
                lock (_dirtyLock)
                {
                    return _isDirty;
                }
            }
        }

        /// <summary>
        /// Copies data from this buffer to another buffer.
        /// </summary>
        public unsafe void CopyTo(CudaMemoryBuffer<T> destination)
        {
            ArgumentNullException.ThrowIfNull(destination);

            if (destination.Length < Count)
            {

                throw new ArgumentException("Destination buffer is too small.", nameof(destination));
            }

            // Use CUDA runtime to copy

            _context.MakeCurrent();
            var result = CudaRuntime.cudaMemcpy(destination.DevicePointer, DevicePointer,

                (nuint)SizeInBytes, CudaMemcpyKind.DeviceToDevice);
            CudaRuntime.CheckError(result, "copying between device buffers");
        }

        /// <summary>
        /// Copies data from host memory to this device buffer.
        /// </summary>
        public unsafe void CopyFromHost(ReadOnlySpan<T> source)
        {
            var elementsTooCopy = Math.Min(source.Length, Count);
            var bytesToCopy = elementsTooCopy * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            _context.MakeCurrent();
            fixed (T* ptr = source)
            {
                var result = CudaRuntime.cudaMemcpy(DevicePointer, (nint)ptr,

                    (nuint)bytesToCopy, CudaMemcpyKind.HostToDevice);
                CudaRuntime.CheckError(result, "copying from host to device");
            }
        }

        /// <summary>
        /// Copies data from this device buffer to host memory.
        /// </summary>
        public unsafe void CopyToHost(Span<T> destination)
        {
            var elementsToCopy = Math.Min(destination.Length, Count);
            var bytesToCopy = elementsToCopy * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            _context.MakeCurrent();
            fixed (T* ptr = destination)
            {
                var result = CudaRuntime.cudaMemcpy((nint)ptr, DevicePointer,

                    (nuint)bytesToCopy, CudaMemcpyKind.DeviceToHost);
                CudaRuntime.CheckError(result, "copying from device to host");
            }
        }

        /// <summary>
        /// Asynchronously copies data from host memory to this buffer.
        /// </summary>
        public async ValueTask CopyFromAsync<TSource>(
            ReadOnlyMemory<TSource> source,
            long offset = 0,
            CancellationToken cancellationToken = default) where TSource : unmanaged
        {
            if (typeof(TSource) != typeof(T))
            {

                throw new ArgumentException($"Source type {typeof(TSource)} does not match buffer type {typeof(T)}");
            }


            await Task.Run(() =>
            {
                var sourceSpan = source.Span;
                var elementsToCopy = Math.Min(sourceSpan.Length, Count - offset);
                var bytesToCopy = elementsToCopy * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();


                if (offset + elementsToCopy > Count)
                {

                    throw new ArgumentException("Source data exceeds buffer capacity.");
                }


                _context.MakeCurrent();
                unsafe
                {
                    fixed (TSource* ptr = sourceSpan)
                    {
                        var result = CudaRuntime.cudaMemcpy(_devicePointer + (nint)(offset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>()),

                            (nint)ptr, (nuint)bytesToCopy, CudaMemcpyKind.HostToDevice);
                        CudaRuntime.CheckError(result, "async copying from host to device");
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously copies data from this buffer to host memory.
        /// </summary>
        public async ValueTask CopyToAsync<TDest>(
            Memory<TDest> destination,
            long offset = 0,
            CancellationToken cancellationToken = default) where TDest : unmanaged
        {
            if (typeof(TDest) != typeof(T))
            {

                throw new ArgumentException($"Destination type {typeof(TDest)} does not match buffer type {typeof(T)}");
            }


            await Task.Run(() =>
            {
                var destinationSpan = destination.Span;
                var elementsToCopy = Math.Min(destinationSpan.Length, Count - offset);
                var bytesToCopy = elementsToCopy * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();


                if (offset + elementsToCopy > Count)
                {

                    throw new ArgumentException("Destination exceeds buffer capacity.");
                }


                _context.MakeCurrent();
                unsafe
                {
                    fixed (TDest* ptr = destinationSpan)
                    {
                        var result = CudaRuntime.cudaMemcpy((nint)ptr,

                            _devicePointer + (nint)(offset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>()),

                            (nuint)bytesToCopy, CudaMemcpyKind.DeviceToHost);
                        CudaRuntime.CheckError(result, "async copying from device to host");
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Copies a range of data from this buffer to another device buffer.
        /// </summary>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="sourceOffset">The offset in the source buffer (in elements).</param>
        /// <param name="destinationOffset">The offset in the destination buffer (in elements).</param>
        /// <param name="count">The number of elements to copy.</param>
        public unsafe void CopyRangeTo(CudaMemoryBuffer<T> destination, long sourceOffset, long destinationOffset, long count)
        {
            ArgumentNullException.ThrowIfNull(destination);


            if (sourceOffset < 0 || sourceOffset >= Count)
            {

                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }


            if (destinationOffset < 0 || destinationOffset >= destination.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(destinationOffset));
            }


            if (count < 0 || sourceOffset + count > Count)
            {

                throw new ArgumentOutOfRangeException(nameof(count));
            }


            if (destinationOffset + count > destination.Length)
            {

                throw new ArgumentException("Destination buffer is too small for the specified range.");
            }


            var bytesToCopy = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var sourcePtr = _devicePointer + (nint)(sourceOffset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
            var destPtr = destination.DevicePointer + (nint)(destinationOffset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());


            _context.MakeCurrent();
            var result = CudaRuntime.cudaMemcpy(destPtr, sourcePtr, (nuint)bytesToCopy, CudaMemcpyKind.DeviceToDevice);
            CudaRuntime.CheckError(result, "copying range between device buffers");

            // Mark destination buffer as dirty

            destination.MarkRangeDirty(destinationOffset, destinationOffset + count);
        }


        /// <summary>
        /// Asynchronously copies a range of data from this buffer to another device buffer.
        /// </summary>
        public async ValueTask CopyRangeToAsync(
            CudaMemoryBuffer<T> destination,

            long sourceOffset,

            long destinationOffset,

            long count,
            IntPtr stream = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);


            if (sourceOffset < 0 || sourceOffset >= Count)
            {

                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }


            if (destinationOffset < 0 || destinationOffset >= destination.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(destinationOffset));
            }


            if (count < 0 || sourceOffset + count > Count)
            {

                throw new ArgumentOutOfRangeException(nameof(count));
            }


            if (destinationOffset + count > destination.Length)
            {

                throw new ArgumentException("Destination buffer is too small for the specified range.");
            }


            await Task.Run(() =>
            {
                var bytesToCopy = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                var sourcePtr = _devicePointer + (nint)(sourceOffset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
                var destPtr = destination.DevicePointer + (nint)(destinationOffset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());


                _context.MakeCurrent();
                var result = stream == IntPtr.Zero

                    ? CudaRuntime.cudaMemcpy(destPtr, sourcePtr, (nuint)bytesToCopy, CudaMemcpyKind.DeviceToDevice)
                    : CudaRuntime.cudaMemcpyAsync(destPtr, sourcePtr, (nuint)bytesToCopy, CudaMemcpyKind.DeviceToDevice, stream);
                CudaRuntime.CheckError(result, "async copying range between device buffers");


                if (stream != IntPtr.Zero)
                {
                    result = CudaRuntime.cudaStreamSynchronize(stream);
                    CudaRuntime.CheckError(result, "synchronizing stream after async copy");
                }

                // Mark destination buffer as dirty

                destination.MarkRangeDirty(destinationOffset, destinationOffset + count);
            }, cancellationToken);
        }


        /// <summary>
        /// Marks the entire buffer as dirty.
        /// </summary>
        public void MarkDirty()
        {
            lock (_dirtyLock)
            {
                _isDirty = true;
                _dirtyRanges.Clear();
                _ = _dirtyRanges.Add((0, Count));
            }
        }


        /// <summary>
        /// Marks a specific range of the buffer as dirty.
        /// </summary>
        /// <param name="start">The start index (inclusive).</param>
        /// <param name="end">The end index (exclusive).</param>
        public void MarkRangeDirty(long start, long end)
        {
            if (start < 0 || start >= Count)
            {

                throw new ArgumentOutOfRangeException(nameof(start));
            }


            if (end <= start || end > Count)
            {

                throw new ArgumentOutOfRangeException(nameof(end));
            }


            lock (_dirtyLock)
            {
                _isDirty = true;

                // Merge overlapping ranges

                var newRange = (start, end);
                var overlapping = _dirtyRanges.Where(r => r.start <= end && r.end >= start).ToList();


                if (overlapping.Any())
                {
                    foreach (var range in overlapping)
                    {
                        _ = _dirtyRanges.Remove(range);
                    }


                    var mergedStart = Math.Min(start, overlapping.Min(r => r.start));
                    var mergedEnd = Math.Max(end, overlapping.Max(r => r.end));
                    _ = _dirtyRanges.Add((mergedStart, mergedEnd));
                }
                else
                {
                    _ = _dirtyRanges.Add(newRange);
                }
            }
        }


        /// <summary>
        /// Clears the dirty flag and all dirty ranges.
        /// </summary>
        public void ClearDirty()
        {
            lock (_dirtyLock)
            {
                _isDirty = false;
                _dirtyRanges.Clear();
            }
        }


        /// <summary>
        /// Gets the dirty ranges in the buffer.
        /// </summary>
        /// <returns>An array of dirty ranges (start, end) where end is exclusive.</returns>
        public (long start, long end)[] GetDirtyRanges()
        {
            lock (_dirtyLock)
            {
                return _dirtyRanges.ToArray();
            }
        }


        /// <summary>
        /// Synchronizes only the dirty ranges from device to host.
        /// </summary>
        public async ValueTask SyncDirtyToHostAsync<TDest>(
            Memory<TDest> destination,
            CancellationToken cancellationToken = default) where TDest : unmanaged
        {
            if (typeof(TDest) != typeof(T))
            {

                throw new ArgumentException($"Destination type {typeof(TDest)} does not match buffer type {typeof(T)}");
            }


            var dirtyRanges = GetDirtyRanges();
            if (!dirtyRanges.Any())
            {
                return;
            }


            await Task.Run(() =>
            {
                _context.MakeCurrent();
                var destSpan = destination.Span;


                foreach (var (start, end) in dirtyRanges)
                {
                    var count = end - start;
                    var bytesToCopy = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                    var sourcePtr = _devicePointer + (nint)(start * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());


                    if (start + count > destSpan.Length)
                    {

                        throw new ArgumentException($"Destination buffer too small for dirty range [{start}, {end})");
                    }


                    unsafe
                    {
                        fixed (TDest* ptr = destSpan.Slice((int)start, (int)count))
                        {
                            var result = CudaRuntime.cudaMemcpy((nint)ptr, sourcePtr, (nuint)bytesToCopy, CudaMemcpyKind.DeviceToHost);
                            CudaRuntime.CheckError(result, $"syncing dirty range [{start}, {end}) from device to host");
                        }
                    }
                }


                ClearDirty();
            }, cancellationToken);
        }

        // Host Memory Access
        /// <inheritdoc/>
        public Span<T> AsSpan() => throw new NotSupportedException("Direct span access to CUDA device memory is not supported. Use CopyToHost instead.");

        /// <inheritdoc/>
        public ReadOnlySpan<T> AsReadOnlySpan() => throw new NotSupportedException("Direct span access to CUDA device memory is not supported. Use CopyToHost instead.");

        /// <inheritdoc/>
        public Memory<T> AsMemory() => throw new NotSupportedException("Direct memory access to CUDA device memory is not supported. Use CopyToHost instead.");

        /// <inheritdoc/>
        public ReadOnlyMemory<T> AsReadOnlyMemory() => throw new NotSupportedException("Direct memory access to CUDA device memory is not supported. Use CopyToHost instead.");

        // Device Memory Access
        /// <inheritdoc/>
        public DeviceMemory GetDeviceMemory() => new(_devicePointer, _sizeInBytes);

        // Memory Mapping
        /// <inheritdoc/>
        public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => throw new NotSupportedException("Memory mapping is not supported for CUDA buffers. Use explicit copy operations instead.");

        /// <inheritdoc/>
        public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => throw new NotSupportedException("Memory mapping is not supported for CUDA buffers. Use explicit copy operations instead.");

        /// <inheritdoc/>
        public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => throw new NotSupportedException("Memory mapping is not supported for CUDA buffers. Use explicit copy operations instead.");

        // Synchronization
        /// <inheritdoc/>
        public void EnsureOnHost()
            // CUDA buffers are device-only, so this would require explicit copy



            => throw new NotSupportedException("CUDA buffers are device-only. Use CopyToHost for host access.");

        /// <inheritdoc/>
        public void EnsureOnDevice()
        {
            // Already on device, no-op
        }

        /// <inheritdoc/>
        public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => throw new NotSupportedException("CUDA buffers are device-only. Use CopyToAsync for host access.");

        /// <inheritdoc/>
        public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
            // Already on device



            => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public void Synchronize() => _context.Synchronize();

        /// <inheritdoc/>
        public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => new(Task.Run(_context.Synchronize, cancellationToken));

        /// <inheritdoc/>
        public void MarkHostDirty()
        {
            // No-op for CUDA device-only buffers
        }

        /// <inheritdoc/>
        public void MarkDeviceDirty()
        {
            // No-op for CUDA device-only buffers  
        }

        // Copy Operations (interface-required overloads)
        /// <inheritdoc/>
        public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => CopyFromAsync(source, 0, cancellationToken);

        /// <inheritdoc/>
        public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => CopyToAsync(destination, 0, cancellationToken);

        /// <inheritdoc/>
        public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        {
            if (destination is CudaMemoryBuffer<T> cudaDestination)
            {
                return new ValueTask(Task.Run(() => CopyTo(cudaDestination), cancellationToken));
            }
            throw new NotSupportedException($"Copy to {destination.GetType()} not supported directly. Use intermediate host memory.");
        }

        /// <inheritdoc/>
        public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);

            if (sourceOffset < 0 || sourceOffset >= Count)
            {

                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }


            if (destinationOffset < 0 || destinationOffset >= destination.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(destinationOffset));
            }


            if (count < 0 || sourceOffset + count > Count || destinationOffset + count > destination.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(count));
            }


            return new ValueTask(Task.Run(() =>
            {
                _context.MakeCurrent();
                var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                var sourceBytes = sourceOffset * elementSize;
                var destBytes = destinationOffset * elementSize;
                var copyBytes = count * elementSize;

                if (destination is CudaMemoryBuffer<T> cudaDestination)
                {
                    // CUDA to CUDA copy
                    var result = CudaRuntime.cudaMemcpy(
                        cudaDestination.DevicePointer + destBytes,
                        _devicePointer + sourceBytes,
                        (nuint)copyBytes,
                        CudaMemcpyKind.DeviceToDevice);
                    CudaRuntime.CheckError(result, "Failed to copy between CUDA buffers");
                }
                else
                {
                    throw new NotSupportedException($"Ranged copy to {destination.GetType()} not supported. Use host memory intermediary.");
                }
            }, cancellationToken));
        }

        // Fill Operations
        /// <inheritdoc/>
        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => FillAsync(value, 0, Length, cancellationToken);

        /// <inheritdoc/>
        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
        {
            return new ValueTask(Task.Run(() =>
            {
                // Use cudaMemset for simple types
                _context.MakeCurrent();
                var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                if (elementSize == 1 && typeof(T) == typeof(byte))
                {
                    var byteValue = System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref value);
                    var result = CudaRuntime.cudaMemset(_devicePointer + offset, byteValue, (nuint)count);
                    CudaRuntime.CheckError(result, "filling buffer with value");
                }
                else
                {
                    throw new NotSupportedException($"Fill operation not supported for type {typeof(T)}. Use byte arrays or implement custom CUDA kernel.");
                }
            }, cancellationToken));
        }

        // View and Slice Operations
        /// <inheritdoc/>
        public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset + length > Length)
            {

                throw new ArgumentOutOfRangeException("Slice parameters are out of range");
            }


            var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var offsetBytes = offset * elementSize;
            return new CudaMemoryBuffer<T>(_devicePointer + offsetBytes, length, _context, Options);
        }

        /// <inheritdoc/>
        public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
        {
            var newElementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
            _ = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();


            if (_sizeInBytes % newElementSize != 0)
            {

                throw new InvalidOperationException($"Buffer size is not aligned for type {typeof(TNew)}");
            }


            var newCount = _sizeInBytes / newElementSize;
            return new CudaMemoryBuffer<TNew>(_devicePointer, newCount, _context, Options);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_devicePointer != nint.Zero)
                {
                    try
                    {
                        _context.MakeCurrent();
                        var result = CudaRuntime.cudaFree(_devicePointer);
                        if (result != CudaError.Success)
                        {
                            // Log but don't throw during disposal
                            System.Diagnostics.Debug.WriteLine($"Failed to free CUDA memory: {result}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Exception during CUDA memory disposal: {ex.Message}");
                    }
                }
                _disposed = true;
            }
        }


        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}