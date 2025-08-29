using System;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Models;

namespace DotCompute.Backends.CUDA.Memory.Models
{
    /// <summary>
    /// Represents a CUDA unified memory buffer accessible from both host and device.
    /// </summary>
    public class UnifiedMemoryBuffer : IUnifiedMemoryBuffer
    {
        /// <summary>
        /// Gets the pointer to the unified memory.
        /// </summary>
        public IntPtr Pointer { get; private set; }

        /// <summary>
        /// Gets the size of the buffer in bytes.
        /// </summary>
        public long SizeInBytes { get; private set; }

        /// <summary>
        /// Gets the device ID associated with this buffer.
        /// </summary>
        public int DeviceId { get; private set; }

        /// <summary>
        /// Gets the managed memory flags used for allocation.
        /// </summary>
        public ManagedMemoryFlags Flags { get; private set; }

        /// <summary>
        /// Gets or sets the current residence location.
        /// </summary>
        public MemoryResidence CurrentResidence { get; set; }

        /// <summary>
        /// Gets the access pattern statistics.
        /// </summary>
        public AccessPatternStats AccessStats { get; set; } = new();

        /// <summary>
        /// Gets whether the buffer has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }
        
        /// <inheritdoc/>
        public MemoryOptions Options { get; private set; } = MemoryOptions.None;
        
        /// <inheritdoc/>
        public BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;

        /// <summary>
        /// Initializes a new instance of the UnifiedMemoryBuffer class.
        /// </summary>
        public UnifiedMemoryBuffer(IntPtr pointer, long sizeInBytes, int deviceId, ManagedMemoryFlags flags)
        {
            Pointer = pointer;
            SizeInBytes = sizeInBytes;
            DeviceId = deviceId;
            Flags = flags;
            CurrentResidence = flags.HasFlag(ManagedMemoryFlags.PreferDeviceNative) 
                ? MemoryResidence.Device 
                : MemoryResidence.Host;
        }

        /// <summary>
        /// Disposes the unified memory buffer.
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                // Disposal logic would be handled by the memory manager
                IsDisposed = true;
                GC.SuppressFinalize(this);
            }
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
                var bytesToCopy = sourceSpan.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                
                if (offset + bytesToCopy > SizeInBytes)
                {

                    throw new ArgumentException("Source data exceeds buffer capacity.");
                }


                unsafe
                {
                    fixed (T* srcPtr = sourceSpan)
                    {
                        var destPtr = Pointer + (nint)offset;
                        // For unified memory, we can use direct memory copy
                        Buffer.MemoryCopy(srcPtr, destPtr.ToPointer(), SizeInBytes - offset, bytesToCopy);
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
                var bytesToCopy = destinationSpan.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                
                if (offset + bytesToCopy > SizeInBytes)
                {

                    throw new ArgumentException("Destination exceeds buffer capacity.");
                }


                unsafe
                {
                    fixed (T* destPtr = destinationSpan)
                    {
                        var srcPtr = Pointer + (nint)offset;
                        // For unified memory, we can use direct memory copy
                        Buffer.MemoryCopy(srcPtr.ToPointer(), destPtr, destinationSpan.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), bytesToCopy);
                    }
                }
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}