// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Configuration
{
    /// <summary>
    /// Centralized manager for CUDA compute capability detection and normalization.
    /// Handles driver/hardware compatibility issues and architecture capping.
    /// </summary>
    public static partial class CudaCapabilityManager
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 5800,
            Level = LogLevel.Warning,
            Message = "Failed to detect device compute capability, using fallback")]
        private static partial void LogCapabilityDetectionFailure(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 5801,
            Level = LogLevel.Warning,
            Message = "CUDA compatibility mode enabled: Forcing sm_89 (Ada Lovelace RTX 2000) to use sm_86 target for maximum driver compatibility. Set DOTCOMPUTE_FORCE_COMPATIBILITY_MODE=false to use native capability. Device capability: {DeviceMajor}.{DeviceMinor}, using compilation target: 8.6")]
        private static partial void LogCompatibilityModeForced(ILogger logger, int deviceMajor, int deviceMinor);

        [LoggerMessage(
            EventId = 5802,
            Level = LogLevel.Information,
            Message = "Device reports compute capability {DeviceMajor}.{DeviceMinor}, capped to {CappedMajor}.{CappedMinor} for driver compatibility")]
        private static partial void LogCapabilityCapped(ILogger logger, int deviceMajor, int deviceMinor, int cappedMajor, int cappedMinor);

        [LoggerMessage(
            EventId = 5803,
            Level = LogLevel.Debug,
            Message = "Using device compute capability {Major}.{Minor}")]
        private static partial void LogUsingDeviceCapability(ILogger logger, int major, int minor);

        [LoggerMessage(
            EventId = 5804,
            Level = LogLevel.Information,
            Message = "Using fallback compute capability {Major}.{Minor}")]
        private static partial void LogUsingFallbackCapability(ILogger logger, int major, int minor);

        [LoggerMessage(
            EventId = 5805,
            Level = LogLevel.Information,
            Message = "CUDA compatibility mode: Mapping sm_{DeviceMajor}{DeviceMinor} to sm_86 for consistent NVRTC compilation behavior")]
        private static partial void LogCompatibilityModeMapping(ILogger logger, int deviceMajor, int deviceMinor);

        [LoggerMessage(
            EventId = 5806,
            Level = LogLevel.Debug,
            Message = "Using native compute capability {Major}.{Minor} for optimal performance. Set DOTCOMPUTE_FORCE_COMPATIBILITY_MODE=true if compilation issues occur.")]
        private static partial void LogNativeCapabilityUsage(ILogger logger, int major, int minor);

        [LoggerMessage(
            EventId = 5807,
            Level = LogLevel.Debug,
            Message = "Capping capability from {Major}.{Minor} to {MaxMajor}.{MaxMinor} based on driver support")]
        private static partial void LogCapabilityCappingByDriver(ILogger logger, int major, int minor, int maxMajor, int maxMinor);

        [LoggerMessage(
            EventId = 5808,
            Level = LogLevel.Debug,
            Message = "CUDA Driver version: {Major}.{Minor}")]
        private static partial void LogDriverVersion(ILogger logger, int major, int minor);

        [LoggerMessage(
            EventId = 5809,
            Level = LogLevel.Warning,
            Message = "Failed to detect CUDA driver version")]
        private static partial void LogDriverVersionDetectionFailed(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 5810,
            Level = LogLevel.Debug,
            Message = "Cleared compute capability cache")]
        private static partial void LogCacheCleared(ILogger logger);

        #endregion
        private static readonly ILogger _logger;
        private static (int major, int minor)? _cachedCapability;
        private static readonly object _lock = new();

        static CudaCapabilityManager()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger("CudaCapabilityManager");
        }

        /// <summary>
        /// Gets the target compute capability for compilation, with appropriate capping for driver compatibility.
        /// This is the SINGLE source of truth for compute capability in the entire CUDA backend.
        /// </summary>
        public static (int major, int minor) GetTargetComputeCapability()
        {
            lock (_lock)
            {
                if (_cachedCapability.HasValue)
                {
                    return _cachedCapability.Value;
                }

                try
                {
                    // Try to get actual device compute capability
                    var result = CudaRuntime.cudaGetDevice(out var deviceId);
                    if (result == CudaError.Success)
                    {
                        var (major, minor) = ComputeCapability.ParseFromDevice(deviceId);

                        // Apply capping based on driver capabilities

                        var capped = ApplyDriverCompatibilityCapping(major, minor);


                        _cachedCapability = capped;


                        if (capped != (major, minor))
                        {
                            LogCapabilityCapped(_logger, major, minor, capped.major, capped.minor);
                        }
                        else
                        {
                            LogUsingDeviceCapability(_logger, major, minor);
                        }


                        return capped;
                    }
                }
                catch (Exception ex)
                {
                    LogCapabilityDetectionFailure(_logger, ex);
                }

                // Fallback to a safe default
                var fallback = GetFallbackCapability();
                _cachedCapability = fallback;
                LogUsingFallbackCapability(_logger, fallback.major, fallback.minor);
                return fallback;
            }
        }

        /// <summary>
        /// Applies driver-specific capping to compute capability with configurable fallback behavior.
        /// Different CUDA driver versions support different maximum architectures.
        /// </summary>
        private static (int major, int minor) ApplyDriverCompatibilityCapping(int major, int minor)
        {
            // Check environment variable for forcing compatibility mode
            var forceCompatibilityMode = Environment.GetEnvironmentVariable("DOTCOMPUTE_FORCE_COMPATIBILITY_MODE");
            var shouldForceCompatibility = string.Equals(forceCompatibilityMode, "true", StringComparison.OrdinalIgnoreCase);


            if (shouldForceCompatibility)
            {
                // Force fallback mode for maximum compatibility when explicitly requested
                if (major == 8 && minor == 9)
                {
                    LogCompatibilityModeForced(_logger, major, minor);
                    return (8, 6);
                }

                // Additional Ada Lovelace variants in compatibility mode

                if (major == 8 && minor >= 7) // sm_87, sm_89 and future Ada variants
                {
                    LogCompatibilityModeMapping(_logger, major, minor);
                    return (8, 6);
                }
            }
            else
            {
                // Production mode: Use actual device capabilities for optimal performance
                LogNativeCapabilityUsage(_logger, major, minor);
            }

            // Detect CUDA driver version to determine maximum supported capability

            var maxCapability = GetMaxSupportedCapability();

            // Cap at maximum supported by driver

            if (major > maxCapability.major || (major == maxCapability.major && minor > maxCapability.minor))
            {
                LogCapabilityCappingByDriver(_logger, major, minor, maxCapability.major, maxCapability.minor);
                return maxCapability;
            }


            return (major, minor);
        }

        /// <summary>
        /// Determines the maximum compute capability supported by the current CUDA driver.
        /// </summary>
        private static (int major, int minor) GetMaxSupportedCapability()
        {
            try
            {
                // Try to get driver version
                var result = CudaRuntime.cudaDriverGetVersion(out var driverVersion);
                if (result == CudaError.Success)
                {
                    var majorVersion = driverVersion / 1000;
                    var minorVersion = (driverVersion % 1000) / 10;


                    LogDriverVersion(_logger, majorVersion, minorVersion);

                    // Map driver version to maximum supported compute capability
                    // Based on NVIDIA documentation

                    return majorVersion switch
                    {
                        >= 13 => (9, 0),  // CUDA 13.x supports up to sm_90 (Hopper)
                        12 => minorVersion switch
                        {
                            >= 8 => (9, 0),  // CUDA 12.8+ supports sm_90
                            >= 6 => (8, 9),  // CUDA 12.6+ supports sm_89 (Ada)
                            >= 0 => (8, 6),  // CUDA 12.0+ supports sm_86 (Ampere)
                            _ => (8, 0)
                        },
                        11 => (8, 6),  // CUDA 11.x supports up to sm_86
                        10 => (7, 5),  // CUDA 10.x supports up to sm_75
                        _ => (6, 0)    // Older versions
                    };
                }
            }
            catch (Exception ex)
            {
                LogDriverVersionDetectionFailed(_logger, ex);
            }

            // For CUDA 13+ systems, default to sm_89 (Ada) as it's widely supported
            // For older systems, sm_86 is a safe fallback
            // Check if we're on a CUDA 13 system by looking for cuda-13 installation

            if (Directory.Exists("/usr/local/cuda-13.0") ||

                Directory.Exists("/usr/local/cuda") &&

                File.Exists("/usr/local/cuda/version.json"))
            {
                return (8, 9);  // CUDA 13 supports Ada (sm_89)
            }

            // Conservative default for older systems

            return (8, 6);
        }

        /// <summary>
        /// Gets a safe fallback compute capability when detection fails.
        /// </summary>
        private static (int major, int minor) GetFallbackCapability()
        {
            // Check for CUDA 13 installation
            if (Directory.Exists("/usr/local/cuda-13.0") ||

                Directory.Exists("/usr/local/cuda"))
            {
                return (8, 9);  // CUDA 13 supports Ada (sm_89)
            }

            // sm_86 (Ampere) is a good fallback for older systems

            return (8, 6);
        }

        /// <summary>
        /// Converts compute capability to architecture string for compilation.
        /// </summary>
        public static string GetArchitectureString((int major, int minor) capability) => $"compute_{capability.major}{capability.minor}";

        /// <summary>
        /// Converts compute capability to SM string for PTX.
        /// </summary>
        public static string GetSmString((int major, int minor) capability) => $"sm_{capability.major}{capability.minor}";

        /// <summary>
        /// Clears the cached capability, forcing re-detection on next access.
        /// Useful for testing or after driver updates.
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                _cachedCapability = null;
                LogCacheCleared(_logger);
            }
        }

        /// <summary>
        /// Gets the PTX version compatible with the given compute capability.
        /// CRITICAL: Updated for CUDA 13.0 compatibility with conservative PTX versions
        /// </summary>
        public static string GetCompatiblePtxVersion((int major, int minor) capability)
        {
            // Map compute capability to PTX version with CUDA 13.0 optimizations
            // Using conservative PTX versions to ensure maximum driver compatibility
            return (capability.major, capability.minor) switch
            {
                (9, 0) => "7.8",  // sm_90 (Hopper) - Conservative PTX 7.8 for CUDA 13.0 compatibility
                (8, 9) => "7.8",  // sm_89 (Ada) - CRITICAL: PTX 7.8 is most stable with CUDA 13.0 driver 581.15
                (8, 7) => "7.8",  // sm_87 (Ada) - Conservative mapping to proven PTX version
                (8, 6) => "7.8",  // sm_86 (Ampere) - PTX 7.8 works reliably across CUDA versions
                (8, 0) => "7.0",  // sm_80 (A100) - Proven stable version
                (7, 5) => "7.0",  // sm_75 (Turing) - Mature PTX support
                (7, 0) or (7, 2) => "6.5",  // sm_70/72 (Volta) - Conservative for older arch
                (6, _) => "6.0",  // sm_6x (Pascal) - Mature support
                (5, _) => "5.0",  // sm_5x (Maxwell) - Legacy support
                _ => "7.0"        // Conservative default - PTX 7.0 is widely supported in CUDA 13.0
            };
        }
    }
}