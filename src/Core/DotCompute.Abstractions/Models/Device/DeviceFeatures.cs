// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Models.Device
{
    /// <summary>
    /// Defines feature flags representing various capabilities supported by compute devices.
    /// These flags can be combined using bitwise operations to represent multiple features.
    /// </summary>
    /// <remarks>
    /// Device features determine which operations, data types, and programming constructs
    /// are available for kernel development. The framework uses these flags to enable
    /// conditional compilation, optimize kernels, and validate compatibility.
    /// Features are discovered during device initialization and remain constant.
    /// </remarks>
    [Flags]
    public enum DeviceFeatures
    {
        /// <summary>
        /// No special features are supported beyond basic compute capability.
        /// </summary>
        /// <remarks>
        /// Represents a minimal compute device with only basic integer and single-precision
        /// floating-point operations. This is the baseline capability that all devices
        /// must support.
        /// </remarks>
        None = 0,

        /// <summary>
        /// Device supports double-precision (64-bit) floating-point operations.
        /// </summary>
        /// <remarks>
        /// Enables high-precision mathematical computations required for scientific
        /// applications, financial calculations, and scenarios where floating-point
        /// accuracy is critical. Not all devices support double precision due to
        /// hardware limitations or performance considerations.
        /// </remarks>
        DoublePrecision = 1 << 0,

        /// <summary>
        /// Device supports half-precision (16-bit) floating-point operations.
        /// </summary>
        /// <remarks>
        /// Provides memory-efficient computations with reduced precision, commonly used
        /// in machine learning, graphics, and applications where memory bandwidth
        /// is more important than precision. Offers significant performance benefits
        /// for suitable workloads.
        /// </remarks>
        HalfPrecision = 1 << 1,

        /// <summary>
        /// Device supports atomic operations for thread-safe memory access.
        /// </summary>
        /// <remarks>
        /// Enables lock-free algorithms, reduction operations, and safe concurrent
        /// memory modifications across multiple work items. Essential for algorithms
        /// requiring synchronization between parallel threads without explicit locks.
        /// </remarks>
        Atomics = 1 << 2,

        /// <summary>
        /// Device supports local (shared) memory for work-group communication.
        /// </summary>
        /// <remarks>
        /// Provides high-speed memory shared among work items in the same work group.
        /// Local memory enables efficient data sharing, reduction operations, and
        /// cache-like behavior for frequently accessed data. Critical for optimizing
        /// memory-intensive algorithms.
        /// </remarks>
        LocalMemory = 1 << 3,

        /// <summary>
        /// Device supports image objects and texture operations.
        /// </summary>
        /// <remarks>
        /// Enables specialized image processing operations with hardware-accelerated
        /// filtering, interpolation, and format conversion. Supports various image
        /// formats and provides optimized memory access patterns for 2D data.
        /// </remarks>
        Images = 1 << 4,

        /// <summary>
        /// Device supports three-dimensional image objects.
        /// </summary>
        /// <remarks>
        /// Extends image support to 3D volumes, enabling volumetric rendering,
        /// 3D convolutions, and scientific visualization applications. Provides
        /// hardware-accelerated 3D interpolation and filtering capabilities.
        /// </remarks>
        Images3D = 1 << 5,

        /// <summary>
        /// Device supports unified memory addressing between host and device.
        /// </summary>
        /// <remarks>
        /// Enables seamless memory access where the same pointers can be used
        /// on both host and device. Simplifies programming model and enables
        /// automatic memory migration based on access patterns. Reduces the
        /// need for explicit memory transfers.
        /// </remarks>
        UnifiedMemory = 1 << 6,

        /// <summary>
        /// Device supports dynamic parallelism for nested kernel launches.
        /// </summary>
        /// <remarks>
        /// Allows kernels to launch other kernels dynamically, enabling recursive
        /// algorithms, adaptive parallelization, and complex control flow patterns.
        /// Particularly useful for irregular problems where the amount of work
        /// is not known until runtime.
        /// </remarks>
        DynamicParallelism = 1 << 7,

        /// <summary>
        /// Device includes tensor processing units for accelerated AI workloads.
        /// </summary>
        /// <remarks>
        /// Provides specialized hardware for matrix operations, convolutions, and
        /// other AI/ML primitives. Tensor cores can dramatically accelerate deep
        /// learning training and inference through mixed-precision operations
        /// and optimized matrix multiplication algorithms.
        /// </remarks>
        TensorCores = 1 << 8
    }
}