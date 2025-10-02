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
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        Success = 0,
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        InvalidValue = 1,
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        OutOfMemory = 2,
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        NotInitialized = 3,
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        Deinitialized = 4,
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        NoDevice = 100
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
    public struct CudaDeviceProperties : IEquatable<CudaDeviceProperties>
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public string DeviceName { get; set; }
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public int Major { get; set; }
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public int Minor { get; set; }
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public long TotalGlobalMem { get; set; }
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public int MultiProcessorCount { get; set; }
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public readonly bool Equals(CudaDeviceProperties other)
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return DeviceName == other.DeviceName &&
                   Major == other.Major &&
                   Minor == other.Minor &&
                   TotalGlobalMem == other.TotalGlobalMem &&
                   MultiProcessorCount == other.MultiProcessorCount;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public readonly override bool Equals(object? obj)
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return obj is CudaDeviceProperties other && Equals(other);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public readonly override int GetHashCode()
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return HashCode.Combine(DeviceName, Major, Minor, TotalGlobalMem, MultiProcessorCount);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public static bool operator ==(CudaDeviceProperties left, CudaDeviceProperties right)
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return left.Equals(right);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public static bool operator !=(CudaDeviceProperties left, CudaDeviceProperties right)
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return !left.Equals(right);
        }
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
    public static class CudaRuntime
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable XDOC001 // Missing XML documentation
        public static CudaError cudaGetDeviceCount(out int count)
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            count = 0;
            return CudaError.NoDevice;
        }
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable XDOC001 // Missing XML documentation
        public static CudaError cudaDriverGetVersion(out int version)
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            version = 0;
            return CudaError.Success;
        }
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable XDOC001 // Missing XML documentation
        public static CudaError cudaRuntimeGetVersion(out int version)
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            version = 0;
            return CudaError.Success;
        }
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable XDOC001 // Missing XML documentation
        public static CudaError cudaGetDeviceProperties(ref CudaDeviceProperties props, int device)
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            props = default;
            return CudaError.NoDevice;
        }
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable XDOC001 // Missing XML documentation
        public static string GetErrorString(CudaError error) => error.ToString();
#pragma warning restore XDOC001 // Missing XML documentation
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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
