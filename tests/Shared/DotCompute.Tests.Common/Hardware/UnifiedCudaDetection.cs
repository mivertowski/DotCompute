// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Hardware.Cuda.Tests.TestHelpers
{
    /// <summary>
    /// Unified CUDA detection helper for consistent CUDA availability checks across all tests.
    /// Provides a single source of truth for CUDA detection with comprehensive diagnostics.
    /// </summary>
    public static class UnifiedCudaDetection
    {
        private static readonly object _lockObject = new();
        private static bool? _cachedAvailability;
        private static string? _cachedDiagnosticInfo;


        /// <summary>
        /// Checks if CUDA is available on the system with detailed diagnostics.
        /// </summary>
        /// <returns>True if CUDA is available and functional, false otherwise.</returns>
        public static bool IsCudaAvailable()
        {
            lock (_lockObject)
            {
                if (_cachedAvailability.HasValue)
                {
                    return _cachedAvailability.Value;
                }


                _cachedAvailability = CheckCudaAvailability();
                return _cachedAvailability.Value;
            }
        }


        /// <summary>
        /// Gets detailed diagnostic information about CUDA availability.
        /// </summary>
        /// <returns>Diagnostic string with details about CUDA detection.</returns>
        public static string GetDiagnosticInfo()
        {
            lock (_lockObject)
            {
                if (_cachedDiagnosticInfo != null)
                {
                    return _cachedDiagnosticInfo;
                }


                _ = IsCudaAvailable(); // Ensure detection has run
                return _cachedDiagnosticInfo ?? "No diagnostic information available";
            }
        }


        private static bool CheckCudaAvailability()
        {
            var diagnostics = new System.Text.StringBuilder();
            diagnostics.AppendLine("=== CUDA Detection Diagnostics ===");

            // Step 1: Check for CUDA runtime libraries

            bool hasRuntimeLibrary = false;
            diagnostics.AppendLine("\n1. CUDA Runtime Library Check:");


            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var windowsLibs = new[] { "cudart64_13.dll", "cudart64_12.dll", "cudart64_11.dll" };
                foreach (var lib in windowsLibs)
                {
                    bool exists = File.Exists(lib);
                    diagnostics.AppendLine($"   {lib}: {(exists ? "✓" : "✗")}");
                    if (exists && !hasRuntimeLibrary)
                    {
                        hasRuntimeLibrary = true;
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var linuxPaths = new[]
                {
                    "/usr/local/cuda/targets/x86_64-linux/lib/libcudart.so.13",
                    "/usr/local/cuda/targets/x86_64-linux/lib/libcudart.so",
                    "/usr/local/cuda-13.0/targets/x86_64-linux/lib/libcudart.so.13.0.48",
                    "/usr/local/cuda/lib64/libcudart.so",
                    "/usr/lib/x86_64-linux-gnu/libcudart.so",
                    "/usr/lib/wsl/lib/libcudart.so.1",
                    "/usr/local/cuda-12/targets/x86_64-linux/lib/libcudart.so.12"
                };


                foreach (var path in linuxPaths)
                {
                    bool exists = File.Exists(path);
                    diagnostics.AppendLine($"   {path}: {(exists ? "✓" : "✗")}");
                    if (exists && !hasRuntimeLibrary)
                    {
                        hasRuntimeLibrary = true;
                    }
                }
            }


            if (!hasRuntimeLibrary)
            {
                diagnostics.AppendLine("\n❌ No CUDA runtime library found");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return false;
            }

            // Step 2: Check CUDA device count using P/Invoke

            diagnostics.AppendLine("\n2. CUDA Device Detection:");


            try
            {
                int deviceCount = 0;
                var result = CudaRuntime.cudaGetDeviceCount(out deviceCount);


                diagnostics.AppendLine($"   cudaGetDeviceCount result: {result}");
                diagnostics.AppendLine($"   Device count: {deviceCount}");


                if (result != CudaError.Success)
                {
                    diagnostics.AppendLine($"   Error: {CudaRuntime.GetErrorString(result)}");
                    _cachedDiagnosticInfo = diagnostics.ToString();
                    return false;
                }


                if (deviceCount == 0)
                {
                    diagnostics.AppendLine("   ❌ No CUDA devices found");
                    _cachedDiagnosticInfo = diagnostics.ToString();
                    return false;
                }

                // Step 3: Get driver and runtime versions

                diagnostics.AppendLine("\n3. CUDA Version Information:");


                if (CudaRuntime.cudaDriverGetVersion(out var driverVersion) == CudaError.Success)
                {
                    diagnostics.AppendLine($"   Driver Version: {FormatCudaVersion(driverVersion)}");
                }


                if (CudaRuntime.cudaRuntimeGetVersion(out var runtimeVersion) == CudaError.Success)
                {
                    diagnostics.AppendLine($"   Runtime Version: {FormatCudaVersion(runtimeVersion)}");
                }

                // Step 4: Get device properties for first device

                diagnostics.AppendLine("\n4. Primary Device Properties:");


                var props = new CudaDeviceProperties();
                if (CudaRuntime.cudaGetDeviceProperties(ref props, 0) == CudaError.Success)
                {
                    diagnostics.AppendLine($"   Device Name: {props.DeviceName}");
                    diagnostics.AppendLine($"   Compute Capability: {props.Major}.{props.Minor}");
                    diagnostics.AppendLine($"   Total Memory: {props.TotalGlobalMem / (1024 * 1024 * 1024):F2} GB");
                    diagnostics.AppendLine($"   Multiprocessors: {props.MultiProcessorCount}");
                }


                diagnostics.AppendLine("\n✓ CUDA is available and functional");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return true;
            }
            catch (DllNotFoundException ex)
            {
                diagnostics.AppendLine($"\n❌ CUDA runtime DLL not found: {ex.Message}");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                diagnostics.AppendLine($"\n❌ CUDA function not found: {ex.Message}");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return false;
            }
            catch (Exception ex)
            {
                diagnostics.AppendLine($"\n❌ Unexpected error: {ex.GetType().Name}: {ex.Message}");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return false;
            }
        }


        private static string FormatCudaVersion(int version)
        {
            int major = version / 1000;
            int minor = (version % 1000) / 10;
            return $"{major}.{minor}";
        }


        /// <summary>
        /// Clears the cached availability status, forcing a fresh check on next call.
        /// </summary>
        public static void ClearCache()
        {
            lock (_lockObject)
            {
                _cachedAvailability = null;
                _cachedDiagnosticInfo = null;
            }
        }
    }
}