// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines output data formats for computation results.
    /// These formats control how computed data is structured and stored,
    /// affecting performance, memory usage, and compatibility with different systems.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// Native device format.
        /// Data is kept in the device's native format for optimal performance.
        /// Minimizes data conversion overhead but may not be directly accessible
        /// from host applications without explicit transfer operations.
        /// </summary>
        Native,

        /// <summary>
        /// Host-accessible format.
        /// Data is formatted for direct access from host applications.
        /// Enables immediate consumption by CPU-based code but may require
        /// additional conversion overhead from device-native formats.
        /// </summary>
        Host,

        /// <summary>
        /// Interleaved format for better cache performance.
        /// Data elements are arranged in an interleaved pattern to improve
        /// cache locality and memory access patterns. Optimizes for scenarios
        /// where data will be accessed in specific patterns.
        /// </summary>
        Interleaved,

        /// <summary>
        /// Compressed format for reduced memory usage.
        /// Data is compressed to minimize memory footprint and bandwidth usage.
        /// Trades computational overhead for memory efficiency, suitable for
        /// scenarios where memory or bandwidth is the limiting factor.
        /// </summary>
        Compressed
    }
}