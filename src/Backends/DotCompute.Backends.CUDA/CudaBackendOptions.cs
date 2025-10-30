// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA
{

    /// <summary>
    /// Configuration options for the CUDA backend.
    /// </summary>
    public sealed class CudaBackendOptions
    {
        /// <summary>
        /// Gets or sets the preferred device ID for single-device scenarios.
        /// </summary>
        public int PreferredDeviceId { get; set; }


        /// <summary>
        /// Gets or sets the device selection strategy.
        /// </summary>
        public CudaDeviceSelectionStrategy DeviceSelectionStrategy { get; set; } = CudaDeviceSelectionStrategy.Default;

        /// <summary>
        /// Gets or sets whether to enable multi-device support.
        /// </summary>
        public bool EnableMultiDevice { get; set; }


        /// <summary>
        /// Gets or sets whether to enable unified memory.
        /// </summary>
        public bool EnableUnifiedMemory { get; set; }


        /// <summary>
        /// Gets or sets whether to enable CUDA graphs for performance optimization.
        /// </summary>
        public bool EnableCudaGraphs { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable stream capture.
        /// </summary>
        public bool EnableStreamCapture { get; set; } = true;

        /// <summary>
        /// Gets or sets the default stream priority.
        /// </summary>
        public int DefaultStreamPriority { get; set; }


        /// <summary>
        /// Gets or sets whether to enable memory pooling.
        /// </summary>
        public bool EnableMemoryPooling { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable synchronous execution for debugging.
        /// </summary>
        public bool EnableSynchronousExecution { get; set; }


        /// <summary>
        /// Gets or sets whether to enable comprehensive error checking.
        /// </summary>
        public bool EnableErrorChecking { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to log kernel launches for debugging.
        /// </summary>
        public bool LogKernelLaunches { get; set; }
    }

    /// <summary>
    /// CUDA device selection strategies.
    /// </summary>
    public enum CudaDeviceSelectionStrategy
    {
        /// <summary>
        /// Use the default device (typically device 0).
        /// </summary>
        Default,

        /// <summary>
        /// Select the device with the highest compute capability.
        /// </summary>
        HighestComputeCapability,

        /// <summary>
        /// Select the device with the most memory.
        /// </summary>
        MostMemory,

        /// <summary>
        /// Select the device with the fastest clock speed.
        /// </summary>
        FastestClock
    }
}
