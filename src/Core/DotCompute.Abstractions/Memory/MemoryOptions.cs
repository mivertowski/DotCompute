// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory
{
    /// <summary>
    /// Options for memory allocation and management.
    /// </summary>
    [Flags]
    public enum MemoryOptions
    {
        /// <summary>
        /// No special options.
        /// </summary>
        None = 0,

        /// <summary>
        /// Memory should be pinned in host memory.
        /// </summary>
        Pinned = 1 << 0,

        /// <summary>
        /// Memory should be mapped for device access.
        /// </summary>
        Mapped = 1 << 1,

        /// <summary>
        /// Memory should be write-combined for better throughput.
        /// </summary>
        WriteCombined = 1 << 2,

        /// <summary>
        /// Memory should be allocated as portable across devices.
        /// </summary>
        Portable = 1 << 3,

        /// <summary>
        /// Memory should use unified memory allocation.
        /// </summary>
        Unified = 1 << 4,

        /// <summary>
        /// Memory should be cached.
        /// </summary>
        Cached = 1 << 5,

        /// <summary>
        /// Memory access should be coherent across devices.
        /// </summary>
        Coherent = 1 << 6,

        /// <summary>
        /// Memory should be allocated from high-priority pool.
        /// </summary>
        HighPriority = 1 << 7,

        /// <summary>
        /// Memory should be zero-initialized.
        /// </summary>
        ZeroInitialized = 1 << 8,

        /// <summary>
        /// Memory should be aligned to specific boundary.
        /// </summary>
        Aligned = 1 << 9,

        /// <summary>
        /// Memory should be visible to host.
        /// </summary>
        HostVisible = 1 << 10,

        /// <summary>
        /// Memory should be initialized to zero on allocation.
        /// </summary>
        InitializeToZero = 1 << 11,

        /// <summary>
        /// Memory should automatically migrate between devices as needed.
        /// </summary>
        AutoMigrate = 1 << 12,

        /// <summary>
        /// Memory should be allocated from a memory pool for better performance.
        /// </summary>
        Pooled = 1 << 13,

        /// <summary>
        /// Memory should be allocated local to the device for optimal access.
        /// </summary>
        DeviceLocal = 1 << 14
    }

    /// <summary>
    /// Extension methods for MemoryOptions.
    /// </summary>
    public static class MemoryOptionsExtensions
    {
        /// <summary>
        /// Gets a value indicating whether the memory should be initialized to zero.
        /// </summary>
        /// <param name="options">The memory options.</param>
        /// <returns>True if memory should be zero-initialized.</returns>
        public static bool InitializeToZero(this MemoryOptions options) => options.HasFlag(MemoryOptions.InitializeToZero) || options.HasFlag(MemoryOptions.ZeroInitialized);

        /// <summary>
        /// Gets a value indicating whether the memory should automatically migrate between devices.
        /// </summary>
        /// <param name="options">The memory options.</param>
        /// <returns>True if memory should auto-migrate.</returns>
        public static bool ShouldAutoMigrate(this MemoryOptions options) => options.HasFlag(MemoryOptions.AutoMigrate);

        /// <summary>
        /// Gets a value indicating whether the memory should be pinned in host memory.
        /// </summary>
        /// <param name="options">The memory options.</param>
        /// <returns>True if memory should be pinned.</returns>
        public static bool IsPinned(this MemoryOptions options) => options.HasFlag(MemoryOptions.Pinned);
    }
}