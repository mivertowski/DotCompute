// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace DotCompute.Plugins.Platform
{

    /// <summary>
    /// Comprehensive platform and hardware capability detection utility.
    /// Provides runtime detection of platform features, hardware capabilities, and compute backends.
    /// </summary>
    public static class PlatformDetection
    {
        private static readonly Lazy<PlatformInfo> _platformInfo = new(DetectPlatformInfo);
        private static readonly Lazy<HardwareCapabilities> _hardwareCapabilities = new(DetectHardwareCapabilities);

        /// <summary>
        /// Gets comprehensive information about the current platform.
        /// </summary>
        public static PlatformInfo Current => _platformInfo.Value;

        /// <summary>
        /// Gets detected hardware capabilities for the current system.
        /// </summary>
        public static HardwareCapabilities Hardware => _hardwareCapabilities.Value;

        /// <summary>
        /// Determines if the specified compute backend is available on the current platform.
        /// </summary>
        public static bool IsBackendAvailable(ComputeBackendType backendType) => backendType switch
        {
            ComputeBackendType.CPU => true, // CPU is always available
            ComputeBackendType.CUDA => IsCudaAvailable(),
            ComputeBackendType.Metal => IsMetalAvailable(),
            ComputeBackendType.OpenCL => IsOpenClAvailable(),
            ComputeBackendType.DirectCompute => IsDirectComputeAvailable(),
            ComputeBackendType.Vulkan => IsVulkanAvailable(),
            _ => false
        };

        /// <summary>
        /// Gets the recommended compute backend for the current platform and hardware.
        /// </summary>
        public static ComputeBackendType GetRecommendedBackend()
        {
            var platform = Current;
            var hardware = Hardware;

            // Metal is preferred on Apple platforms with GPU
            if (platform.IsMacOS && hardware.HasGpu && IsMetalAvailable())
            {
                return ComputeBackendType.Metal;
            }

            // CUDA is preferred on systems with NVIDIA GPUs
            if (hardware.HasNvidiaGpu && IsCudaAvailable())
            {
                return ComputeBackendType.CUDA;
            }

            // DirectCompute on Windows with DirectX support
            if (platform.IsWindows && hardware.HasGpu && IsDirectComputeAvailable())
            {
                return ComputeBackendType.DirectCompute;
            }

            // OpenCL as cross-platform GPU fallback
            if (hardware.HasGpu && IsOpenClAvailable())
            {
                return ComputeBackendType.OpenCL;
            }

            // CPU fallback with SIMD optimization
            return ComputeBackendType.CPU;
        }

        /// <summary>
        /// Validates backend availability and throws appropriate exceptions for unsupported configurations.
        /// </summary>
        public static void ValidateBackendAvailability(ComputeBackendType backendType)
        {
            switch (backendType)
            {
                case ComputeBackendType.Metal:
                    if (!IsMetalAvailable())
                    {
                        throw new PlatformNotSupportedException(
                            "Metal backend is only available on macOS and iOS with Metal framework support. " +
                            $"Current platform: {Current.OperatingSystem} {Current.Architecture}");
                    }
                    break;

                case ComputeBackendType.CUDA:
                    if (!IsCudaAvailable())
                    {
                        var reason = Current.IsWindows || Current.IsLinux
                            ? "CUDA runtime libraries not found or no NVIDIA GPU detected"
                            : $"CUDA is not supported on {Current.OperatingSystem}";
                        throw new PlatformNotSupportedException(
                            $"CUDA backend is not available: {reason}. " +
                            "Please install NVIDIA CUDA toolkit and ensure compatible GPU is present.");
                    }
                    break;

                case ComputeBackendType.DirectCompute:
                    if (!IsDirectComputeAvailable())
                    {
                        throw new PlatformNotSupportedException(
                            "DirectCompute backend is only available on Windows with DirectX 11+ support. " +
                            $"Current platform: {Current.OperatingSystem} {Current.Architecture}");
                    }
                    break;

                case ComputeBackendType.OpenCL:
                    if (!IsOpenClAvailable())
                    {
                        throw new PlatformNotSupportedException(
                            "OpenCL backend is not available: No OpenCL runtime found or no compatible devices detected. " +
                            "Please install OpenCL drivers for your GPU or CPU.");
                    }
                    break;

                case ComputeBackendType.Vulkan:
                    if (!IsVulkanAvailable())
                    {
                        throw new PlatformNotSupportedException(
                            "Vulkan compute backend is not available: No Vulkan runtime found or no compatible devices detected. " +
                            "Please install Vulkan drivers and runtime.");
                    }
                    break;
            }
        }

        private static PlatformInfo DetectPlatformInfo()
        {
            return new PlatformInfo
            {
                OperatingSystem = GetOperatingSystemName(),
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                IsWindows = OperatingSystem.IsWindows(),
                IsLinux = OperatingSystem.IsLinux(),
                IsMacOS = OperatingSystem.IsMacOS(),
                IsFreeBSD = OperatingSystem.IsFreeBSD(),
                Is64Bit = Environment.Is64BitOperatingSystem,
                ProcessorCount = Environment.ProcessorCount,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                OSDescription = RuntimeInformation.OSDescription,
                OSArchitecture = RuntimeInformation.OSArchitecture,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture
            };
        }

        private static string GetOperatingSystemName()
        {
            if (OperatingSystem.IsWindows())
            {
                return "Windows";
            }

            if (OperatingSystem.IsLinux())
            {
                return "Linux";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "macOS";
            }

            if (OperatingSystem.IsFreeBSD())
            {
                return "FreeBSD";
            }

            if (OperatingSystem.IsAndroid())
            {
                return "Android";
            }

            if (OperatingSystem.IsIOS())
            {
                return "iOS";
            }

            return "Unknown";
        }

        private static HardwareCapabilities DetectHardwareCapabilities()
        {
            return new HardwareCapabilities
            {
                // CPU Features
                SupportsAvx = Avx.IsSupported,
                SupportsAvx2 = Avx2.IsSupported,
                SupportsAvx512F = Avx512F.IsSupported,
                SupportsAvx512Bw = Avx512BW.IsSupported,
                SupportsAvx512Cd = Avx512CD.IsSupported,
                SupportsAvx512Dq = Avx512DQ.IsSupported,
                SupportsAvx512Vl = false, // Avx512VL not available in current .NET
                SupportsSse = Sse.IsSupported,
                SupportsSse2 = Sse2.IsSupported,
                SupportsSse3 = Sse3.IsSupported,
                SupportsSsse3 = Ssse3.IsSupported,
                SupportsSse41 = Sse41.IsSupported,
                SupportsSse42 = Sse42.IsSupported,
                SupportsAes = System.Runtime.Intrinsics.X86.Aes.IsSupported,
                SupportsFma = Fma.IsSupported,
                SupportsPopcnt = Popcnt.IsSupported,
                SupportsLzcnt = Lzcnt.IsSupported,
                SupportsBmi1 = Bmi1.IsSupported,
                SupportsBmi2 = Bmi2.IsSupported,

                // ARM Features (if running on ARM)
                SupportsArmBase = AdvSimd.IsSupported,
                SupportsArmAes = System.Runtime.Intrinsics.Arm.Aes.IsSupported,
                SupportsArmSha1 = Sha1.IsSupported,
                SupportsArmSha256 = Sha256.IsSupported,
                SupportsArmCrc32 = Crc32.IsSupported,
                SupportsArmDp = Dp.IsSupported,
                SupportsArmRdm = Rdm.IsSupported,

                // Vector capabilities
                VectorSizeBytes = System.Numerics.Vector<byte>.Count,
                Vector128IsSupported = Vector128.IsHardwareAccelerated,
                Vector256IsSupported = Vector256.IsHardwareAccelerated,
                Vector512IsSupported = Vector512.IsHardwareAccelerated,

                // GPU Detection
                HasGpu = DetectGpuPresence(),
                HasNvidiaGpu = DetectNvidiaGpu(),
                HasAmdGpu = DetectAmdGpu(),
                HasIntelGpu = DetectIntelGpu(),

                // Memory
                TotalPhysicalMemory = GetTotalPhysicalMemory(),
                AvailablePhysicalMemory = GetAvailablePhysicalMemory(),

                // CPU Info
                ProcessorCount = Environment.ProcessorCount
            };
        }

        #region Backend Detection Methods

        private static bool IsMetalAvailable()
        {
            // Metal is only available on Apple platforms
            if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsIOS())
            {
                return false;
            }

            try
            {
                // On macOS, check for Metal framework availability
                if (OperatingSystem.IsMacOS())
                {
                    return CheckMacOSMetalAvailability();
                }

                // iOS Metal detection would go here
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsCudaAvailable()
        {
            // CUDA is only supported on Windows and Linux x64
            if (!Environment.Is64BitOperatingSystem ||
                !(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
            {
                return false;
            }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return CheckWindowsCudaAvailability();
                }
                else if (OperatingSystem.IsLinux())
                {
                    return CheckLinuxCudaAvailability();
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsOpenClAvailable()
        {
            try
            {
                // Check for OpenCL runtime libraries
                if (OperatingSystem.IsWindows())
                {
                    return CheckWindowsOpenClAvailability();
                }
                else if (OperatingSystem.IsLinux())
                {
                    return CheckLinuxOpenClAvailability();
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return CheckMacOSOpenClAvailability();
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDirectComputeAvailable()
        {
            // DirectCompute is Windows-only
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                return CheckDirectComputeAvailability();
            }
            catch
            {
                return false;
            }
        }

        private static bool IsVulkanAvailable()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return CheckWindowsVulkanAvailability();
                }
                else if (OperatingSystem.IsLinux())
                {
                    return CheckLinuxVulkanAvailability();
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return CheckMacOSVulkanAvailability();
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Platform-specific Detection Implementation

        private static bool CheckMacOSMetalAvailability()
        {
            // Check if Metal framework is available
            // This is a basic check - in a real implementation you might use P/Invoke to Metal APIs
            try
            {
                var metalFrameworkPath = "/System/Library/Frameworks/Metal.framework";
                return Directory.Exists(metalFrameworkPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckWindowsCudaAvailability()
        {
            try
            {
                // Check for NVIDIA driver and CUDA runtime
                var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                // Check for NVIDIA Management Library (nvml.dll)
                var nvmlPath = Path.Combine(systemDirectory, "nvml.dll");
                if (File.Exists(nvmlPath))
                {
                    return true;
                }

                // Check for CUDA runtime libraries
                var cudartFiles = Directory.GetFiles(systemDirectory, "cudart64_*.dll");
                if (cudartFiles.Length > 0)
                {
                    return true;
                }

                // Check CUDA installation directory
                var cudaPath = Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA");
                if (Directory.Exists(cudaPath))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckLinuxCudaAvailability()
        {
            try
            {
                // Standard CUDA library locations on Linux
                var cudaLibPaths = new[]
                {
                "/usr/lib/x86_64-linux-gnu/libcuda.so",
                "/usr/lib/x86_64-linux-gnu/libcuda.so.1",
                "/usr/lib64/libcuda.so",
                "/usr/lib64/libcuda.so.1",
                "/usr/local/cuda/lib64/libcudart.so",
                "/usr/local/cuda/lib64/libcuda.so"
            };

                // Check for CUDA libraries
                if (cudaLibPaths.Any(File.Exists))
                {
                    return true;
                }

                // Check for NVIDIA driver
                if (Directory.Exists("/proc/driver/nvidia"))
                {
                    return true;
                }

                // Check for NVIDIA device files
                if (File.Exists("/dev/nvidia0") || File.Exists("/dev/nvidiactl"))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckWindowsOpenClAvailability()
        {
            try
            {
                var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var openclPath = Path.Combine(systemDirectory, "OpenCL.dll");
                return File.Exists(openclPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckLinuxOpenClAvailability()
        {
            try
            {
                var openclPaths = new[]
                {
                "/usr/lib/x86_64-linux-gnu/libOpenCL.so",
                "/usr/lib/x86_64-linux-gnu/libOpenCL.so.1",
                "/usr/lib64/libOpenCL.so",
                "/usr/lib64/libOpenCL.so.1",
                "/usr/local/lib/libOpenCL.so",
                "/opt/intel/opencl/lib64/libOpenCL.so"
            };

                return openclPaths.Any(File.Exists);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckMacOSOpenClAvailability()
        {
            try
            {
                // OpenCL framework on macOS
                var openclFrameworkPath = "/System/Library/Frameworks/OpenCL.framework";
                return Directory.Exists(openclFrameworkPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckDirectComputeAvailability()
        {
            try
            {
                // Check for D3D11.dll and other DirectX components
                var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var d3d11Path = Path.Combine(systemDirectory, "d3d11.dll");
                var dxgiPath = Path.Combine(systemDirectory, "dxgi.dll");

                return File.Exists(d3d11Path) && File.Exists(dxgiPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckWindowsVulkanAvailability()
        {
            try
            {
                var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var vulkanPath = Path.Combine(systemDirectory, "vulkan-1.dll");
                return File.Exists(vulkanPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckLinuxVulkanAvailability()
        {
            try
            {
                var vulkanPaths = new[]
                {
                "/usr/lib/x86_64-linux-gnu/libvulkan.so",
                "/usr/lib/x86_64-linux-gnu/libvulkan.so.1",
                "/usr/lib64/libvulkan.so",
                "/usr/lib64/libvulkan.so.1"
            };

                return vulkanPaths.Any(File.Exists);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckMacOSVulkanAvailability()
        {
            try
            {
                // Vulkan on macOS typically through MoltenVK
                var vulkanPaths = new[]
                {
                "/usr/local/lib/libvulkan.dylib",
                "/usr/local/lib/libMoltenVK.dylib"
            };

                return vulkanPaths.Any(File.Exists);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region GPU Detection

        private static bool DetectGpuPresence() => DetectNvidiaGpu() || DetectAmdGpu() || DetectIntelGpu();

        private static bool DetectNvidiaGpu()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Check for NVIDIA driver files
                    var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    return File.Exists(Path.Combine(systemDir, "nvapi64.dll")) ||
                           File.Exists(Path.Combine(systemDir, "nvml.dll"));
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Check for NVIDIA driver
                    return Directory.Exists("/proc/driver/nvidia") ||
                           File.Exists("/dev/nvidia0");
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool DetectAmdGpu()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    return File.Exists(Path.Combine(systemDir, "amdvlk64.dll")) ||
                           File.Exists(Path.Combine(systemDir, "atiumd64.dll"));
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Check for AMD GPU device files
                    return Directory.EnumerateFiles("/dev/dri", "card*").Any() ||
                           File.Exists("/dev/kfd");
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool DetectIntelGpu()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    return File.Exists(Path.Combine(systemDir, "igdumdim64.dll")) ||
                           File.Exists(Path.Combine(systemDir, "intel_gfx_api-x64.dll"));
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Intel GPU typically appears as i915
                    return Directory.EnumerateFiles("/dev/dri", "card*").Any();
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Memory Detection

        private static long GetTotalPhysicalMemory()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Use GC.GetTotalMemory as approximation - in real implementation use Win32 APIs
                    return GC.GetTotalMemory(false);
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Parse /proc/meminfo
                    var meminfo = File.ReadAllText("/proc/meminfo");
                    var lines = meminfo.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                            {
                                return kb * 1024; // Convert KB to bytes
                            }
                        }
                    }
                }

                // Fallback
                return Environment.WorkingSet;
            }
            catch
            {
                return Environment.WorkingSet;
            }
        }

        private static long GetAvailablePhysicalMemory()
        {
            try
            {
                if (OperatingSystem.IsLinux())
                {
                    var meminfo = File.ReadAllText("/proc/meminfo");
                    var lines = meminfo.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                            {
                                return kb * 1024;
                            }
                        }
                    }
                }

                // Fallback
                return GC.GetTotalMemory(false);
            }
            catch
            {
                return GC.GetTotalMemory(false);
            }
        }

        #endregion
    }

    /// <summary>
    /// Comprehensive platform information.
    /// </summary>
    public class PlatformInfo
    {
        /// <summary>
        /// Gets or sets the operating system.
        /// </summary>
        /// <value>The operating system.</value>
        public required string OperatingSystem { get; init; }
        /// <summary>
        /// Gets or sets the architecture.
        /// </summary>
        /// <value>The architecture.</value>
        public required string Architecture { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether windows.
        /// </summary>
        /// <value>The is windows.</value>
        public required bool IsWindows { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether linux.
        /// </summary>
        /// <value>The is linux.</value>
        public required bool IsLinux { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether mac o s.
        /// </summary>
        /// <value>The is mac o s.</value>
        public required bool IsMacOS { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether free b s d.
        /// </summary>
        /// <value>The is free b s d.</value>
        public required bool IsFreeBSD { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether 64 bit.
        /// </summary>
        /// <value>The is64 bit.</value>
        public required bool Is64Bit { get; init; }
        /// <summary>
        /// Gets or sets the processor count.
        /// </summary>
        /// <value>The processor count.</value>
        public required int ProcessorCount { get; init; }
        /// <summary>
        /// Gets or sets the framework description.
        /// </summary>
        /// <value>The framework description.</value>
        public required string FrameworkDescription { get; init; }
        /// <summary>
        /// Gets or sets the runtime identifier.
        /// </summary>
        /// <value>The runtime identifier.</value>
        public required string RuntimeIdentifier { get; init; }
        /// <summary>
        /// Gets or sets the o s description.
        /// </summary>
        /// <value>The o s description.</value>
        public required string OSDescription { get; init; }
        /// <summary>
        /// Gets or sets the o s architecture.
        /// </summary>
        /// <value>The o s architecture.</value>
        public required Architecture OSArchitecture { get; init; }
        /// <summary>
        /// Gets or sets the process architecture.
        /// </summary>
        /// <value>The process architecture.</value>
        public required Architecture ProcessArchitecture { get; init; }
    }

    /// <summary>
    /// Detected hardware capabilities.
    /// </summary>
    public class HardwareCapabilities
    {
        /// <summary>
        /// Gets or sets the supports avx.
        /// </summary>
        /// <value>The supports avx.</value>
        // x86/x64 SIMD Capabilities
        public required bool SupportsAvx { get; init; }
        /// <summary>
        /// Gets or sets the supports avx2.
        /// </summary>
        /// <value>The supports avx2.</value>
        public required bool SupportsAvx2 { get; init; }
        /// <summary>
        /// Gets or sets the supports avx512 f.
        /// </summary>
        /// <value>The supports avx512 f.</value>
        public required bool SupportsAvx512F { get; init; }
        /// <summary>
        /// Gets or sets the supports avx512 bw.
        /// </summary>
        /// <value>The supports avx512 bw.</value>
        public required bool SupportsAvx512Bw { get; init; }
        /// <summary>
        /// Gets or sets the supports avx512 cd.
        /// </summary>
        /// <value>The supports avx512 cd.</value>
        public required bool SupportsAvx512Cd { get; init; }
        /// <summary>
        /// Gets or sets the supports avx512 dq.
        /// </summary>
        /// <value>The supports avx512 dq.</value>
        public required bool SupportsAvx512Dq { get; init; }
        /// <summary>
        /// Gets or sets the supports avx512 vl.
        /// </summary>
        /// <value>The supports avx512 vl.</value>
        public required bool SupportsAvx512Vl { get; init; }
        /// <summary>
        /// Gets or sets the supports sse.
        /// </summary>
        /// <value>The supports sse.</value>
        public required bool SupportsSse { get; init; }
        /// <summary>
        /// Gets or sets the supports sse2.
        /// </summary>
        /// <value>The supports sse2.</value>
        public required bool SupportsSse2 { get; init; }
        /// <summary>
        /// Gets or sets the supports sse3.
        /// </summary>
        /// <value>The supports sse3.</value>
        public required bool SupportsSse3 { get; init; }
        /// <summary>
        /// Gets or sets the supports ssse3.
        /// </summary>
        /// <value>The supports ssse3.</value>
        public required bool SupportsSsse3 { get; init; }
        /// <summary>
        /// Gets or sets the supports sse41.
        /// </summary>
        /// <value>The supports sse41.</value>
        public required bool SupportsSse41 { get; init; }
        /// <summary>
        /// Gets or sets the supports sse42.
        /// </summary>
        /// <value>The supports sse42.</value>
        public required bool SupportsSse42 { get; init; }
        /// <summary>
        /// Gets or sets the supports aes.
        /// </summary>
        /// <value>The supports aes.</value>
        public required bool SupportsAes { get; init; }
        /// <summary>
        /// Gets or sets the supports fma.
        /// </summary>
        /// <value>The supports fma.</value>
        public required bool SupportsFma { get; init; }
        /// <summary>
        /// Gets or sets the supports popcnt.
        /// </summary>
        /// <value>The supports popcnt.</value>
        public required bool SupportsPopcnt { get; init; }
        /// <summary>
        /// Gets or sets the supports lzcnt.
        /// </summary>
        /// <value>The supports lzcnt.</value>
        public required bool SupportsLzcnt { get; init; }
        /// <summary>
        /// Gets or sets the supports bmi1.
        /// </summary>
        /// <value>The supports bmi1.</value>
        public required bool SupportsBmi1 { get; init; }
        /// <summary>
        /// Gets or sets the supports bmi2.
        /// </summary>
        /// <value>The supports bmi2.</value>
        public required bool SupportsBmi2 { get; init; }
        /// <summary>
        /// Gets or sets the supports arm base.
        /// </summary>
        /// <value>The supports arm base.</value>

        // ARM SIMD Capabilities
        public required bool SupportsArmBase { get; init; }
        /// <summary>
        /// Gets or sets the supports arm aes.
        /// </summary>
        /// <value>The supports arm aes.</value>
        public required bool SupportsArmAes { get; init; }
        /// <summary>
        /// Gets or sets the supports arm sha1.
        /// </summary>
        /// <value>The supports arm sha1.</value>
        public required bool SupportsArmSha1 { get; init; }
        /// <summary>
        /// Gets or sets the supports arm sha256.
        /// </summary>
        /// <value>The supports arm sha256.</value>
        public required bool SupportsArmSha256 { get; init; }
        /// <summary>
        /// Gets or sets the supports arm crc32.
        /// </summary>
        /// <value>The supports arm crc32.</value>
        public required bool SupportsArmCrc32 { get; init; }
        /// <summary>
        /// Gets or sets the supports arm dp.
        /// </summary>
        /// <value>The supports arm dp.</value>
        public required bool SupportsArmDp { get; init; }
        /// <summary>
        /// Gets or sets the supports arm rdm.
        /// </summary>
        /// <value>The supports arm rdm.</value>
        public required bool SupportsArmRdm { get; init; }
        /// <summary>
        /// Gets or sets the vector size bytes.
        /// </summary>
        /// <value>The vector size bytes.</value>

        // Vector Capabilities
        public required int VectorSizeBytes { get; init; }
        /// <summary>
        /// Gets or sets the vector128 is supported.
        /// </summary>
        /// <value>The vector128 is supported.</value>
        public required bool Vector128IsSupported { get; init; }
        /// <summary>
        /// Gets or sets the vector256 is supported.
        /// </summary>
        /// <value>The vector256 is supported.</value>
        public required bool Vector256IsSupported { get; init; }
        /// <summary>
        /// Gets or sets the vector512 is supported.
        /// </summary>
        /// <value>The vector512 is supported.</value>
        public required bool Vector512IsSupported { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether gpu.
        /// </summary>
        /// <value>The has gpu.</value>

        // GPU Capabilities
        public required bool HasGpu { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether nvidia gpu.
        /// </summary>
        /// <value>The has nvidia gpu.</value>
        public required bool HasNvidiaGpu { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether amd gpu.
        /// </summary>
        /// <value>The has amd gpu.</value>
        public required bool HasAmdGpu { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether intel gpu.
        /// </summary>
        /// <value>The has intel gpu.</value>
        public required bool HasIntelGpu { get; init; }
        /// <summary>
        /// Gets or sets the total physical memory.
        /// </summary>
        /// <value>The total physical memory.</value>

        // Memory
        public required long TotalPhysicalMemory { get; init; }
        /// <summary>
        /// Gets or sets the available physical memory.
        /// </summary>
        /// <value>The available physical memory.</value>
        public required long AvailablePhysicalMemory { get; init; }
        /// <summary>
        /// Gets or sets the processor count.
        /// </summary>
        /// <value>The processor count.</value>

        // CPU Info
        public required int ProcessorCount { get; init; }
    }
    /// <summary>
    /// An compute backend type enumeration.
    /// </summary>

    /// <summary>
    /// Supported compute backend types.
    /// </summary>
    public enum ComputeBackendType
    {
        CPU,
        CUDA,
        Metal,
        OpenCL,
        DirectCompute,
        Vulkan
    }
}
