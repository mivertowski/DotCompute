// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Memory locations where buffers can be allocated.
    /// This is a duplicate of DotCompute.Core.Memory.MemoryLocation for backward compatibility.
    /// </summary>
    public enum MemoryLocation
    {
        /// <summary>
        /// Host (CPU) memory.
        /// </summary>
        Host,

        /// <summary>
        /// Device (GPU) memory.
        /// </summary>
        Device,

        /// <summary>
        /// Pinned host memory for faster transfers.
        /// </summary>
        HostPinned,

        /// <summary>
        /// Unified memory accessible by both host and device.
        /// </summary>
        Unified,

        /// <summary>
        /// Managed memory with automatic migration.
        /// </summary>
        Managed,

        /// <summary>
        /// System memory pool.
        /// </summary>
        System,

        /// <summary>
        /// GPU local memory.
        /// </summary>
        GPU,

        /// <summary>
        /// CPU local memory.
        /// </summary>
        CPU
    }

    /// <summary>
    /// Memory access modes.
    /// This is a duplicate of DotCompute.Core.Memory.MemoryAccess for backward compatibility.
    /// </summary>
    [Flags]
    public enum MemoryAccess
    {
        /// <summary>
        /// Read-only access.
        /// </summary>
        ReadOnly = 1,

        /// <summary>
        /// Write-only access.
        /// </summary>
        WriteOnly = 2,

        /// <summary>
        /// Read-write access.
        /// </summary>
        ReadWrite = ReadOnly | WriteOnly,

        /// <summary>
        /// Host access for debugging.
        /// </summary>
        HostAccess = 4
    }

    /// <summary>
    /// Memory mapping modes.
    /// This is a duplicate of DotCompute.Core.Memory.MemoryMapMode for backward compatibility.
    /// </summary>
    public enum MemoryMapMode
    {
        /// <summary>
        /// Read-only mapping.
        /// </summary>
        Read,

        /// <summary>
        /// Write-only mapping.
        /// </summary>
        Write,

        /// <summary>
        /// Read-write mapping.
        /// </summary>
        ReadWrite
    }

    /// <summary>
    /// Represents a mapped memory region.
    /// This is a duplicate of DotCompute.Core.Memory.IMemoryMapping for backward compatibility.
    /// </summary>
    public interface IMemoryMapping<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// Gets the mapped memory span.
        /// </summary>
        public Span<T> Span { get; }

        /// <summary>
        /// Gets the mapping mode.
        /// </summary>
        public MemoryMapMode Mode { get; }

        /// <summary>
        /// Gets whether the mapping is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Flushes any pending writes.
        /// </summary>
        public void Flush();
    }

    /// <summary>
    /// Memory usage statistics.
    /// This is a duplicate of DotCompute.Core.Memory.IMemoryStatistics for backward compatibility.
    /// </summary>
    public interface IMemoryStatistics
    {
        /// <summary>
        /// Gets total allocated memory in bytes.
        /// </summary>
        public long TotalAllocatedBytes { get; }

        /// <summary>
        /// Gets available memory in bytes.
        /// </summary>
        public long AvailableBytes { get; }

        /// <summary>
        /// Gets peak memory usage in bytes.
        /// </summary>
        public long PeakUsageBytes { get; }

        /// <summary>
        /// Gets allocation count.
        /// </summary>
        public int AllocationCount { get; }

        /// <summary>
        /// Gets memory fragmentation percentage.
        /// </summary>
        public double FragmentationPercentage { get; }

        /// <summary>
        /// Gets memory usage by location.
        /// </summary>
        public IReadOnlyDictionary<MemoryLocation, long> UsageByLocation { get; }
    }
}
