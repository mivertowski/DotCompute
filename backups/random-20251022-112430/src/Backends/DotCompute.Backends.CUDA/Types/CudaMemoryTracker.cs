// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA memory manager for device memory allocation and management.
    /// </summary>
    public sealed class CudaMemoryTracker : IAsyncDisposable, IDisposable
    {
        private readonly ILogger _logger;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, MemoryAllocationInfo> _allocations;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaMemoryTracker"/> class.
        /// </summary>
        public CudaMemoryTracker(CudaContext context, ILogger logger)
        {
            Context = context?.Handle ?? nint.Zero;
            DeviceIndex = context?.DeviceId ?? 0;
            _logger = logger;
            _allocations = new System.Collections.Concurrent.ConcurrentDictionary<nint, MemoryAllocationInfo>();

            // Initialize with reasonable defaults

            TotalMemory = 8L * 1024 * 1024 * 1024; // 8GB default
            MaxAllocationSize = TotalMemory / 2;
        }

        /// <summary>
        /// Gets or sets the associated CUDA context.
        /// </summary>
        public nint Context { get; set; }

        /// <summary>
        /// Gets or sets the device index.
        /// </summary>
        public int DeviceIndex { get; set; }

        /// <summary>
        /// Gets or sets the total allocated memory in bytes.
        /// </summary>
        public long TotalAllocatedBytes { get; set; }

        /// <summary>
        /// Gets the total memory available on the device.
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets the currently used memory.
        /// </summary>
        public long UsedMemory { get; set; }

        /// <summary>
        /// Gets the maximum allocation size.
        /// </summary>
        public long MaxAllocationSize { get; set; }

        /// <summary>
        /// Resets the memory manager and clears all memory pools.
        /// </summary>
        public void Reset()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                _logger?.LogDebug("Resetting CUDA memory manager for device {DeviceIndex}", DeviceIndex);

                // Free all tracked allocations
                var allocationCount = 0;
                foreach (var kvp in _allocations)
                {
                    try
                    {
                        if (kvp.Key != nint.Zero)
                        {
                            // Free device memory
                            var result = CUDA.Native.CudaRuntime.cudaFree(kvp.Key);
                            if (result != Native.CudaError.Success)
                            {
                                _logger?.LogWarning("Failed to free CUDA memory during reset: {Error}", result);
                            }
                            allocationCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error freeing allocation during reset");
                    }
                }

                // Clear the allocations dictionary
                _allocations.Clear();

                // Reset counters
                TotalAllocatedBytes = 0;
                UsedMemory = 0;

                _logger?.LogInformation("Reset complete. Freed {AllocationCount} allocations for device {DeviceIndex}",

                    allocationCount, DeviceIndex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during CUDA memory manager reset for device {DeviceIndex}", DeviceIndex);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously disposes the memory manager and all allocated resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                try
                {
                    _logger?.LogDebug("Starting async disposal of CUDA memory manager for device {DeviceIndex}", DeviceIndex);
                    // Reset and free all memory

                    // Perform cleanup operations asynchronously

                    await Task.Run(Reset).ConfigureAwait(false);

                    _disposed = true;
                    _logger?.LogDebug("Async disposal completed for CUDA memory manager device {DeviceIndex}", DeviceIndex);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during async disposal of CUDA memory manager device {DeviceIndex}", DeviceIndex);
                    // Still mark as disposed to prevent further operations
                    _disposed = true;
                    throw;
                }
            }
        }

        /// <summary>
        /// Synchronously disposes the memory manager and all allocated resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    Reset();
                    _disposed = true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during disposal of CUDA memory manager device {DeviceIndex}", DeviceIndex);
                    _disposed = true;
                    throw;
                }
            }
        }

        /// <summary>
        /// Tracks a memory allocation.
        /// </summary>
        internal void TrackAllocation(nint devicePtr, long sizeInBytes)
        {
            if (devicePtr != nint.Zero)
            {
                var info = new MemoryAllocationInfo
                {
                    DevicePointer = devicePtr,
                    SizeInBytes = sizeInBytes,
                    AllocatedAt = DateTime.UtcNow
                };

                _ = _allocations.TryAdd(devicePtr, info);
                TotalAllocatedBytes += sizeInBytes;
                UsedMemory += sizeInBytes;
            }
        }

        /// <summary>
        /// Removes tracking for a memory allocation.
        /// </summary>
        internal void UntrackAllocation(nint devicePtr)
        {
            if (_allocations.TryRemove(devicePtr, out var info))
            {
                TotalAllocatedBytes -= info.SizeInBytes;
                UsedMemory -= info.SizeInBytes;
            }
        }

        /// <summary>
        /// Information about a memory allocation.
        /// </summary>
        private sealed class MemoryAllocationInfo
        {
            /// <summary>
            /// Gets or sets the device pointer.
            /// </summary>
            /// <value>The device pointer.</value>
            public nint DevicePointer { get; init; }
            /// <summary>
            /// Gets or sets the size in bytes.
            /// </summary>
            /// <value>The size in bytes.</value>
            public long SizeInBytes { get; init; }
            /// <summary>
            /// Gets or sets the allocated at.
            /// </summary>
            /// <value>The allocated at.</value>
            public DateTime AllocatedAt { get; init; }
        }
    }
}