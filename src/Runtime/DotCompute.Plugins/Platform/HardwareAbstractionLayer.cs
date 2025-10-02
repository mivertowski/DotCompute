// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;

namespace DotCompute.Plugins.Platform
{

    /// <summary>
    /// Hardware abstraction layer that provides unified interface for different compute backends.
    /// Handles automatic backend selection, performance optimization, and graceful degradation.
    /// </summary>
    public class HardwareAbstractionLayer
    {
        private readonly ILogger<HardwareAbstractionLayer> _logger;
        private readonly Dictionary<ComputeBackendType, BackendCapabilityInfo> _backendCapabilities;

        public HardwareAbstractionLayer(ILogger<HardwareAbstractionLayer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backendCapabilities = [];

            AnalyzeHardwareCapabilities();
        }

        /// <summary>
        /// Gets the optimal compute backend configuration for the current hardware.
        /// </summary>
        public ComputeConfiguration GetOptimalConfiguration()
        {
            var platformInfo = PlatformDetection.Current;
            _ = PlatformDetection.Hardware;

            _logger.LogInfoMessage($"Analyzing optimal compute configuration for {platformInfo.OperatingSystem} {platformInfo.Architecture} with {platformInfo.ProcessorCount} cores");

            // Find the best available backend
            var primaryBackend = SelectPrimaryBackend();
            var fallbackBackends = GetFallbackBackends(primaryBackend);

            var config = new ComputeConfiguration
            {
                PrimaryBackend = primaryBackend,
                FallbackBackends = fallbackBackends,
                MaxParallelism = CalculateOptimalParallelism(primaryBackend),
                MemoryConfiguration = GetMemoryConfiguration(),
                SIMDConfiguration = GetSIMDConfiguration(),
                BackendSpecificSettings = GetBackendSpecificSettings(primaryBackend)
            };

            _logger.LogInfoMessage($"Selected configuration: Primary={config.PrimaryBackend}, Fallbacks=[{string.Join(", ", config.FallbackBackends)}], Parallelism={config.MaxParallelism}");

            return config;
        }

        /// <summary>
        /// Validates if a specific backend configuration is supported on the current platform.
        /// </summary>
        public BackendValidationResult ValidateConfiguration(ComputeBackendType backendType)
        {
            var result = new BackendValidationResult
            {
                BackendType = backendType,
                IsSupported = PlatformDetection.IsBackendAvailable(backendType)
            };

            if (!result.IsSupported)
            {
                try
                {
                    PlatformDetection.ValidateBackendAvailability(backendType);
                }
                catch (PlatformNotSupportedException ex)
                {
                    result.ValidationErrors.Add(ex.Message);
                }
            }

            if (result.IsSupported && _backendCapabilities.TryGetValue(backendType, out var capabilities))
            {
                result.EstimatedPerformance = capabilities.RelativePerformance;
                result.MemoryRequirements = capabilities.MemoryRequirements;
                result.SupportedFeatures = [.. capabilities.SupportedFeatures];
            }

            return result;
        }

        /// <summary>
        /// Gets performance benchmarking information for all available backends.
        /// </summary>
        public IReadOnlyDictionary<ComputeBackendType, BackendBenchmark> GetBenchmarkInfo()
        {
            var benchmarks = new Dictionary<ComputeBackendType, BackendBenchmark>();

            foreach (var backendType in Enum.GetValues<ComputeBackendType>())
            {
                if (PlatformDetection.IsBackendAvailable(backendType))
                {
                    benchmarks[backendType] = CreateBenchmarkInfo(backendType);
                }
            }

            return benchmarks;
        }

        private void AnalyzeHardwareCapabilities()
        {
            var platformInfo = PlatformDetection.Current;
            var hardware = PlatformDetection.Hardware;

            _logger.LogDebugMessage("Analyzing hardware capabilities...");
            _logger.LogDebugMessage($"Platform: {platformInfo.OperatingSystem} {platformInfo.Architecture}, CPUs: {platformInfo.ProcessorCount}, Memory: {hardware.TotalPhysicalMemory / (1024 * 1024)} MB");

            // Analyze each backend
            foreach (var backendType in Enum.GetValues<ComputeBackendType>())
            {
                if (PlatformDetection.IsBackendAvailable(backendType))
                {
                    _backendCapabilities[backendType] = AnalyzeBackendCapabilities(backendType);
                    _logger.LogDebugMessage($"Backend {backendType}: Performance={_backendCapabilities[backendType].RelativePerformance}, Memory={_backendCapabilities[backendType].MemoryRequirements / (1024 * 1024)} MB");
                }
            }
        }

        private static BackendCapabilityInfo AnalyzeBackendCapabilities(ComputeBackendType backendType)
        {
            var hardware = PlatformDetection.Hardware;

            return backendType switch
            {
                ComputeBackendType.CPU => new BackendCapabilityInfo
                {
                    RelativePerformance = 1.0f, // Base performance
                    MemoryRequirements = Math.Max(1024 * 1024, hardware.AvailablePhysicalMemory / 4), // 1MB min, 25% of available max
                    SupportedFeatures = GetCpuFeatures(),
                    OptimalWorkgroupSize = Math.Min(1024, hardware.ProcessorCount * 4),
                    MaxMemoryAllocation = hardware.AvailablePhysicalMemory / 2
                },

                ComputeBackendType.CUDA => new BackendCapabilityInfo
                {
                    RelativePerformance = hardware.HasNvidiaGpu ? 10.0f : 0.0f, // 10x CPU performance estimate
                    MemoryRequirements = 512 * 1024 * 1024, // 512MB baseline
                    SupportedFeatures = GetCudaFeatures(),
                    OptimalWorkgroupSize = 256, // CUDA warp size optimization
                    MaxMemoryAllocation = EstimateGpuMemory()
                },

                ComputeBackendType.Metal => new BackendCapabilityInfo
                {
                    RelativePerformance = 8.0f, // Slightly lower than CUDA
                    MemoryRequirements = 256 * 1024 * 1024, // 256MB baseline
                    SupportedFeatures = GetMetalFeatures(),
                    OptimalWorkgroupSize = 128, // Apple GPU optimization
                    MaxMemoryAllocation = EstimateGpuMemory()
                },

                ComputeBackendType.OpenCL => new BackendCapabilityInfo
                {
                    RelativePerformance = hardware.HasGpu ? 6.0f : 2.0f, // GPU or CPU OpenCL
                    MemoryRequirements = 128 * 1024 * 1024, // 128MB baseline
                    SupportedFeatures = GetOpenClFeatures(),
                    OptimalWorkgroupSize = hardware.HasGpu ? 64 : 16,
                    MaxMemoryAllocation = EstimateOpenClMemory()
                },

                ComputeBackendType.DirectCompute => new BackendCapabilityInfo
                {
                    RelativePerformance = 7.0f, // Good performance on Windows
                    MemoryRequirements = 256 * 1024 * 1024, // 256MB baseline
                    SupportedFeatures = GetDirectComputeFeatures(),
                    OptimalWorkgroupSize = 64, // DirectX optimization
                    MaxMemoryAllocation = EstimateGpuMemory()
                },

                ComputeBackendType.Vulkan => new BackendCapabilityInfo
                {
                    RelativePerformance = 9.0f, // High performance, modern API
                    MemoryRequirements = 512 * 1024 * 1024, // 512MB baseline
                    SupportedFeatures = GetVulkanFeatures(),
                    OptimalWorkgroupSize = 128, // Vulkan compute optimization
                    MaxMemoryAllocation = EstimateGpuMemory()
                },

                _ => new BackendCapabilityInfo
                {
                    RelativePerformance = 0.0f,
                    MemoryRequirements = 0,
                    SupportedFeatures = [],
                    OptimalWorkgroupSize = 1,
                    MaxMemoryAllocation = 0
                }
            };
        }

        private ComputeBackendType SelectPrimaryBackend()
        {
            var availableBackends = _backendCapabilities.OrderByDescending(kvp => kvp.Value.RelativePerformance);

            foreach (var (backendType, capabilities) in availableBackends)
            {
                if (capabilities.RelativePerformance > 0)
                {
                    _logger.LogInfoMessage($"Selected {backendType} as primary backend (performance score: {capabilities.RelativePerformance})");
                    return backendType;
                }
            }

            // Fallback to CPU if nothing else is available
            return ComputeBackendType.CPU;
        }

        private List<ComputeBackendType> GetFallbackBackends(ComputeBackendType primaryBackend)
        {
            var fallbacks = new List<ComputeBackendType>();

            // Always include CPU as final fallback
            if (primaryBackend != ComputeBackendType.CPU)
            {
                fallbacks.Add(ComputeBackendType.CPU);
            }

            // Add other available backends as intermediate fallbacks
            var otherBackends = _backendCapabilities
                .Where(kvp => kvp.Key != primaryBackend && kvp.Key != ComputeBackendType.CPU && kvp.Value.RelativePerformance > 0)
                .OrderByDescending(kvp => kvp.Value.RelativePerformance)
                .Select(kvp => kvp.Key)
                .Take(2); // Limit to 2 additional fallbacks

            fallbacks.InsertRange(0, otherBackends);

            return fallbacks;
        }

        private static int CalculateOptimalParallelism(ComputeBackendType backendType)
        {
            var hardware = PlatformDetection.Hardware;

            return backendType switch
            {
                ComputeBackendType.CPU => hardware.ProcessorCount,
                ComputeBackendType.CUDA => 8192, // High parallelism for GPU
                ComputeBackendType.Metal => 4096, // Apple GPU optimization
                ComputeBackendType.OpenCL => hardware.HasGpu ? 2048 : hardware.ProcessorCount,
                ComputeBackendType.DirectCompute => 4096, // DirectX compute shaders
                ComputeBackendType.Vulkan => 8192, // High parallelism for modern GPUs
                _ => hardware.ProcessorCount
            };
        }

        private static MemoryConfiguration GetMemoryConfiguration()
        {
            var hardware = PlatformDetection.Hardware;

            return new MemoryConfiguration
            {
                TotalSystemMemory = hardware.TotalPhysicalMemory,
                AvailableMemory = hardware.AvailablePhysicalMemory,
                RecommendedMaxAllocation = Math.Min(hardware.AvailablePhysicalMemory / 2, 4L * 1024 * 1024 * 1024), // 50% of available or 4GB max
                PageSize = 4096, // Standard page size
                AllocationAlignment = 64, // Cache line alignment
                UseMemoryPools = hardware.TotalPhysicalMemory > 8L * 1024 * 1024 * 1024 // Use pools for systems >8GB
            };
        }

        private static SIMDConfiguration GetSIMDConfiguration()
        {
            var hardware = PlatformDetection.Hardware;

            return new SIMDConfiguration
            {
                PreferredVectorWidth = hardware.VectorSizeBytes,
                SupportedInstructions = GetSupportedSimdInstructions(),
                UseVectorization = true,
                FallbackToScalar = true,
                OptimizeForArch = DetermineArchitectureOptimization()
            };
        }

        private Dictionary<string, object> GetBackendSpecificSettings(ComputeBackendType backendType)
        {
            return backendType switch
            {
                ComputeBackendType.CUDA => new Dictionary<string, object>
                {
                    ["device_id"] = 0,
                    ["memory_pool_size"] = 512 * 1024 * 1024,
                    ["stream_priority"] = "high",
                    ["enable_peer_access"] = true
                },

                ComputeBackendType.OpenCL => new Dictionary<string, object>
                {
                    ["platform_preference"] = "gpu_first",
                    ["work_group_size"] = _backendCapabilities.GetValueOrDefault(backendType)?.OptimalWorkgroupSize ?? 64,
                    ["enable_profiling"] = false
                },

                ComputeBackendType.Metal => new Dictionary<string, object>
                {
                    ["command_buffer_size"] = 64,
                    ["resource_options"] = "storage_mode_shared",
                    ["enable_validation"] = false
                },

                _ => []
            };
        }

        private BackendBenchmark CreateBenchmarkInfo(ComputeBackendType backendType)
        {
            var capabilities = _backendCapabilities.GetValueOrDefault(backendType);

            return new BackendBenchmark
            {
                BackendType = backendType,
                RelativePerformance = capabilities?.RelativePerformance ?? 0.0f,
                MemoryBandwidth = EstimateMemoryBandwidth(backendType),
                ComputeThroughput = EstimateComputeThroughput(backendType),
                Latency = EstimateLatency(backendType),
                PowerEfficiency = EstimatePowerEfficiency(backendType)
            };
        }

        #region Feature Detection Methods

        private static HashSet<string> GetCpuFeatures()
        {
            var hardware = PlatformDetection.Hardware;
            var features = new HashSet<string>();

            if (hardware.SupportsSse)
            {
                _ = features.Add("SSE");
            }

            if (hardware.SupportsSse2)
            {
                _ = features.Add("SSE2");
            }

            if (hardware.SupportsSse3)
            {
                _ = features.Add("SSE3");
            }

            if (hardware.SupportsSsse3)
            {
                _ = features.Add("SSSE3");
            }

            if (hardware.SupportsSse41)
            {
                _ = features.Add("SSE4.1");
            }

            if (hardware.SupportsSse42)
            {
                _ = features.Add("SSE4.2");
            }

            if (hardware.SupportsAvx)
            {
                _ = features.Add("AVX");
            }

            if (hardware.SupportsAvx2)
            {
                _ = features.Add("AVX2");
            }

            if (hardware.SupportsAvx512F)
            {
                _ = features.Add("AVX-512");
            }

            if (hardware.SupportsAes)
            {
                _ = features.Add("AES-NI");
            }

            if (hardware.SupportsFma)
            {
                _ = features.Add("FMA");
            }

            if (hardware.SupportsArmBase)
            {
                _ = features.Add("NEON");
            }

            return features;
        }

        private static HashSet<string> GetCudaFeatures()
        {
            return
            [
                "CUDA_CORES", "TENSOR_CORES", "RT_CORES", "UNIFIED_MEMORY",

            "DYNAMIC_PARALLELISM", "COOPERATIVE_GROUPS", "WARP_FUNCTIONS"
            ];
        }

        private static HashSet<string> GetMetalFeatures()
        {
            return
            [
                "METAL_SHADING_LANGUAGE", "COMPUTE_SHADERS", "INDIRECT_DISPATCH",
            "ARGUMENT_BUFFERS", "RASTER_ORDER_GROUPS", "TILE_SHADERS"
            ];
        }

        private static HashSet<string> GetOpenClFeatures()
        {
            return
            [
                "OPENCL_1_2", "OPENCL_2_0", "OPENCL_2_1", "SUBGROUPS",
            "PIPES", "SVM", "DEVICE_ENQUEUE", "PRINTF"
            ];
        }

        private static HashSet<string> GetDirectComputeFeatures()
        {
            return
            [
                "COMPUTE_SHADERS_5_0", "UAV", "STRUCTURED_BUFFERS",

            "ATOMIC_OPERATIONS", "APPEND_CONSUME_BUFFERS"
            ];
        }

        private static HashSet<string> GetVulkanFeatures()
        {
            return
            [
                "COMPUTE_PIPELINE", "DESCRIPTOR_SETS", "PUSH_CONSTANTS",
            "SUBGROUPS", "VARIABLE_POINTERS", "STORAGE_BUFFER_16BIT"
            ];
        }

        #endregion

        #region Performance Estimation Methods

        private static long EstimateGpuMemory()
            // Conservative estimate - would be better to query actual GPU memory




            => 2L * 1024 * 1024 * 1024; // 2GB default

        private static long EstimateOpenClMemory()
        {
            var hardware = PlatformDetection.Hardware;
            return hardware.HasGpu ? EstimateGpuMemory() : hardware.AvailablePhysicalMemory / 4;
        }

        private static float EstimateMemoryBandwidth(ComputeBackendType backendType)
        {
            return backendType switch
            {
                ComputeBackendType.CPU => 50.0f, // GB/s - typical DDR4
                ComputeBackendType.CUDA => 900.0f, // GB/s - high-end GPU
                ComputeBackendType.Metal => 400.0f, // GB/s - Apple GPU
                ComputeBackendType.OpenCL => 300.0f, // GB/s - varies widely
                ComputeBackendType.DirectCompute => 500.0f, // GB/s - depends on GPU
                ComputeBackendType.Vulkan => 600.0f, // GB/s - modern GPU
                _ => 10.0f
            };
        }

        private static float EstimateComputeThroughput(ComputeBackendType backendType)
        {
            var hardware = PlatformDetection.Hardware;

            return backendType switch
            {
                ComputeBackendType.CPU => hardware.ProcessorCount * 2.5f, // GHz estimate
                ComputeBackendType.CUDA => 15000.0f, // CUDA cores estimate
                ComputeBackendType.Metal => 8000.0f, // Apple GPU cores
                ComputeBackendType.OpenCL => hardware.HasGpu ? 10000.0f : hardware.ProcessorCount * 2.0f,
                ComputeBackendType.DirectCompute => 12000.0f, // DirectX compute units
                ComputeBackendType.Vulkan => 16000.0f, // Modern GPU compute units
                _ => 1.0f
            };
        }

        private static float EstimateLatency(ComputeBackendType backendType)
        {
            return backendType switch
            {
                ComputeBackendType.CPU => 0.1f, // ms - very low latency
                ComputeBackendType.CUDA => 2.0f, // ms - GPU dispatch overhead
                ComputeBackendType.Metal => 1.5f, // ms - Apple GPU efficiency
                ComputeBackendType.OpenCL => 3.0f, // ms - varies by implementation
                ComputeBackendType.DirectCompute => 2.5f, // ms - DirectX overhead
                ComputeBackendType.Vulkan => 1.0f, // ms - low-level API
                _ => 10.0f
            };
        }

        private static float EstimatePowerEfficiency(ComputeBackendType backendType)
        {
            return backendType switch
            {
                ComputeBackendType.CPU => 3.0f, // GFLOPS/W
                ComputeBackendType.CUDA => 15.0f, // GFLOPS/W - good efficiency
                ComputeBackendType.Metal => 25.0f, // GFLOPS/W - Apple optimization
                ComputeBackendType.OpenCL => 12.0f, // GFLOPS/W - varies
                ComputeBackendType.DirectCompute => 10.0f, // GFLOPS/W
                ComputeBackendType.Vulkan => 18.0f, // GFLOPS/W - efficient API
                _ => 1.0f
            };
        }

        private static List<string> GetSupportedSimdInstructions()
        {
            var hardware = PlatformDetection.Hardware;
            var instructions = new List<string>();

            if (hardware.SupportsSse)
            {
                instructions.Add("SSE");
            }

            if (hardware.SupportsSse2)
            {
                instructions.Add("SSE2");
            }

            if (hardware.SupportsAvx)
            {
                instructions.Add("AVX");
            }

            if (hardware.SupportsAvx2)
            {
                instructions.Add("AVX2");
            }

            if (hardware.SupportsAvx512F)
            {
                instructions.Add("AVX512F");
            }

            if (hardware.SupportsArmBase)
            {
                instructions.Add("NEON");
            }

            return instructions;
        }

        private static string DetermineArchitectureOptimization()
        {
            var platform = PlatformDetection.Current;

            return platform.ProcessArchitecture switch
            {
                global::System.Runtime.InteropServices.Architecture.X64 => "x86_64",
                global::System.Runtime.InteropServices.Architecture.Arm64 => "aarch64",
                global::System.Runtime.InteropServices.Architecture.X86 => "x86",
                global::System.Runtime.InteropServices.Architecture.Arm => "arm",
                _ => "generic"
            };
        }

        #endregion
    }

    /// <summary>
    /// Configuration information for optimal compute setup.
    /// </summary>
    public class ComputeConfiguration
    {
        public required ComputeBackendType PrimaryBackend { get; init; }
        public required List<ComputeBackendType> FallbackBackends { get; init; }
        public required int MaxParallelism { get; init; }
        public required MemoryConfiguration MemoryConfiguration { get; init; }
        public required SIMDConfiguration SIMDConfiguration { get; init; }
        public required Dictionary<string, object> BackendSpecificSettings { get; init; }
    }

    /// <summary>
    /// Memory configuration parameters.
    /// </summary>
    public class MemoryConfiguration
    {
        public required long TotalSystemMemory { get; init; }
        public required long AvailableMemory { get; init; }
        public required long RecommendedMaxAllocation { get; init; }
        public required int PageSize { get; init; }
        public required int AllocationAlignment { get; init; }
        public required bool UseMemoryPools { get; init; }
    }

    /// <summary>
    /// SIMD configuration parameters.
    /// </summary>
    public class SIMDConfiguration
    {
        public required int PreferredVectorWidth { get; init; }
        public required List<string> SupportedInstructions { get; init; }
        public required bool UseVectorization { get; init; }
        public required bool FallbackToScalar { get; init; }
        public required string OptimizeForArch { get; init; }
    }

    /// <summary>
    /// Backend capability analysis results.
    /// </summary>
    public class BackendCapabilityInfo
    {
        public required float RelativePerformance { get; init; }
        public required long MemoryRequirements { get; init; }
        public required HashSet<string> SupportedFeatures { get; init; }
        public required int OptimalWorkgroupSize { get; init; }
        public required long MaxMemoryAllocation { get; init; }
    }

    /// <summary>
    /// Backend validation results.
    /// </summary>
    public class BackendValidationResult
    {
        public required ComputeBackendType BackendType { get; init; }
        public required bool IsSupported { get; init; }
        public float EstimatedPerformance { get; set; }
        public long MemoryRequirements { get; set; }
        public List<string> SupportedFeatures { get; set; } = [];
        public List<string> ValidationErrors { get; set; } = [];
    }

    /// <summary>
    /// Backend benchmark information.
    /// </summary>
    public class BackendBenchmark
    {
        public required ComputeBackendType BackendType { get; init; }
        public required float RelativePerformance { get; init; }
        public required float MemoryBandwidth { get; init; } // GB/s
        public required float ComputeThroughput { get; init; } // GFLOPS estimate
        public required float Latency { get; init; } // ms
        public required float PowerEfficiency { get; init; } // GFLOPS/W
    }
}
