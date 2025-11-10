// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Models.Device;

namespace DotCompute.Abstractions.Interfaces.Device
{
    /// <summary>
    /// Provides comprehensive information about a compute device's capabilities,
    /// including performance characteristics, supported features, and data types.
    /// </summary>
    public interface IDeviceCapabilities
    {
        /// <summary>
        /// Gets the compute capability version of the device.
        /// </summary>
        /// <value>The version indicating the level of compute capability support.</value>
        public Version ComputeCapability { get; }

        /// <summary>
        /// Gets the maximum number of work items that can be grouped together.
        /// </summary>
        /// <value>The maximum work group size supported by the device.</value>
        public int MaxWorkGroupSize { get; }

        /// <summary>
        /// Gets the maximum number of dimensions for work item indexing.
        /// </summary>
        /// <value>The maximum number of work item dimensions (typically 3).</value>
        public int MaxWorkItemDimensions { get; }

        /// <summary>
        /// Gets the maximum work item sizes for each dimension.
        /// </summary>
        /// <value>A read-only list containing the maximum work item size for each dimension.</value>
        public IReadOnlyList<long> MaxWorkItemSizes { get; }

        /// <summary>
        /// Gets the number of compute units (cores/processors) available on the device.
        /// </summary>
        /// <value>The number of parallel compute units.</value>
        public int ComputeUnits { get; }

        /// <summary>
        /// Gets the base clock frequency of the device in MHz.
        /// </summary>
        /// <value>The clock frequency in megahertz.</value>
        public int ClockFrequency { get; }

        /// <summary>
        /// Gets a bitfield of all supported device features.
        /// </summary>
        /// <value>A combination of feature flags indicating supported capabilities.</value>
        public DeviceFeatures SupportedFeatures { get; }

        /// <summary>
        /// Gets a bitfield of all supported data types.
        /// </summary>
        /// <value>A combination of data type flags indicating supported numeric types.</value>
        public DataTypeSupport SupportedDataTypes { get; }

        /// <summary>
        /// Checks if a specific feature is supported by the device.
        /// </summary>
        /// <param name="feature">The feature to check for support.</param>
        /// <returns>True if the feature is supported, false otherwise.</returns>
        public bool IsFeatureSupported(DeviceFeature feature);

        /// <summary>
        /// Gets whether the device supports nanosecond-precision hardware timers.
        /// </summary>
        /// <value>
        /// True if the device supports nanosecond timers (e.g., CUDA %%globaltimer on CC 6.0+),
        /// false if only microsecond precision is available (e.g., CUDA events on CC &lt; 6.0).
        /// </value>
        /// <remarks>
        /// <para>
        /// Nanosecond precision is available on:
        /// <list type="bullet">
        /// <item><description>CUDA: Compute Capability 6.0+ (%%globaltimer register)</description></item>
        /// <item><description>OpenCL: Platform-dependent (typically microsecond precision)</description></item>
        /// <item><description>CPU: Depends on Stopwatch resolution (~100ns typical)</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public bool SupportsNanosecondTimers { get; }

        /// <summary>
        /// Gets the hardware timer resolution in nanoseconds.
        /// </summary>
        /// <value>
        /// The minimum measurable time interval in nanoseconds. Lower values indicate higher precision.
        /// Typical values:
        /// <list type="bullet">
        /// <item><description>CUDA (CC 6.0+): 1 ns (%%globaltimer)</description></item>
        /// <item><description>CUDA (CC &lt; 6.0): 1,000 ns (CUDA events)</description></item>
        /// <item><description>OpenCL: 1,000 ns (clock() built-in)</description></item>
        /// <item><description>CPU: ~100 ns (Stopwatch)</description></item>
        /// </list>
        /// </value>
        public long TimerResolutionNanos { get; }

        /// <summary>
        /// Gets the GPU hardware clock frequency in Hertz (cycles per second).
        /// </summary>
        /// <value>
        /// The timer clock frequency in Hz. Typical values:
        /// <list type="bullet">
        /// <item><description>CUDA (nanosecond timer): 1,000,000,000 Hz (1 GHz)</description></item>
        /// <item><description>CUDA (event timer): 1,000,000 Hz (1 MHz)</description></item>
        /// <item><description>OpenCL: Platform-dependent</description></item>
        /// <item><description>CPU: Stopwatch.Frequency (typically 10 MHz)</description></item>
        /// </list>
        /// </value>
        /// <remarks>
        /// This frequency determines the timer resolution: resolution = 1 / frequency.
        /// A 1 GHz clock provides 1 ns resolution.
        /// </remarks>
        public long ClockFrequencyHz { get; }
    }
}
