// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DotCompute.Backends.CUDA.Logging;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Manages pinned (page-locked) host memory for high-bandwidth transfers.
    /// Pinned memory provides up to 10x bandwidth improvement (20GB/s vs 2GB/s).
    /// </summary>
    public sealed partial class CudaPinnedMemoryAllocator : IDisposable
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 6859,
            Level = LogLevel.Debug,
            Message = "Allocated {Size:N0} bytes of pinned memory at {HostPtr:X}, device ptr: {DevicePtr:X}")]
        private static partial void LogAllocatedPinnedMemory(ILogger logger, long size, IntPtr hostPtr, IntPtr devicePtr);

        #endregion

        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<IntPtr, PinnedAllocation> _allocations;
        private readonly SemaphoreSlim _allocationSemaphore;
        private long _totalAllocated;
        private readonly long _maxPinnedMemory;
        private bool _disposed;

        // Constants for pinned memory management
        private const long DEFAULT_MAX_PINNED_MEMORY = 4L * 1024 * 1024 * 1024; // 4GB default
        private const int ALLOCATION_ALIGNMENT = 256; // Align to 256 bytes for optimal performance
        /// <summary>
        /// Initializes a new instance of the CudaPinnedMemoryAllocator class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="maxPinnedMemory">The max pinned memory.</param>

        public CudaPinnedMemoryAllocator(CudaContext context, ILogger logger, long maxPinnedMemory = DEFAULT_MAX_PINNED_MEMORY)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _allocations = new ConcurrentDictionary<IntPtr, PinnedAllocation>();
            _allocationSemaphore = new SemaphoreSlim(1, 1);
            _maxPinnedMemory = maxPinnedMemory;


            _logger.LogInfoMessage("Initialized pinned memory allocator with {_maxPinnedMemory} bytes limit");
        }

        /// <summary>
        /// Gets the total amount of pinned memory currently allocated.
        /// </summary>
        public long TotalAllocated => _totalAllocated;

        /// <summary>
        /// Gets the maximum amount of pinned memory that can be allocated.
        /// </summary>
        public long MaxPinnedMemory => _maxPinnedMemory;

        /// <summary>
        /// Gets the available pinned memory.
        /// </summary>
        public long AvailablePinnedMemory => _maxPinnedMemory - _totalAllocated;

        /// <summary>
        /// Allocates pinned host memory for high-bandwidth transfers.
        /// </summary>
        public async Task<IPinnedMemoryBuffer<T>> AllocatePinnedAsync<T>(
            long count,

            CudaHostAllocFlags flags = CudaHostAllocFlags.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            // Align size to ALLOCATION_ALIGNMENT

            var alignedSize = (sizeInBytes + ALLOCATION_ALIGNMENT - 1) & ~(ALLOCATION_ALIGNMENT - 1);

            await _allocationSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Check if we have enough space
                if (_totalAllocated + alignedSize > _maxPinnedMemory)
                {
                    throw new InvalidOperationException(
                        $"Cannot allocate {alignedSize:N0} bytes of pinned memory. " +
                        $"Current: {_totalAllocated:N0}, Max: {_maxPinnedMemory:N0}");
                }

                // Allocate pinned memory
                var hostPtr = IntPtr.Zero;
                var result = CudaRuntime.cudaHostAlloc(ref hostPtr, (nuint)alignedSize, (uint)flags);
                CudaRuntime.CheckError(result, "allocating pinned memory");

                // Get device pointer if mapped
                var devicePtr = IntPtr.Zero;
                if ((flags & CudaHostAllocFlags.Mapped) != 0)
                {
                    result = CudaRuntime.cudaHostGetDevicePointer(ref devicePtr, hostPtr, 0);
                    CudaRuntime.CheckError(result, "getting device pointer for mapped memory");
                }

                var allocation = new PinnedAllocation(hostPtr, devicePtr, alignedSize, flags);
                _allocations[hostPtr] = allocation;
                _ = Interlocked.Add(ref _totalAllocated, alignedSize);

                LogAllocatedPinnedMemory(_logger, alignedSize, hostPtr, devicePtr);

                return new CudaPinnedMemoryBuffer<T>(this, hostPtr, devicePtr, count);
            }
            finally
            {
                _ = _allocationSemaphore.Release();
            }
        }

        /// <summary>
        /// Allocates write-combined pinned memory for optimal GPU write performance.
        /// </summary>
        public Task<IPinnedMemoryBuffer<T>> AllocateWriteCombinedAsync<T>(long count, CancellationToken cancellationToken = default) where T : unmanaged => AllocatePinnedAsync<T>(count, CudaHostAllocFlags.WriteCombined, cancellationToken);

        /// <summary>
        /// Allocates mapped pinned memory accessible from both host and device.
        /// </summary>
        public Task<IPinnedMemoryBuffer<T>> AllocateMappedAsync<T>(long count, CancellationToken cancellationToken = default) where T : unmanaged => AllocatePinnedAsync<T>(count, CudaHostAllocFlags.Mapped, cancellationToken);

        /// <summary>
        /// Allocates portable pinned memory accessible from all CUDA contexts.
        /// </summary>
        public Task<IPinnedMemoryBuffer<T>> AllocatePortableAsync<T>(long count, CancellationToken cancellationToken = default) where T : unmanaged => AllocatePinnedAsync<T>(count, CudaHostAllocFlags.Portable, cancellationToken);

        /// <summary>
        /// Frees pinned memory.
        /// </summary>
        internal void FreePinned(IntPtr hostPtr)
        {
            if (_allocations.TryRemove(hostPtr, out var allocation))
            {
                var result = CudaRuntime.cudaFreeHost(hostPtr);
                if (result == CudaError.Success)
                {
                    _ = Interlocked.Add(ref _totalAllocated, -allocation.Size);
                    _logger.LogDebugMessage($"Freed {allocation.Size:N0} bytes of pinned memory at 0x{hostPtr:X}");
                }
                else
                {
                    _logger.LogWarningMessage($"Failed to free pinned memory at 0x{hostPtr:X}: {result}");
                }
            }
        }

        /// <summary>
        /// Registers existing host memory as pinned for improved transfer performance.
        /// </summary>
        public async Task<IPinnedMemoryRegistration> RegisterHostMemoryAsync(
            IntPtr hostPtr,
            long sizeInBytes,
            CudaHostRegisterFlags flags = CudaHostRegisterFlags.None,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaHostRegister(hostPtr, (nuint)sizeInBytes, (uint)flags);
                CudaRuntime.CheckError(result, "registering host memory");
            }, cancellationToken);

            _logger.LogDebugMessage($"Registered {sizeInBytes:N0} bytes of host memory at 0x{hostPtr:X}");


            return new PinnedMemoryRegistration(hostPtr, sizeInBytes, this);
        }

        /// <summary>
        /// Unregisters previously registered host memory.
        /// </summary>
        internal void UnregisterHostMemory(IntPtr hostPtr)
        {
            var result = CudaRuntime.cudaHostUnregister(hostPtr);
            if (result != CudaError.Success)
            {
                _logger.LogWarningMessage("");
            }
        }

        /// <summary>
        /// Gets memory allocation statistics.
        /// </summary>
        public PinnedMemoryStatistics Statistics => new PinnedMemoryStatistics
        {
            TotalAllocated = _totalAllocated,
            MaxAllowed = _maxPinnedMemory,
            Available = _maxPinnedMemory - _totalAllocated,
            AllocationCount = _allocations.Count,
            AverageAllocationSize = !_allocations.IsEmpty ? _totalAllocated / _allocations.Count : 0
        };
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Free all allocations

            foreach (var allocation in _allocations.Keys)
            {
                FreePinned(allocation);
            }

            _allocations.Clear();
            _allocationSemaphore?.Dispose();
            _disposed = true;

            _logger.LogInfoMessage("Disposed pinned memory allocator");
        }
        /// <summary>
        /// A class that represents pinned allocation.
        /// </summary>

        private sealed class PinnedAllocation(IntPtr hostPointer, IntPtr devicePointer, long size, CudaHostAllocFlags flags)
        {
            /// <summary>
            /// Gets or sets the host pointer.
            /// </summary>
            /// <value>The host pointer.</value>
            public IntPtr HostPointer { get; } = hostPointer;
            /// <summary>
            /// Gets or sets the device pointer.
            /// </summary>
            /// <value>The device pointer.</value>
            public IntPtr DevicePointer { get; } = devicePointer;
            /// <summary>
            /// Gets or sets the size.
            /// </summary>
            /// <value>The size.</value>
            public long Size { get; } = size;
            /// <summary>
            /// Gets or sets the flags.
            /// </summary>
            /// <value>The flags.</value>
            public CudaHostAllocFlags Flags { get; } = flags;
        }
    }

    /// <summary>
    /// Flags for pinned memory allocation.
    /// </summary>
    [Flags]
    public enum CudaHostAllocFlags
    {
        /// <summary>
        /// No special allocation flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Allocates write-combined memory for improved GPU write performance.
        /// </summary>
        WriteCombined = 1,

        /// <summary>
        /// Maps allocation to device address space.
        /// </summary>
        Mapped = 2,

        /// <summary>
        /// Makes allocation portable across CUDA contexts.
        /// </summary>
        Portable = 4
    }

    /// <summary>
    /// Flags for host memory registration.
    /// </summary>
    [Flags]
    public enum CudaHostRegisterFlags
    {
        /// <summary>
        /// No special registration flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Maps registered memory to device address space.
        /// </summary>
        Mapped = 1,

        /// <summary>
        /// Makes registered memory portable across CUDA contexts.
        /// </summary>
        Portable = 2
    }

    /// <summary>
    /// Interface for pinned memory buffers.
    /// </summary>
    public interface IPinnedMemoryBuffer<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the host pointer.
        /// </summary>
        /// <value>The host pointer.</value>
        public IntPtr HostPointer { get; }
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>
        public IntPtr DevicePointer { get; }
        /// <summary>
        /// Gets or sets the count.
        /// </summary>
        /// <value>The count.</value>
        public long Count { get; }
        /// <summary>
        /// Gets as span.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public Span<T> AsSpan();
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public Memory<T> AsMemory();
        /// <summary>
        /// Gets copy to device asynchronously.
        /// </summary>
        /// <param name="devicePtr">The device ptr.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public Task CopyToDeviceAsync(IntPtr devicePtr, CancellationToken cancellationToken = default);
        /// <summary>
        /// Gets copy from device asynchronously.
        /// </summary>
        /// <param name="devicePtr">The device ptr.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public Task CopyFromDeviceAsync(IntPtr devicePtr, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for pinned memory registration.
    /// </summary>
    public interface IPinnedMemoryRegistration : IDisposable
    {
        /// <summary>
        /// Gets or sets the host pointer.
        /// </summary>
        /// <value>The host pointer.</value>
        public IntPtr HostPointer { get; }
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        public long Size { get; }
    }

    /// <summary>
    /// Implementation of pinned memory buffer.
    /// </summary>
    internal sealed class CudaPinnedMemoryBuffer<T>(
        CudaPinnedMemoryAllocator allocator,
        IntPtr hostPointer,
        IntPtr devicePointer,
        long count) : IPinnedMemoryBuffer<T> where T : unmanaged
    {
        [SuppressMessage("IDisposableAnalyzers.Correctness", "CA2213:Disposable fields should be disposed",
            Justification = "Handle class does not own the allocator - memory is returned to allocator via FreePinned call")]
        private readonly CudaPinnedMemoryAllocator _allocator = allocator;
        private bool _disposed;
        /// <summary>
        /// Gets or sets the host pointer.
        /// </summary>
        /// <value>The host pointer.</value>

        public IntPtr HostPointer { get; } = hostPointer;
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>
        public IntPtr DevicePointer { get; } = devicePointer;
        /// <summary>
        /// Gets or sets the count.
        /// </summary>
        /// <value>The count.</value>
        public long Count { get; } = count;
        /// <summary>
        /// Gets as span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public unsafe Span<T> AsSpan()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new Span<T>(HostPointer.ToPointer(), (int)Count);
        }
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public unsafe Memory<T> AsMemory()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Create a custom MemoryManager that wraps the pinned memory

            return new PinnedMemoryManager<T>(HostPointer, (int)Count, this).Memory;
        }
        /// <summary>
        /// Gets copy to device asynchronously.
        /// </summary>
        /// <param name="devicePtr">The device ptr.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async Task CopyToDeviceAsync(IntPtr devicePtr, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await Task.Run(() =>
            {
                var sizeInBytes = Count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                var result = CudaRuntime.cudaMemcpy(
                    devicePtr,
                    HostPointer,
                    (nuint)sizeInBytes,
                    CudaMemcpyKind.HostToDevice);
                CudaRuntime.CheckError(result, "copying pinned memory to device");
            }, cancellationToken);
        }
        /// <summary>
        /// Gets copy from device asynchronously.
        /// </summary>
        /// <param name="devicePtr">The device ptr.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async Task CopyFromDeviceAsync(IntPtr devicePtr, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await Task.Run(() =>
            {
                var sizeInBytes = Count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                var result = CudaRuntime.cudaMemcpy(
                    HostPointer,
                    devicePtr,
                    (nuint)sizeInBytes,
                    CudaMemcpyKind.DeviceToHost);
                CudaRuntime.CheckError(result, "copying device memory to pinned");
            }, cancellationToken);
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _allocator.FreePinned(HostPointer);
            _disposed = true;
        }
    }

    /// <summary>
    /// Implementation of pinned memory registration.
    /// </summary>
    internal sealed class PinnedMemoryRegistration(IntPtr hostPointer, long size, CudaPinnedMemoryAllocator allocator) : IPinnedMemoryRegistration
    {
        [SuppressMessage("IDisposableAnalyzers.Correctness", "CA2213:Disposable fields should be disposed",
            Justification = "Registration class does not own the allocator - memory is unregistered via UnregisterHostMemory call")]
        private readonly CudaPinnedMemoryAllocator _allocator = allocator;
        private bool _disposed;
        /// <summary>
        /// Gets or sets the host pointer.
        /// </summary>
        /// <value>The host pointer.</value>

        public IntPtr HostPointer { get; } = hostPointer;
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        public long Size { get; } = size;
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _allocator.UnregisterHostMemory(HostPointer);
            _disposed = true;
        }
    }

    /// <summary>
    /// Statistics for pinned memory usage.
    /// </summary>
    public sealed class PinnedMemoryStatistics
    {
        /// <summary>
        /// Gets or sets the total allocated.
        /// </summary>
        /// <value>The total allocated.</value>
        public long TotalAllocated { get; init; }
        /// <summary>
        /// Gets or sets the max allowed.
        /// </summary>
        /// <value>The max allowed.</value>
        public long MaxAllowed { get; init; }
        /// <summary>
        /// Gets or sets the available.
        /// </summary>
        /// <value>The available.</value>
        public long Available { get; init; }
        /// <summary>
        /// Gets or sets the allocation count.
        /// </summary>
        /// <value>The allocation count.</value>
        public int AllocationCount { get; init; }
        /// <summary>
        /// Gets or sets the average allocation size.
        /// </summary>
        /// <value>The average allocation size.</value>
        public long AverageAllocationSize { get; init; }
    }
}
