// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Extensions
{
    /// <summary>
    /// Extension methods for AcceleratorInfo to provide backward compatibility
    /// and additional properties required by tests and legacy code.
    /// </summary>
    public static class AcceleratorInfoExtensions
    {
        #region Memory Properties

        /// <summary>
        /// Gets the global memory size in bytes. Maps to existing GlobalMemorySize property.
        /// </summary>
        public static long GlobalMemoryBytes(this AcceleratorInfo info) => info.GlobalMemorySize;

        /// <summary>
        /// Gets the shared memory per block. Maps to existing MaxSharedMemoryPerBlock property.
        /// </summary>
        public static long SharedMemoryPerBlock(this AcceleratorInfo info) => info.MaxSharedMemoryPerBlock;

        /// <summary>
        /// Gets the L2 cache size in bytes. Provides reasonable default based on device type.
        /// </summary>
        public static long L2CacheSize(this AcceleratorInfo info)
        {
            // L2 cache size estimates based on typical GPU architectures
            if (info.DeviceType == "CPU")
            {

                return 8 * 1024 * 1024; // 8 MB for CPUs
            }

            // For GPUs, estimate based on compute capability or use reasonable defaults

            if (info.ComputeCapability != null)
            {
                var major = info.ComputeCapability.Major;
                return major switch
                {
                    >= 8 => 40 * 1024 * 1024, // 40 MB for RTX 30/40 series
                    7 => 6 * 1024 * 1024,      // 6 MB for RTX 20 series
                    6 => 4 * 1024 * 1024,      // 4 MB for GTX 10 series
                    _ => 2 * 1024 * 1024       // 2 MB default
                };
            }


            return 6 * 1024 * 1024; // 6 MB default for unknown GPUs
        }

        /// <summary>
        /// Gets the memory bandwidth in GB/s. Estimated based on device characteristics.
        /// </summary>
        public static double MemoryBandwidthGBps(this AcceleratorInfo info)
        {
            if (info.DeviceType == "CPU")
            {

                return 100.0; // Typical DDR4/DDR5 bandwidth
            }

            // GPU memory bandwidth estimates based on compute capability

            if (info.ComputeCapability != null)
            {
                var major = info.ComputeCapability.Major;
                return major switch
                {
                    >= 8 => 900.0,  // RTX 30/40 series range
                    7 => 500.0,     // RTX 20 series range
                    6 => 400.0,     // GTX 10 series range
                    _ => 300.0      // Older architectures
                };
            }

            return 500.0; // Default estimate
        }

        /// <summary>
        /// Gets the memory bus width in bits. Estimated based on device type.
        /// </summary>
        public static int MemoryBusWidth(this AcceleratorInfo info)
        {
            if (info.DeviceType == "CPU")
            {

                return 128; // Typical CPU memory bus width
            }

            // GPU memory bus width estimates

            if (info.ComputeCapability != null)
            {
                var major = info.ComputeCapability.Major;
                return major switch
                {
                    >= 8 => 384,  // Modern high-end GPUs
                    7 => 256,     // RTX 20 series
                    6 => 256,     // GTX 10 series
                    _ => 128      // Older or lower-end GPUs
                };
            }

            return 256; // Default estimate
        }

        /// <summary>
        /// Gets the memory clock rate in MHz. Estimated based on device type.
        /// </summary>
        public static int MemoryClockRate(this AcceleratorInfo info)
        {
            if (info.DeviceType == "CPU")
            {

                return 3200; // DDR4-3200 equivalent
            }

            // GPU memory clock estimates

            if (info.ComputeCapability != null)
            {
                var major = info.ComputeCapability.Major;
                return major switch
                {
                    >= 8 => 19000,  // GDDR6X speeds
                    7 => 14000,     // GDDR6 speeds
                    6 => 8000,      // GDDR5X speeds
                    _ => 6000       // GDDR5 speeds
                };
            }

            return 14000; // Default GDDR6 estimate
        }

        #endregion

        #region Compute Properties

        /// <summary>
        /// Gets the multiprocessor count. Maps to existing MaxComputeUnits property.
        /// </summary>
        public static int MultiprocessorCount(this AcceleratorInfo info) => info.MaxComputeUnits;

        /// <summary>
        /// Gets the estimated number of CUDA cores based on compute capability and multiprocessor count.
        /// </summary>
        public static int EstimatedCudaCores(this AcceleratorInfo info)
        {
            if (info.DeviceType == "CPU")
            {

                return info.MaxComputeUnits; // CPU cores
            }

            // CUDA cores per SM based on compute capability

            var coresPerSM = info.ComputeCapability?.Major switch
            {
                8 => 128,  // Ampere architecture
                7 => info.ComputeCapability.Minor == 5 ? 64 : 128, // Turing
                6 => info.ComputeCapability.Minor == 0 ? 64 : 128, // Pascal
                5 => 128,  // Maxwell
                3 => 192,  // Kepler
                _ => 128   // Default estimate
            };

            return info.MaxComputeUnits * coresPerSM;
        }

        /// <summary>
        /// Gets the warp size. Standard CUDA warp size is 32.
        /// </summary>
        public static int WarpSize(this AcceleratorInfo info) => info.DeviceType == "CPU" ? 1 : 32; // CUDA warp size is always 32

        /// <summary>
        /// Gets the clock rate in MHz. Maps to existing MaxClockFrequency or provides estimate.
        /// </summary>
        public static int ClockRate(this AcceleratorInfo info)
        {
            if (info.MaxClockFrequency > 0)
            {

                return info.MaxClockFrequency;
            }

            // Provide reasonable defaults based on device type

            if (info.DeviceType == "CPU")
            {

                return 3000; // 3 GHz typical
            }

            // GPU clock rate estimates

            if (info.ComputeCapability != null)
            {
                var major = info.ComputeCapability.Major;
                return major switch
                {
                    >= 8 => 1700,  // RTX 30/40 series
                    7 => 1500,     // RTX 20 series
                    6 => 1300,     // GTX 10 series
                    _ => 1000      // Older architectures
                };
            }

            return 1500; // Default estimate
        }

        #endregion

        #region Architecture and Feature Properties

        /// <summary>
        /// Gets the architecture generation string based on compute capability.
        /// </summary>
        public static string ArchitectureGeneration(this AcceleratorInfo info)
        {
            if (info.DeviceType == "CPU")
            {

                return "x86_64";
            }


            if (info.ComputeCapability == null)
            {

                return "Unknown";
            }


            var major = info.ComputeCapability.Major;
            return major switch
            {
                9 => "Hopper",
                8 => info.ComputeCapability.Minor == 9 ? "Ada Lovelace" : "Ampere",
                7 => info.ComputeCapability.Minor >= 5 ? "Turing" : "Volta",
                6 => "Pascal",
                5 => "Maxwell",
                3 => "Kepler",
                _ => $"Compute_{major}.{info.ComputeCapability.Minor}"
            };
        }

        /// <summary>
        /// Determines if this is an RTX 2000 Ada series GPU based on compute capability and name.
        /// </summary>
        public static bool IsRTX2000Ada(this AcceleratorInfo info)
        {
            if (info.ComputeCapability?.Major == 8 && info.ComputeCapability?.Minor == 9)
            {
                return info.Name.Contains("RTX", StringComparison.OrdinalIgnoreCase) &&
                       info.Name.Contains("2000", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// Determines if ECC memory is enabled. Estimated based on device type and tier.
        /// </summary>
        public static bool IsECCEnabled(this AcceleratorInfo info)
        {
            // ECC is typically available on professional/data center GPUs
            return info.Name.Contains("Tesla", StringComparison.OrdinalIgnoreCase) ||
                   info.Name.Contains("Quadro", StringComparison.OrdinalIgnoreCase) ||
                   info.Name.Contains("A100", StringComparison.OrdinalIgnoreCase) ||
                   info.Name.Contains("V100", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Feature Support Properties

        /// <summary>
        /// Determines if the device supports concurrent kernel execution.
        /// </summary>
        public static bool SupportsConcurrentKernels(this AcceleratorInfo info)
        {
            if (info.DeviceType == "CPU")
            {

                return true; // CPUs support concurrent execution
            }

            // Most modern GPUs support concurrent kernels

            return info.ComputeCapability?.Major >= 6;
        }

        /// <summary>
        /// Determines if the device supports managed (unified) memory.
        /// </summary>
        public static bool SupportsManagedMemory(this AcceleratorInfo info) => info.IsUnifiedMemory || info.ComputeCapability?.Major >= 6;

        /// <summary>
        /// Determines if the device supports ray tracing operations.
        /// </summary>
        public static bool SupportsRayTracing(this AcceleratorInfo info)
        {
            // Ray tracing support started with Turing (7.5) and Ada Lovelace
            if (info.ComputeCapability?.Major == 7 && info.ComputeCapability?.Minor >= 5)
            {
                return true;
            }


            if (info.ComputeCapability?.Major >= 8)
            {

                return true;
            }


            return false;
        }

        /// <summary>
        /// Determines if the device supports tensor operations (Tensor Cores).
        /// </summary>
        public static bool SupportsTensorOperations(this AcceleratorInfo info)
            // Tensor Core support started with Volta (7.0)

            => info.ComputeCapability?.Major >= 7;

        /// <summary>
        /// Legacy alias for IsUnifiedMemory property.
        /// </summary>
        public static bool SupportsUnifiedMemory(this AcceleratorInfo info) => info.IsUnifiedMemory;

        #endregion

        #region Compute Capability Extensions

        /// <summary>
        /// Gets the major version of compute capability. Provides safe access with defaults.
        /// </summary>
        public static int ComputeCapabilityMajor(this AcceleratorInfo info) => info.ComputeCapability?.Major ?? (info.DeviceType == "CPU" ? 0 : 6);

        /// <summary>
        /// Gets the minor version of compute capability. Provides safe access with defaults.
        /// </summary>
        public static int ComputeCapabilityMinor(this AcceleratorInfo info) => info.ComputeCapability?.Minor ?? 0;

        #endregion
    }
}