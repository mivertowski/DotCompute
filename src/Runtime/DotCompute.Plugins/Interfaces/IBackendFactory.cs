// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Plugins.Interfaces
{

    /// <summary>
    /// Factory interface for creating accelerator backend instances.
    /// </summary>
    public interface IBackendFactory
    {
        /// <summary>
        /// Gets the name of this backend.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the description of this backend.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the version of this backend.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Determines if this backend is available on the current system.
        /// </summary>
        public bool IsAvailable();

        /// <summary>
        /// Creates all available accelerators for this backend.
        /// </summary>
        public IEnumerable<IAccelerator> CreateAccelerators();

        /// <summary>
        /// Creates the default accelerator for this backend.
        /// </summary>
        public IAccelerator? CreateDefaultAccelerator();

        /// <summary>
        /// Gets the capabilities of this backend.
        /// </summary>
        public BackendCapabilities GetCapabilities();
    }

    /// <summary>
    /// Describes the capabilities of a compute backend.
    /// </summary>
    public class BackendCapabilities
    {
        /// <summary>
        /// Indicates if the backend supports float16 operations.
        /// </summary>
        public bool SupportsFloat16 { get; set; }

        /// <summary>
        /// Indicates if the backend supports float32 operations.
        /// </summary>
        public bool SupportsFloat32 { get; set; }

        /// <summary>
        /// Indicates if the backend supports float64 operations.
        /// </summary>
        public bool SupportsFloat64 { get; set; }

        /// <summary>
        /// Indicates if the backend supports int8 operations.
        /// </summary>
        public bool SupportsInt8 { get; set; }

        /// <summary>
        /// Indicates if the backend supports int16 operations.
        /// </summary>
        public bool SupportsInt16 { get; set; }

        /// <summary>
        /// Indicates if the backend supports int32 operations.
        /// </summary>
        public bool SupportsInt32 { get; set; }

        /// <summary>
        /// Indicates if the backend supports int64 operations.
        /// </summary>
        public bool SupportsInt64 { get; set; }

        /// <summary>
        /// Indicates if the backend supports asynchronous execution.
        /// </summary>
        public bool SupportsAsyncExecution { get; set; }

        /// <summary>
        /// Indicates if the backend supports multiple devices.
        /// </summary>
        public bool SupportsMultiDevice { get; set; }

        /// <summary>
        /// Indicates if the backend supports unified memory.
        /// </summary>
        public bool SupportsUnifiedMemory { get; set; }

        /// <summary>
        /// Maximum number of devices supported.
        /// </summary>
        public int MaxDevices { get; set; }

        /// <summary>
        /// Maximum memory available across all devices in bytes.
        /// </summary>
        /// <remarks>
        /// This property represents the total memory capacity that can be utilized
        /// by this backend across all available devices. For backends that support
        /// multiple devices, this might be the sum of all device memories or the
        /// largest single device memory, depending on the backend's memory model.
        /// A value of 0 indicates that the memory limit is unknown or unlimited.
        /// </remarks>
        public long MaxMemory { get; set; }


        /// <summary>
        /// List of supported features specific to this backend.
        /// </summary>
        public IReadOnlyList<string> SupportedFeatures { get; init; } = Array.Empty<string>();
    }
}
