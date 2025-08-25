using System;

namespace DotCompute.Backends.CUDA.Profiling.Models
{
    /// <summary>
    /// Represents GPU performance and utilization metrics.
    /// </summary>
    public class GpuMetrics
    {
        /// <summary>
        /// Gets or initializes the timestamp when metrics were collected.
        /// </summary>
        public DateTimeOffset Timestamp { get; init; }

        /// <summary>
        /// Gets or initializes the device index for these metrics.
        /// </summary>
        public int DeviceIndex { get; init; }

        /// <summary>
        /// Gets or sets the GPU utilization percentage (0-100).
        /// </summary>
        public uint GpuUtilization { get; set; }

        /// <summary>
        /// Gets or sets the memory utilization percentage (0-100).
        /// </summary>
        public uint MemoryUtilization { get; set; }

        /// <summary>
        /// Gets or sets the amount of memory used in bytes.
        /// </summary>
        public ulong MemoryUsed { get; set; }

        /// <summary>
        /// Gets or sets the total available memory in bytes.
        /// </summary>
        public ulong MemoryTotal { get; set; }

        /// <summary>
        /// Gets or sets the amount of free memory in bytes.
        /// </summary>
        public ulong MemoryFree { get; set; }

        /// <summary>
        /// Gets or sets the GPU temperature in degrees Celsius.
        /// </summary>
        public uint Temperature { get; set; }

        /// <summary>
        /// Gets or sets the power usage in watts.
        /// </summary>
        public double PowerUsage { get; set; }
    }
}