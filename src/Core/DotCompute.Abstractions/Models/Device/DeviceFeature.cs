// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Models.Device
{
    /// <summary>
    /// Represents individual device features that can be queried for support.
    /// This enum provides a type-safe way to check for specific device capabilities.
    /// </summary>
    /// <remarks>
    /// Unlike the DeviceFeatures flags enum, this enum represents individual features
    /// for use in querying methods like IDeviceCapabilities.IsFeatureSupported().
    /// Each value corresponds to a specific bit in the DeviceFeatures flags.
    /// </remarks>
    public enum DeviceFeature
    {
        /// <summary>
        /// Double-precision (64-bit) floating-point arithmetic support.
        /// </summary>
        /// <remarks>
        /// Queries whether the device can perform double-precision floating-point
        /// operations with IEEE 754 compliance. Essential for scientific computing,
        /// high-precision simulations, and applications requiring extended precision.
        /// </remarks>
        DoublePrecision,

        /// <summary>
        /// Half-precision (16-bit) floating-point arithmetic support.
        /// </summary>
        /// <remarks>
        /// Queries whether the device supports 16-bit floating-point operations,
        /// which can provide significant memory and performance benefits for
        /// machine learning and graphics applications where reduced precision
        /// is acceptable.
        /// </remarks>
        HalfPrecision,

        /// <summary>
        /// Atomic memory operation support for thread synchronization.
        /// </summary>
        /// <remarks>
        /// Queries whether the device supports atomic read-modify-write operations
        /// for implementing lock-free algorithms, parallel reductions, and other
        /// synchronization primitives. Critical for many parallel algorithms.
        /// </remarks>
        Atomics,

        /// <summary>
        /// Local memory support for work-group data sharing.
        /// </summary>
        /// <remarks>
        /// Queries whether the device provides high-speed local memory that can
        /// be shared among work items in the same work group. Used for cache-like
        /// behavior and efficient inter-thread communication.
        /// </remarks>
        LocalMemory,

        /// <summary>
        /// Image object support for texture operations.
        /// </summary>
        /// <remarks>
        /// Queries whether the device supports image objects with hardware-accelerated
        /// filtering, format conversion, and specialized memory access patterns
        /// optimized for 2D data structures.
        /// </remarks>
        Images,

        /// <summary>
        /// Three-dimensional image object support.
        /// </summary>
        /// <remarks>
        /// Queries whether the device extends image support to 3D volumes,
        /// enabling volumetric operations, 3D convolutions, and advanced
        /// scientific visualization capabilities.
        /// </remarks>
        Images3D,

        /// <summary>
        /// Unified memory addressing between host and device.
        /// </summary>
        /// <remarks>
        /// Queries whether the device supports a unified memory model where
        /// the same virtual addresses can be accessed from both host and device
        /// code, simplifying memory management and enabling automatic migration.
        /// </remarks>
        UnifiedMemory,

        /// <summary>
        /// Dynamic parallelism for kernel-launched kernels.
        /// </summary>
        /// <remarks>
        /// Queries whether the device allows kernels to dynamically launch
        /// other kernels, enabling recursive algorithms and adaptive parallelization
        /// strategies that adjust based on runtime conditions.
        /// </remarks>
        DynamicParallelism,

        /// <summary>
        /// Tensor processing unit support for AI acceleration.
        /// </summary>
        /// <remarks>
        /// Queries whether the device includes specialized tensor processing
        /// hardware for accelerating matrix operations, convolutions, and
        /// other AI/ML workloads through optimized mixed-precision arithmetic.
        /// </remarks>
        TensorCores
    }
}
