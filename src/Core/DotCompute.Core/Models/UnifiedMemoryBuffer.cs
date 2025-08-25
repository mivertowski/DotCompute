using System;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Core.Abstractions;
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

        /// <summary>
        /// Initializes a new instance of the UnifiedMemoryBuffer class.
        /// </summary>
        public UnifiedMemoryBuffer(IntPtr pointer, long sizeInBytes, int deviceId, ManagedMemoryFlags flags)
        {
            Pointer = pointer;
            SizeInBytes = sizeInBytes;
            DeviceId = deviceId;
            Flags = flags;
            CurrentResidence = flags.HasFlag(ManagedMemoryFlags.PreferDevice) 
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
    }
}