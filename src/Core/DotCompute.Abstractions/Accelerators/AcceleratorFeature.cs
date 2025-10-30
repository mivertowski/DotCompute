// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Accelerators;

/// <summary>
/// Defines hardware and software features that may be supported by compute accelerators.
/// </summary>
/// <remarks>
/// This enumeration uses the <see cref="FlagsAttribute"/> to allow combination of multiple features.
/// Use bitwise operations to check for multiple feature support.
/// </remarks>
[Flags]
public enum AcceleratorFeature
{
    /// <summary>
    /// No special features are supported.
    /// </summary>
    None = 0,

    /// <summary>
    /// Support for 16-bit floating-point (half-precision) operations.
    /// </summary>
    /// <remarks>
    /// This feature enables faster computation for workloads that don't require full precision,
    /// such as certain machine learning inference tasks.
    /// </remarks>
    Float16 = 1 << 0,

    /// <summary>
    /// Support for 64-bit floating-point (double-precision) operations.
    /// </summary>
    /// <remarks>
    /// Essential for scientific computing applications requiring high numerical precision.
    /// </remarks>
    DoublePrecision = 1 << 1,

    /// <summary>
    /// Support for 64-bit integer operations.
    /// </summary>
    /// <remarks>
    /// Required for applications working with large integer values or pointers on 64-bit systems.
    /// </remarks>
    LongInteger = 1 << 2,

    /// <summary>
    /// Support for Tensor Core operations (NVIDIA) or equivalent matrix acceleration units.
    /// </summary>
    /// <remarks>
    /// Provides significant acceleration for matrix multiplication and convolution operations,
    /// particularly beneficial for deep learning workloads.
    /// </remarks>
    TensorCores = 1 << 3,

    /// <summary>
    /// Support for unified memory between host and device.
    /// </summary>
    /// <remarks>
    /// Allows automatic memory migration between CPU and GPU, simplifying memory management
    /// at the potential cost of performance.
    /// </remarks>
    UnifiedMemory = 1 << 4,

    /// <summary>
    /// Support for cooperative groups and grid synchronization.
    /// </summary>
    /// <remarks>
    /// Enables synchronization across multiple thread blocks, allowing more complex
    /// parallel algorithms to be implemented.
    /// </remarks>
    CooperativeGroups = 1 << 5,

    /// <summary>
    /// Support for dynamic parallelism (nested kernel launches).
    /// </summary>
    /// <remarks>
    /// Allows kernels to launch other kernels directly from device code,
    /// enabling recursive and adaptive algorithms.
    /// </remarks>
    DynamicParallelism = 1 << 6,

    /// <summary>
    /// Support for atomic operations on global and shared memory.
    /// </summary>
    /// <remarks>
    /// Essential for implementing thread-safe data structures and algorithms
    /// that require synchronization between threads.
    /// </remarks>
    AtomicOperations = 1 << 7,

    /// <summary>
    /// Support for Brain Floating Point 16-bit format (bfloat16).
    /// </summary>
    /// <remarks>
    /// A 16-bit format that maintains the same exponent range as float32,
    /// popular in machine learning for its balance of range and precision.
    /// </remarks>
    Bfloat16 = 1 << 8,

    /// <summary>
    /// Support for signed 8-bit integer operations.
    /// </summary>
    /// <remarks>
    /// Enables efficient quantized integer operations, commonly used in
    /// optimized neural network inference.
    /// </remarks>
    SignedByte = 1 << 9,

    /// <summary>
    /// Support for mixed-precision operations within a single kernel.
    /// </summary>
    /// <remarks>
    /// Allows combining different precision levels in a single computation
    /// for optimal performance and accuracy trade-offs.
    /// </remarks>
    MixedPrecision = 1 << 10
}
