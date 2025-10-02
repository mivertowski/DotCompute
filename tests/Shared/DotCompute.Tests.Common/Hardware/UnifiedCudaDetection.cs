// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
// Mock CUDA types for testing without CUDA backend dependency

namespace DotCompute.Hardware.Cuda.Tests.TestHelpers
{
    /// <summary>
    /// Mock CUDA types and enums for testing without CUDA backend dependency
    /// </summary>
    public enum CudaError
    {
        Success = 0,
        InvalidValue = 1,
        OutOfMemory = 2,
        NotInitialized = 3,
        Deinitialized = 4,
        NoDevice = 100
    }

    public struct CudaDeviceProperties : IEquatable<CudaDeviceProperties>
    {
        public string DeviceName { get; set; }
        public int Major { get; set; }
        public int Minor { get; set; }
        public long TotalGlobalMem { get; set; }
        public int MultiProcessorCount { get; set; }

        public readonly bool Equals(CudaDeviceProperties other)
        {
            return DeviceName == other.DeviceName &&
                   Major == other.Major &&
                   Minor == other.Minor &&
                   TotalGlobalMem == other.TotalGlobalMem &&
                   MultiProcessorCount == other.MultiProcessorCount;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is CudaDeviceProperties other && Equals(other);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(DeviceName, Major, Minor, TotalGlobalMem, MultiProcessorCount);
        }

        public static bool operator ==(CudaDeviceProperties left, CudaDeviceProperties right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CudaDeviceProperties left, CudaDeviceProperties right)
        {
            return !left.Equals(right);
        }
    }

    public static class CudaRuntime
    {
        public static CudaError cudaGetDeviceCount(out int count)
        {
            count = 0;
            return CudaError.NoDevice;
        }
        public static CudaError cudaDriverGetVersion(out int version)
        {
            version = 0;
            return CudaError.Success;
        }
        public static CudaError cudaRuntimeGetVersion(out int version)
        {
            version = 0;
            return CudaError.Success;
        }
        public static CudaError cudaGetDeviceProperties(ref CudaDeviceProperties props, int device)
        {
            props = default;
            return CudaError.NoDevice;
        }
        public static string GetErrorString(CudaError error) => error.ToString();
    }

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
#pragma warning disable CA1508
                return _cachedDiagnosticInfo ?? "No diagnostic information available";
#pragma warning restore CA1508
            }
        }


        private static bool CheckCudaAvailability()
        {
            var diagnostics = new System.Text.StringBuilder();
            _ = diagnostics.AppendLine("=== CUDA Detection Diagnostics ===");

            // Step 1: Check for CUDA runtime libraries

            var hasRuntimeLibrary = false;
            _ = diagnostics.AppendLine("\n1. CUDA Runtime Library Check:");


            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var windowsLibs = new[] { "cudart64_13.dll", "cudart64_12.dll", "cudart64_11.dll" };
                foreach (var lib in windowsLibs)
                {
                    var exists = File.Exists(lib);
                    _ = diagnostics.AppendLine($"   {lib}: {(exists ? "✓" : "✗")}");
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
                    var exists = File.Exists(path);
                    _ = diagnostics.AppendLine($"   {path}: {(exists ? "✓" : "✗")}");
                    if (exists && !hasRuntimeLibrary)
                    {
                        hasRuntimeLibrary = true;
                    }
                }
            }


            if (!hasRuntimeLibrary)
            {
                _ = diagnostics.AppendLine("\n❌ No CUDA runtime library found");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return false;
            }

            // Step 2: Check CUDA device count using P/Invoke

            _ = diagnostics.AppendLine("\n2. CUDA Device Detection:");


            try
            {
                var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);


                _ = diagnostics.AppendLine($"   cudaGetDeviceCount result: {result}");
                _ = diagnostics.AppendLine($"   Device count: {deviceCount}");


                if (result != CudaError.Success)
                {
                    _ = diagnostics.AppendLine($"   Error: {CudaRuntime.GetErrorString(result)}");
                    _cachedDiagnosticInfo = diagnostics.ToString();
                    return false;
                }


                if (deviceCount == 0)
                {
                    _ = diagnostics.AppendLine("   ❌ No CUDA devices found");
                    _cachedDiagnosticInfo = diagnostics.ToString();
                    return false;
                }

                // Step 3: Get driver and runtime versions

                _ = diagnostics.AppendLine("\n3. CUDA Version Information:");


                if (CudaRuntime.cudaDriverGetVersion(out var driverVersion) == CudaError.Success)
                {
                    _ = diagnostics.AppendLine($"   Driver Version: {FormatCudaVersion(driverVersion)}");
                }


                if (CudaRuntime.cudaRuntimeGetVersion(out var runtimeVersion) == CudaError.Success)
                {
                    _ = diagnostics.AppendLine($"   Runtime Version: {FormatCudaVersion(runtimeVersion)}");
                }

                // Step 4: Get device properties for first device

                _ = diagnostics.AppendLine("\n4. Primary Device Properties:");


                var props = new CudaDeviceProperties();
                if (CudaRuntime.cudaGetDeviceProperties(ref props, 0) == CudaError.Success)
                {
                    _ = diagnostics.AppendLine($"   Device Name: {props.DeviceName}");
                    _ = diagnostics.AppendLine($"   Compute Capability: {props.Major}.{props.Minor}");
                    _ = diagnostics.AppendLine($"   Total Memory: {props.TotalGlobalMem / (1024 * 1024 * 1024):F2} GB");
                    _ = diagnostics.AppendLine($"   Multiprocessors: {props.MultiProcessorCount}");
                }


                _ = diagnostics.AppendLine("\n✓ CUDA is available and functional");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return true;
            }
            catch (DllNotFoundException ex)
            {
                _ = diagnostics.AppendLine($"\n❌ CUDA runtime DLL not found: {ex.Message}");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                _ = diagnostics.AppendLine($"\n❌ CUDA function not found: {ex.Message}");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return false;
            }
            catch (Exception ex)
            {
                _ = diagnostics.AppendLine($"\n❌ Unexpected error: {ex.GetType().Name}: {ex.Message}");
                _cachedDiagnosticInfo = diagnostics.ToString();
                return false;
            }
        }


        private static string FormatCudaVersion(int version)
        {
            var major = version / 1000;
            var minor = (version % 1000) / 10;
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