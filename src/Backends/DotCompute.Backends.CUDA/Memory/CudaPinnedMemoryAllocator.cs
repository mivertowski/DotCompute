// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Manages pinned (page-locked) host memory for high-bandwidth transfers.
    /// Pinned memory provides up to 10x bandwidth improvement (20GB/s vs 2GB/s).
    /// </summary>
    public sealed class CudaPinnedMemoryAllocator : IDisposable
    {
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

            CudaHostAllocFlags flags = CudaHostAllocFlags.Default,
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
                    throw new OutOfMemoryException(
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

                _logger.LogDebug(
                    "Allocated {Size:N0} bytes of pinned memory at {HostPtr:X}, device ptr: {DevicePtr:X}",
                    alignedSize, hostPtr, devicePtr);

                return new CudaPinnedMemoryBuffer<T>(this, hostPtr, devicePtr, count, _context, _logger);
            }
            finally
            {
                _ = _allocationSemaphore.Release();
            }
        }

        /// <summary>
        /// Allocates write-combined pinned memory for optimal GPU write performance.
        /// </summary>
        public Task<IPinnedMemoryBuffer<T>> AllocateWriteCombinedAsync<T>(long count, CancellationToken cancellationToken = default) where T : unmanaged
        {
            return AllocatePinnedAsync<T>(count, CudaHostAllocFlags.WriteCombined, cancellationToken);
        }

        /// <summary>
        /// Allocates mapped pinned memory accessible from both host and device.
        /// </summary>
        public Task<IPinnedMemoryBuffer<T>> AllocateMappedAsync<T>(long count, CancellationToken cancellationToken = default) where T : unmanaged
        {
            return AllocatePinnedAsync<T>(count, CudaHostAllocFlags.Mapped, cancellationToken);
        }

        /// <summary>
        /// Allocates portable pinned memory accessible from all CUDA contexts.
        /// </summary>
        public Task<IPinnedMemoryBuffer<T>> AllocatePortableAsync<T>(long count, CancellationToken cancellationToken = default) where T : unmanaged
        {
            return AllocatePinnedAsync<T>(count, CudaHostAllocFlags.Portable, cancellationToken);
        }

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
            CudaHostRegisterFlags flags = CudaHostRegisterFlags.Default,
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
        public PinnedMemoryStatistics GetStatistics()
        {
            return new PinnedMemoryStatistics
            {
                TotalAllocated = _totalAllocated,
                MaxAllowed = _maxPinnedMemory,
                Available = _maxPinnedMemory - _totalAllocated,
                AllocationCount = _allocations.Count,
                AverageAllocationSize = _allocations.Count > 0 ? _totalAllocated / _allocations.Count : 0
            };
        }

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

        private sealed class PinnedAllocation
        {
            public IntPtr HostPointer { get; }
            public IntPtr DevicePointer { get; }
            public long Size { get; }
            public CudaHostAllocFlags Flags { get; }

            public PinnedAllocation(IntPtr hostPointer, IntPtr devicePointer, long size, CudaHostAllocFlags flags)
            {
                HostPointer = hostPointer;
                DevicePointer = devicePointer;
                Size = size;
                Flags = flags;
            }
        }
    }

    /// <summary>
    /// Flags for pinned memory allocation.
    /// </summary>
    [Flags]
    public enum CudaHostAllocFlags
    {
        /// <summary>
        /// Default allocation.
        /// </summary>
        Default = 0,

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
        /// Default registration.
        /// </summary>
        Default = 0,

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
        public IntPtr HostPointer { get; }
        public IntPtr DevicePointer { get; }
        public long Count { get; }
        public Span<T> AsSpan();
        public Memory<T> AsMemory();
        public Task CopyToDeviceAsync(IntPtr devicePtr, CancellationToken cancellationToken = default);
        public Task CopyFromDeviceAsync(IntPtr devicePtr, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for pinned memory registration.
    /// </summary>
    public interface IPinnedMemoryRegistration : IDisposable
    {
        public IntPtr HostPointer { get; }
        public long Size { get; }
    }

    /// <summary>
    /// Implementation of pinned memory buffer.
    /// </summary>
    internal sealed class CudaPinnedMemoryBuffer<T> : IPinnedMemoryBuffer<T> where T : unmanaged
    {
        private readonly CudaPinnedMemoryAllocator _allocator;
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private bool _disposed;

        public IntPtr HostPointer { get; }
        public IntPtr DevicePointer { get; }
        public long Count { get; }

        public CudaPinnedMemoryBuffer(
            CudaPinnedMemoryAllocator allocator,
            IntPtr hostPointer,
            IntPtr devicePointer,
            long count,
            CudaContext context,
            ILogger logger)
        {
            _allocator = allocator;
            HostPointer = hostPointer;
            DevicePointer = devicePointer;
            Count = count;
            _context = context;
            _logger = logger;
        }

        public unsafe Span<T> AsSpan()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new Span<T>(HostPointer.ToPointer(), (int)Count);
        }

        public unsafe Memory<T> AsMemory()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Create a custom MemoryManager that wraps the pinned memory

            return new PinnedMemoryManager<T>(HostPointer, (int)Count, this).Memory;
        }

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
    internal sealed class PinnedMemoryRegistration : IPinnedMemoryRegistration
    {
        private readonly CudaPinnedMemoryAllocator _allocator;
        private bool _disposed;

        public IntPtr HostPointer { get; }
        public long Size { get; }

        public PinnedMemoryRegistration(IntPtr hostPointer, long size, CudaPinnedMemoryAllocator allocator)
        {
            HostPointer = hostPointer;
            Size = size;
            _allocator = allocator;
        }

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
        public long TotalAllocated { get; init; }
        public long MaxAllowed { get; init; }
        public long Available { get; init; }
        public int AllocationCount { get; init; }
        public long AverageAllocationSize { get; init; }
    }
}