// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Configuration
{
    /// <summary>
    /// Centralized manager for CUDA compute capability detection and normalization.
    /// Handles driver/hardware compatibility issues and architecture capping.
    /// </summary>
    public static class CudaCapabilityManager
    {
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
                            _logger.LogInformation(
                                "Device reports compute capability {DeviceMajor}.{DeviceMinor}, " +
                                "capped to {CappedMajor}.{CappedMinor} for driver compatibility",
                                major, minor, capped.major, capped.minor);
                        }
                        else
                        {
                            _logger.LogDebug("Using device compute capability {Major}.{Minor}", major, minor);
                        }
                        
                        return capped;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to detect device compute capability, using fallback");
                }

                // Fallback to a safe default
                var fallback = GetFallbackCapability();
                _cachedCapability = fallback;
                _logger.LogInformation("Using fallback compute capability {Major}.{Minor}", fallback.major, fallback.minor);
                return fallback;
            }
        }

        /// <summary>
        /// Applies driver-specific capping to compute capability.
        /// Different CUDA driver versions support different maximum architectures.
        /// CRITICAL: CUDA 13.0 has incomplete support for sm_89 - force fallback to sm_86
        /// </summary>
        private static (int major, int minor) ApplyDriverCompatibilityCapping(int major, int minor)
        {
            // CRITICAL FIX: CUDA 13.0 driver 581.15 has incomplete support for sm_89
            // Force fallback to sm_86 (Ampere) which has mature driver support
            if (major == 8 && minor == 9)
            {
                _logger.LogWarning(
                    "CUDA 13.0 has incomplete support for sm_89 (Ada Lovelace). " +
                    "Falling back to sm_86 (Ampere) for stable compilation. " +
                    "Original capability: {Major}.{Minor}, using: 8.6",
                    major, minor);
                return (8, 6);
            }
            
            // Detect CUDA driver version to determine maximum supported capability
            var maxCapability = GetMaxSupportedCapability();
            
            // Cap at maximum supported
            if (major > maxCapability.major || (major == maxCapability.major && minor > maxCapability.minor))
            {
                _logger.LogDebug(
                    "Capping capability from {Major}.{Minor} to {MaxMajor}.{MaxMinor} based on driver support",
                    major, minor, maxCapability.major, maxCapability.minor);
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
                    
                    _logger.LogDebug("CUDA Driver version: {Major}.{Minor}", majorVersion, minorVersion);
                    
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
                _logger.LogWarning(ex, "Failed to detect CUDA driver version");
            }
            
            // For CUDA 13+ systems, default to sm_89 (Ada) as it's widely supported
            // For older systems, sm_86 is a safe fallback
            // Check if we're on a CUDA 13 system by looking for cuda-13 installation
            if (System.IO.Directory.Exists("/usr/local/cuda-13.0") || 
                System.IO.Directory.Exists("/usr/local/cuda") && 
                System.IO.File.Exists("/usr/local/cuda/version.json"))
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
            if (System.IO.Directory.Exists("/usr/local/cuda-13.0") || 
                System.IO.Directory.Exists("/usr/local/cuda"))
            {
                return (8, 9);  // CUDA 13 supports Ada (sm_89)
            }
            
            // sm_86 (Ampere) is a good fallback for older systems
            return (8, 6);
        }

        /// <summary>
        /// Converts compute capability to architecture string for compilation.
        /// </summary>
        public static string GetArchitectureString((int major, int minor) capability)
        {
            return $"compute_{capability.major}{capability.minor}";
        }

        /// <summary>
        /// Converts compute capability to SM string for PTX.
        /// </summary>
        public static string GetSmString((int major, int minor) capability)
        {
            return $"sm_{capability.major}{capability.minor}";
        }

        /// <summary>
        /// Clears the cached capability, forcing re-detection on next access.
        /// Useful for testing or after driver updates.
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                _cachedCapability = null;
                _logger.LogDebug("Cleared compute capability cache");
            }
        }

        /// <summary>
        /// Gets the PTX version compatible with the given compute capability.
        /// CRITICAL: Updated for CUDA 13.0 compatibility with conservative PTX versions
        /// </summary>
        public static string GetCompatiblePtxVersion((int major, int minor) capability)
        {
            // Map compute capability to PTX version
            // Based on NVIDIA PTX ISA documentation for CUDA 13 with conservative choices
            return (capability.major, capability.minor) switch
            {
                (9, 0) => "8.0",  // sm_90 (Hopper) - Use conservative PTX version for CUDA 13.0
                (8, 9) => "8.0",  // sm_89 (Ada) - CRITICAL: Use PTX 8.0 for better CUDA 13.0 compatibility
                (8, 6) or (8, 7) => "8.0",  // sm_86/87 (Ampere) - Unified to PTX 8.0
                (8, 0) => "7.8",  // sm_80 (Ampere)
                (7, 5) => "7.5",  // sm_75 (Turing)
                (7, 0) or (7, 2) => "7.0",  // sm_70/72 (Volta)
                (6, _) => "6.5",  // sm_6x (Pascal)
                (5, _) => "5.0",  // sm_5x (Maxwell)
                _ => "7.8"        // Conservative default that works well with CUDA 13.0
            };
        }
    }
}