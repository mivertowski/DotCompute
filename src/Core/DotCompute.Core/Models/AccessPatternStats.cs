using System;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Statistics about memory access patterns for optimization.
    /// </summary>
    public class AccessPatternStats
    {
        /// <summary>
        /// Gets or sets the number of host accesses.
        /// </summary>
        public long HostAccessCount { get; set; }

        /// <summary>
        /// Gets or sets the number of device accesses.
        /// </summary>
        public long DeviceAccessCount { get; set; }

        /// <summary>
        /// Gets or sets the last host access time.
        /// </summary>
        public DateTimeOffset? LastHostAccess { get; set; }

        /// <summary>
        /// Gets or sets the last device access time.
        /// </summary>
        public DateTimeOffset? LastDeviceAccess { get; set; }

        /// <summary>
        /// Gets or sets the number of page faults.
        /// </summary>
        public long PageFaultCount { get; set; }

        /// <summary>
        /// Gets or sets the number of memory migrations.
        /// </summary>
        public int MigrationCount { get; set; }

        /// <summary>
        /// Gets or sets the total bytes migrated.
        /// </summary>
        public long BytesMigrated { get; set; }

        /// <summary>
        /// Gets or sets the average access size in bytes.
        /// </summary>
        public double AverageAccessSize { get; set; }

        /// <summary>
        /// Gets the dominant access location based on statistics.
        /// </summary>
        public string DominantAccessLocation => 
            HostAccessCount > DeviceAccessCount ? "Host" : "Device";

        /// <summary>
        /// Gets the access ratio (device accesses / total accesses).
        /// </summary>
        public double DeviceAccessRatio => 
            HostAccessCount + DeviceAccessCount > 0 
                ? (double)DeviceAccessCount / (HostAccessCount + DeviceAccessCount) 
                : 0;
    }
}
