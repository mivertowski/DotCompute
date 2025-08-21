// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Device.Types
{
    /// <summary>
    /// Defines the types of compute devices available in the DotCompute framework.
    /// Each device type has different characteristics, capabilities, and optimization strategies.
    /// </summary>
    /// <remarks>
    /// The device type determines the compilation target, execution model, and available features.
    /// Different device types may support different kernel languages and optimization techniques.
    /// </remarks>
    public enum ComputeDeviceType
    {
        /// <summary>
        /// Central Processing Unit (CPU) device.
        /// </summary>
        /// <remarks>
        /// CPU devices provide general-purpose computing with high single-threaded performance.
        /// They typically support complex control flow, large caches, and sophisticated 
        /// branch prediction. Best suited for tasks requiring irregular memory access patterns,
        /// complex algorithms, or when debugging is needed.
        /// </remarks>
        CPU,

        /// <summary>
        /// Graphics Processing Unit (GPU) device.
        /// </summary>
        /// <remarks>
        /// GPU devices excel at massively parallel computations with thousands of cores.
        /// They provide high memory bandwidth and are optimized for data-parallel workloads.
        /// Best suited for mathematical operations, image processing, machine learning,
        /// and scientific computing tasks that can be parallelized effectively.
        /// </remarks>
        GPU,

        /// <summary>
        /// Field-Programmable Gate Array (FPGA) device.
        /// </summary>
        /// <remarks>
        /// FPGA devices offer customizable hardware acceleration with ultra-low latency.
        /// They can be reconfigured to implement specialized computational circuits.
        /// Best suited for high-frequency trading, real-time signal processing,
        /// custom algorithms, and applications requiring deterministic timing.
        /// </remarks>
        FPGA,

        /// <summary>
        /// Custom hardware accelerator device.
        /// </summary>
        /// <remarks>
        /// Accelerator devices include specialized processors like TPUs, DSPs, or custom ASICs.
        /// They are optimized for specific computational domains or algorithms.
        /// Examples include tensor processing units for AI, digital signal processors
        /// for audio/video processing, or custom chips for cryptocurrency mining.
        /// </remarks>
        Accelerator,

        /// <summary>
        /// Virtual compute device for testing and simulation.
        /// </summary>
        /// <remarks>
        /// Virtual devices simulate hardware behavior without requiring physical hardware.
        /// They are useful for testing, debugging, profiling, and development scenarios
        /// where the target hardware is not available. Performance characteristics
        /// can be configured to match real hardware for accurate testing.
        /// </remarks>
        Virtual
    }
}