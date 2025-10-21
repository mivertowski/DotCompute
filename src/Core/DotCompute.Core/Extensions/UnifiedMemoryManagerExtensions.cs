// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Extensions
{
    /// <summary>
    /// Extension methods for IUnifiedMemoryManager to provide additional memory query operations
    /// and backward compatibility with legacy APIs that expect these methods.
    /// </summary>
    public static class UnifiedMemoryManagerExtensions
    {
        #region Memory Query Methods

        /// <summary>
        /// Gets the available memory in bytes that can be allocated by this memory manager.
        /// This delegates to the Statistics property or calculates from existing properties.
        /// </summary>
        /// <param name="manager">The unified memory manager instance.</param>
        /// <returns>The amount of available memory in bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when manager is null.</exception>
        public static long GetAvailableMemory(this IUnifiedMemoryManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);

            // First try to get from statistics if available
            var stats = manager.Statistics;
            if (stats != null)
            {
                // Prefer the most specific available memory properties
                if (stats.AvailableMemoryBytes > 0)
                {

                    return stats.AvailableMemoryBytes;
                }


                if (stats.AvailableMemory > 0)
                {
                    return stats.AvailableMemory;
                }


                if (stats.TotalAvailable > 0)
                {

                    return stats.TotalAvailable;
                }
            }

            // Fallback to calculating from interface properties
            // If we have total available memory from the interface, use it directly
            if (manager.TotalAvailableMemory > 0)
            {
                // Calculate available as total - current allocated
                var available = manager.TotalAvailableMemory - manager.CurrentAllocatedMemory;
                return Math.Max(0, available); // Ensure non-negative
            }

            // If no statistics or interface properties are available, return 0
            // This is safer than throwing an exception for production systems
            return 0;
        }

        /// <summary>
        /// Gets the total memory in bytes that this memory manager can potentially manage.
        /// This represents the total capacity of the memory subsystem.
        /// </summary>
        /// <param name="manager">The unified memory manager instance.</param>
        /// <returns>The total memory capacity in bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when manager is null.</exception>
        public static long GetTotalMemory(this IUnifiedMemoryManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);

            // First try to get from statistics if available
            var stats = manager.Statistics;
            if (stats != null)
            {
                // Prefer the most specific total memory properties
                if (stats.TotalMemoryBytes > 0)
                {
                    return stats.TotalMemoryBytes;
                }


                if (stats.TotalCapacity > 0)
                {
                    return stats.TotalCapacity;
                }


                if (stats.TotalAllocated > 0)
                {

                    return stats.TotalAllocated;
                }
            }

            // Fallback to interface property
            if (manager.TotalAvailableMemory > 0)
            {

                return manager.TotalAvailableMemory;
            }

            // If no statistics or interface properties are available, return 0
            // This is safer than throwing an exception for production systems

            return 0;
        }

        /// <summary>
        /// Gets the currently used memory in bytes by this memory manager.
        /// This represents the amount of memory that has been allocated and is currently in use.
        /// </summary>
        /// <param name="manager">The unified memory manager instance.</param>
        /// <returns>The amount of used memory in bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when manager is null.</exception>
        public static long GetUsedMemory(this IUnifiedMemoryManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);

            // First try to get from statistics if available
            var stats = manager.Statistics;
            if (stats != null)
            {
                // Prefer the most specific used memory properties
                if (stats.UsedMemoryBytes > 0)
                {
                    return stats.UsedMemoryBytes;
                }


                if (stats.CurrentUsed > 0)
                {
                    return stats.CurrentUsed;
                }


                if (stats.CurrentUsage > 0)
                {

                    return stats.CurrentUsage;
                }
            }

            // Fallback to interface property
            if (manager.CurrentAllocatedMemory > 0)
            {

                return manager.CurrentAllocatedMemory;
            }

            // If no statistics or interface properties are available, return 0
            // This is safer than throwing an exception for production systems

            return 0;
        }

        #endregion

        #region Memory Utilization Analysis Methods

        /// <summary>
        /// Calculates the memory utilization percentage (0-100) based on used vs total memory.
        /// </summary>
        /// <param name="manager">The unified memory manager instance.</param>
        /// <returns>The memory utilization as a percentage (0-100).</returns>
        /// <exception cref="ArgumentNullException">Thrown when manager is null.</exception>
        public static double GetMemoryUtilizationPercentage(this IUnifiedMemoryManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);

            var totalMemory = manager.GetTotalMemory();
            var usedMemory = manager.GetUsedMemory();

            if (totalMemory <= 0)
            {

                return 0.0;
            }


            return Math.Min(100.0, Math.Max(0.0, (double)usedMemory / totalMemory * 100.0));
        }

        /// <summary>
        /// Determines if the memory manager is under memory pressure based on utilization.
        /// </summary>
        /// <param name="manager">The unified memory manager instance.</param>
        /// <param name="pressureThreshold">The threshold percentage (0-100) above which memory pressure is considered high. Defaults to 85%.</param>
        /// <returns>True if memory utilization is above the pressure threshold, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when manager is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when pressureThreshold is not between 0 and 100.</exception>
        public static bool IsUnderMemoryPressure(this IUnifiedMemoryManager manager, double pressureThreshold = 85.0)
        {
            ArgumentNullException.ThrowIfNull(manager);
            if (pressureThreshold is < 0.0 or > 100.0)
            {

                throw new ArgumentOutOfRangeException(nameof(pressureThreshold), "Pressure threshold must be between 0 and 100.");
            }


            return manager.GetMemoryUtilizationPercentage() > pressureThreshold;
        }

        /// <summary>
        /// Gets a detailed memory summary containing all key memory metrics.
        /// </summary>
        /// <param name="manager">The unified memory manager instance.</param>
        /// <returns>A formatted string with memory usage details.</returns>
        /// <exception cref="ArgumentNullException">Thrown when manager is null.</exception>
        public static string GetMemorySummary(this IUnifiedMemoryManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);

            var totalMemory = manager.GetTotalMemory();
            var usedMemory = manager.GetUsedMemory();
            var availableMemory = manager.GetAvailableMemory();
            var utilizationPercentage = manager.GetMemoryUtilizationPercentage();

            return $"Memory Summary - Total: {FormatBytes(totalMemory)}, " +
                   $"Used: {FormatBytes(usedMemory)}, " +
                   $"Available: {FormatBytes(availableMemory)}, " +
                   $"Utilization: {utilizationPercentage:F1}%";
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Formats a byte count into a human-readable string with appropriate units.
        /// </summary>
        /// <param name="bytes">The number of bytes to format.</param>
        /// <returns>A formatted string with appropriate units (B, KB, MB, GB, TB).</returns>
        private static string FormatBytes(long bytes)
        {
            if (bytes < 0)
            {

                return "0 B";
            }


            string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
            var order = 0;
            double size = bytes;

            while (size >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F1} {suffixes[order]}";
        }

        #endregion

        #region CUDA-Specific Allocation Methods

        /// <summary>
        /// Allocates unified memory that is accessible by both host and device.
        /// This is primarily a CUDA feature.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="memoryManager">The memory manager.</param>
        /// <param name="count">The number of elements to allocate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A unified memory buffer.</returns>
        [RequiresUnreferencedCode("This method uses reflection to dynamically invoke generic methods and may not work in AOT scenarios.")]
        [RequiresDynamicCode("This method uses reflection to dynamically invoke generic methods and may not work in AOT scenarios.")]
        public static ValueTask<IUnifiedMemoryBuffer<T>> AllocateUnifiedAsync<T>(
            this IUnifiedMemoryManager memoryManager,
            int count,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(memoryManager);

            // Try to use native implementation if available

            var method = memoryManager.GetType().GetMethod("AllocateUnifiedAsync", [typeof(int), typeof(CancellationToken)]);
            if (method != null && method.IsGenericMethodDefinition)
            {
                var genericMethod = method.MakeGenericMethod(typeof(T));
                var result = genericMethod.Invoke(memoryManager, [count, cancellationToken]);
                if (result is ValueTask<IUnifiedMemoryBuffer<T>> valueTask)
                {

                    return valueTask;
                }
            }

            // Fall back to regular allocation with unified memory flag

            return memoryManager.AllocateAsync<T>(count, MemoryOptions.Unified, cancellationToken);
        }

        /// <summary>
        /// Allocates pinned (page-locked) host memory for faster transfers.
        /// This is primarily a CUDA feature.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="memoryManager">The memory manager.</param>
        /// <param name="count">The number of elements to allocate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A pinned memory buffer.</returns>
        [RequiresUnreferencedCode("This method uses reflection to dynamically invoke generic methods and may not work in AOT scenarios.")]
        [RequiresDynamicCode("This method uses reflection to dynamically invoke generic methods and may not work in AOT scenarios.")]
        public static ValueTask<IUnifiedMemoryBuffer<T>> AllocatePinnedAsync<T>(
            this IUnifiedMemoryManager memoryManager,
            int count,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(memoryManager);

            // Try to use native implementation if available

            var method = memoryManager.GetType().GetMethod("AllocatePinnedAsync", [typeof(int), typeof(CancellationToken)]);
            if (method != null && method.IsGenericMethodDefinition)
            {
                var genericMethod = method.MakeGenericMethod(typeof(T));
                var result = genericMethod.Invoke(memoryManager, [count, cancellationToken]);
                if (result is ValueTask<IUnifiedMemoryBuffer<T>> valueTask)
                {

                    return valueTask;
                }
            }

            // Fall back to regular allocation with pinned memory flag

            return memoryManager.AllocateAsync<T>(count, MemoryOptions.Pinned, cancellationToken);
        }

        #endregion
    }
}
