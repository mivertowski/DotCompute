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
        /// <value>An array containing the maximum work item size for each dimension.</value>
        public long[] MaxWorkItemSizes { get; }

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
    }
}