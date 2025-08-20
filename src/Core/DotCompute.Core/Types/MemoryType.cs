// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Types
{

    /// <summary>
    /// Represents different types of memory available on compute devices.
    /// </summary>
    public enum MemoryType
    {
        /// <summary>
        /// Memory that is local to the device/GPU and optimized for high-bandwidth access.
        /// Not directly accessible from the host.
        /// </summary>
        DeviceLocal,

        /// <summary>
        /// Memory that is accessible from the host CPU but may have slower access patterns.
        /// Often used for staging data transfers.
        /// </summary>
        HostVisible,

        /// <summary>
        /// Unified memory that can be accessed by both host and device.
        /// Provides automatic migration between host and device as needed.
        /// </summary>
        Shared
    }
}
