// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA unified memory buffer implementation.
    /// </summary>
    public sealed class CudaMemoryBufferInfo
    {
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>
        public nint DevicePointer { get; set; }
        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        /// <value>The size in bytes.</value>
        public long SizeInBytes { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether managed.
        /// </summary>
        /// <value>The is managed.</value>
        public bool IsManaged { get; set; }
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>The device id.</value>
        public int DeviceId { get; set; }
    }
}
