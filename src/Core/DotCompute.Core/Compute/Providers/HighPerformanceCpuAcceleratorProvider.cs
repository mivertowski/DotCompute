// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using AcceleratorType = DotCompute.Abstractions.AcceleratorType;

namespace DotCompute.Core.Compute.Providers
{
    /// <summary>
    /// High-performance CPU accelerator provider with SIMD optimization and OpenCL kernel execution.
    /// Provides discovery and creation of high-performance CPU accelerators with advanced capability detection.
    /// </summary>
    public class HighPerformanceCpuAcceleratorProvider(ILogger<HighPerformanceCpuAcceleratorProvider> logger) : IAcceleratorProvider
    {
        private readonly ILogger<HighPerformanceCpuAcceleratorProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string Name => "High-Performance CPU";

        /// <summary>
        /// Gets the supported accelerator types.
        /// </summary>
        public AcceleratorType[] SupportedTypes => [AcceleratorType.CPU];

        /// <summary>
        /// Discovers available high-performance CPU accelerators.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Collection of discovered accelerators.</returns>
        public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Discovering high-performance CPU accelerators");

            var cpuInfo = new AcceleratorInfo(
                AcceleratorType.CPU,
                GetProcessorName(),
                "High-Performance v2.0", // driver version
                GetAvailableMemory(),
                Environment.ProcessorCount,
                0, // max clock frequency
                GetSimdCapability(),
                GetAvailableMemory() / 4,
                true // is unified memory
            );

            // Create a high-performance CPU accelerator (delegates to optimized backend when available)
            var accelerator = new Accelerators.HighPerformanceCpuAccelerator(cpuInfo, _logger);
            return ValueTask.FromResult<IEnumerable<IAccelerator>>(new[] { accelerator });
        }

        /// <summary>
        /// Creates a new accelerator instance from the provided information.
        /// </summary>
        /// <param name="info">Accelerator information.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Created accelerator instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the device type is not CPU.</exception>
        public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
        {
            if (info.DeviceType != "CPU")
            {
                throw new ArgumentException("Can only create CPU accelerators", nameof(info));
            }

            var accelerator = new Accelerators.HighPerformanceCpuAccelerator(info, _logger);
            return ValueTask.FromResult<IAccelerator>(accelerator);
        }

        /// <summary>
        /// Gets the processor name from environment variables or creates a default name.
        /// </summary>
        /// <returns>Processor name string.</returns>
        private static string GetProcessorName()
        {
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ??
                   $"{Environment.ProcessorCount}-core CPU";
        }

        /// <summary>
        /// Detects SIMD capability by checking for various instruction sets.
        /// </summary>
        /// <returns>Version representing the highest supported SIMD capability.</returns>
        private static Version GetSimdCapability()
        {
            if (Avx512F.IsSupported)
            {
                return new Version(5, 1, 2);
            }

            if (Avx2.IsSupported)
            {
                return new Version(2, 0);
            }

            if (Sse41.IsSupported)
            {
                return new Version(1, 4);
            }

            if (global::System.Numerics.Vector.IsHardwareAccelerated)
            {
                return new Version(1, 0);
            }

            return new Version(0, 1);
        }

        /// <summary>
        /// Estimates available memory based on working set and GC information.
        /// </summary>
        /// <returns>Estimated available memory in bytes.</returns>
        private static long GetAvailableMemory()
        {
            try
            {
                var workingSet = Environment.WorkingSet;
                var totalMemory = GC.GetTotalMemory(false);
                return Math.Max(workingSet * 4, totalMemory * 16);
            }
            catch
            {
                return 8L * 1024 * 1024 * 1024; // 8GB fallback
            }
        }
    }
}