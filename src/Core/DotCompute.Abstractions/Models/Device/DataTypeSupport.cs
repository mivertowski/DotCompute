// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Models.Device
{
    /// <summary>
    /// Defines flags representing the data types supported by a compute device.
    /// These flags can be combined using bitwise operations to represent multiple data types.
    /// </summary>
    /// <remarks>
    /// Data type support determines which numeric formats can be used in kernels
    /// and affects memory layout, performance characteristics, and precision.
    /// The framework uses these flags to validate kernel parameters, optimize
    /// memory transfers, and enable appropriate compiler optimizations.
    /// </remarks>
    [Flags]
    public enum DataTypeSupport
    {
        /// <summary>
        /// 8-bit signed integer support (-128 to 127).
        /// </summary>
        /// <remarks>
        /// Enables compact integer storage for small numeric ranges, character data,
        /// and applications where memory efficiency is critical. Often used in
        /// image processing, neural networks, and embedded applications.
        /// </remarks>
#pragma warning disable CA1720 // Identifier contains type name - Required for hardware data type naming convention
        Int8 = 1 << 0,
#pragma warning restore CA1720

        /// <summary>
        /// 16-bit signed integer support (-32,768 to 32,767).
        /// </summary>
        /// <remarks>
        /// Provides a balance between range and memory efficiency for medium-range
        /// integer values. Commonly used for audio processing, intermediate
        /// calculations, and applications requiring more range than 8-bit integers.
        /// </remarks>
#pragma warning disable CA1720 // Identifier contains type name - Required for hardware data type naming convention
        Int16 = 1 << 1,
#pragma warning restore CA1720

        /// <summary>
        /// 32-bit signed integer support (-2,147,483,648 to 2,147,483,647).
        /// </summary>
        /// <remarks>
        /// Standard integer type for most applications, providing sufficient range
        /// for array indices, counters, and general-purpose integer arithmetic.
        /// Widely supported across all device types and programming languages.
        /// </remarks>
#pragma warning disable CA1720 // Identifier contains type name - Required for hardware data type naming convention
        Int32 = 1 << 2,
#pragma warning restore CA1720

        /// <summary>
        /// 64-bit signed integer support (-9,223,372,036,854,775,808 to 9,223,372,036,854,775,807).
        /// </summary>
        /// <remarks>
        /// Extended-range integers for large datasets, high-precision counters,
        /// and applications requiring very large numeric ranges. May have
        /// performance implications on some devices due to register pressure.
        /// </remarks>
#pragma warning disable CA1720 // Identifier contains type name - Required for hardware data type naming convention
        Int64 = 1 << 3,
#pragma warning restore CA1720

        /// <summary>
        /// 16-bit IEEE 754 half-precision floating-point support.
        /// </summary>
        /// <remarks>
        /// Compact floating-point format providing significant memory and bandwidth
        /// savings for machine learning, graphics, and applications where reduced
        /// precision is acceptable. Range: approximately ±65,504 with ~3 decimal digits.
        /// </remarks>
        Float16 = 1 << 4,

        /// <summary>
        /// 32-bit IEEE 754 single-precision floating-point support.
        /// </summary>
        /// <remarks>
        /// Standard floating-point type for most scientific and engineering applications.
        /// Provides good balance of range, precision, and performance. Range: approximately
        /// ±3.4×10³⁸ with ~7 decimal digits of precision.
        /// </remarks>
#pragma warning disable CA1720 // Identifier contains type name - Required for hardware data type naming convention
        Float32 = 1 << 5,
#pragma warning restore CA1720

        /// <summary>
        /// 64-bit IEEE 754 double-precision floating-point support.
        /// </summary>
        /// <remarks>
        /// High-precision floating-point for scientific computing, financial calculations,
        /// and applications requiring extended precision. Range: approximately ±1.8×10³⁰⁸
        /// with ~15 decimal digits of precision. May be emulated on some devices.
        /// </remarks>
#pragma warning disable CA1720 // Identifier contains type name - Required for hardware data type naming convention
        Float64 = 1 << 6,
#pragma warning restore CA1720

        /// <summary>
        /// 16-bit Brain Floating Point format support (bfloat16).
        /// </summary>
        /// <remarks>
        /// Specialized floating-point format optimized for machine learning workloads.
        /// Provides the same exponent range as Float32 but with reduced mantissa precision.
        /// Particularly useful for neural network training and inference where gradient
        /// flow is more important than precision.
        /// </remarks>
        BFloat16 = 1 << 7
    }
}
