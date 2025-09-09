using System.Diagnostics;
using System.Runtime.InteropServices;
#if WINDOWS
using System.Management;
#endif

namespace DotCompute.Tests.Common.Helpers;

/// <summary>
/// Provides utilities for detecting available hardware capabilities for testing purposes.
/// Helps determine which tests can be executed based on available hardware resources.
/// </summary>
public static class HardwareDetection
{
    private static readonly Lazy<HardwareInfo> _hardwareInfo = new(DetectHardware);


    /// <summary>
    /// Gets cached hardware information.
    /// </summary>
    public static HardwareInfo Info => _hardwareInfo.Value;

    #region GPU Detection


    /// <summary>
    /// Checks if CUDA-capable GPUs are available.
    /// </summary>
    public static bool IsCudaAvailable()
    {
        try
        {
            // Check for NVIDIA drivers and CUDA runtime
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CheckWindowsCuda();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return CheckLinuxCuda();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return false; // CUDA not supported on macOS
            }


            return false;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// Checks if OpenCL is available.
    /// </summary>
    public static bool IsOpenCLAvailable()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CheckWindowsOpenCL();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return CheckLinuxOpenCL();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return CheckMacOSOpenCL();
            }


            return false;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// Gets the number of available CUDA devices.
    /// </summary>
    public static int GetCudaDeviceCount()
    {
        if (!IsCudaAvailable())
            return 0;


        try
        {
            // This would typically use CUDA API calls
            // For now, we'll use a heuristic approach. TODO: Use proper CUDA device enumeration API
            return GetNvidiaGpuCount();
        }
        catch
        {
            return 0;
        }
    }


    /// <summary>
    /// Gets the number of available OpenCL devices.
    /// </summary>
    public static int GetOpenCLDeviceCount()
    {
        if (!IsOpenCLAvailable())
            return 0;


        try
        {
            // This would typically enumerate OpenCL platforms and devices
            // For now, we'll return a conservative estimate. TODO: Use proper OpenCL device enumeration
            return Math.Max(GetNvidiaGpuCount() + GetAmdGpuCount() + GetIntelGpuCount(), 1);
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region CPU Detection


    /// <summary>
    /// Gets the number of logical CPU cores.
    /// </summary>
    public static int GetLogicalCoreCount() => Environment.ProcessorCount;


    /// <summary>
    /// Gets the number of physical CPU cores.
    /// </summary>
    public static int GetPhysicalCoreCount()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsPhysicalCores();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxPhysicalCores();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSPhysicalCores();
            }
        }
        catch
        {
            // Fallback to logical cores
        }


        return GetLogicalCoreCount();
    }


    /// <summary>
    /// Checks if the CPU supports AVX instructions.
    /// </summary>
    public static bool IsAvxSupported()
    {
        try
        {
            return System.Runtime.Intrinsics.X86.Avx.IsSupported;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// Checks if the CPU supports AVX2 instructions.
    /// </summary>
    public static bool IsAvx2Supported()
    {
        try
        {
            return System.Runtime.Intrinsics.X86.Avx2.IsSupported;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// Checks if the CPU supports AVX-512 instructions.
    /// </summary>
    public static bool IsAvx512Supported()
    {
        try
        {
            return System.Runtime.Intrinsics.X86.Avx512F.IsSupported;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Memory Detection


    /// <summary>
    /// Gets the total system memory in bytes.
    /// </summary>
    public static long GetTotalSystemMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsTotalMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxTotalMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSTotalMemory();
            }
        }
        catch
        {
            // Fallback to GC estimate
        }


        return GC.GetTotalMemory(false);
    }


    /// <summary>
    /// Gets available system memory in bytes.
    /// </summary>
    public static long GetAvailableSystemMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsAvailableMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxAvailableMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSAvailableMemory();
            }
        }
        catch
        {
            // Conservative estimate
        }


        return GetTotalSystemMemory() / 2; // Conservative estimate
    }

    #endregion

    #region Hardware Information Class


    /// <summary>
    /// Contains comprehensive hardware information.
    /// </summary>
    public class HardwareInfo
    {
        /// <summary>
        /// Gets a value indicating whether [cuda available].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [cuda available]; otherwise, <c>false</c>.
        /// </value>
        public bool CudaAvailable { get; init; }

        /// <summary>
        /// Gets a value indicating whether [open cl available].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [open cl available]; otherwise, <c>false</c>.
        /// </value>
        public bool OpenCLAvailable { get; init; }

        /// <summary>
        /// Gets the cuda device count.
        /// </summary>
        /// <value>
        /// The cuda device count.
        /// </value>
        public int CudaDeviceCount { get; init; }

        /// <summary>
        /// Gets the open cl device count.
        /// </summary>
        /// <value>
        /// The open cl device count.
        /// </value>
        public int OpenCLDeviceCount { get; init; }

        /// <summary>
        /// Gets the logical cores.
        /// </summary>
        /// <value>
        /// The logical cores.
        /// </value>
        public int LogicalCores { get; init; }

        /// <summary>
        /// Gets the physical cores.
        /// </summary>
        /// <value>
        /// The physical cores.
        /// </value>
        public int PhysicalCores { get; init; }

        /// <summary>
        /// Gets a value indicating whether [avx supported].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [avx supported]; otherwise, <c>false</c>.
        /// </value>
        public bool AvxSupported { get; init; }

        /// <summary>
        /// Gets a value indicating whether [avx2 supported].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [avx2 supported]; otherwise, <c>false</c>.
        /// </value>
        public bool Avx2Supported { get; init; }

        /// <summary>
        /// Gets a value indicating whether [avx512 supported].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [avx512 supported]; otherwise, <c>false</c>.
        /// </value>
        public bool Avx512Supported { get; init; }

        /// <summary>
        /// Gets the total memory.
        /// </summary>
        /// <value>
        /// The total memory.
        /// </value>
        public long TotalMemory { get; init; }

        /// <summary>
        /// Gets the available memory.
        /// </summary>
        /// <value>
        /// The available memory.
        /// </value>
        public long AvailableMemory { get; init; }

        /// <summary>
        /// Gets the platform.
        /// </summary>
        /// <value>
        /// The platform.
        /// </value>
        public string Platform { get; init; } = string.Empty;

        /// <summary>
        /// Gets the architecture.
        /// </summary>
        /// <value>
        /// The architecture.
        /// </value>
        public string Architecture { get; init; } = string.Empty;

        /// <summary>
        /// Gets the gpu names.
        /// </summary>
        /// <value>
        /// The gpu names.
        /// </value>
        public List<string> GpuNames { get; init; } = [];


        /// <summary>
        /// Gets the name of the cpu.
        /// </summary>
        /// <value>
        /// The name of the cpu.
        /// </value>
        public string CpuName { get; init; } = string.Empty;
    }

    #endregion

    #region Private Implementation Methods


    private static HardwareInfo DetectHardware()
    {
        return new HardwareInfo
        {
            CudaAvailable = IsCudaAvailable(),
            OpenCLAvailable = IsOpenCLAvailable(),
            CudaDeviceCount = GetCudaDeviceCount(),
            OpenCLDeviceCount = GetOpenCLDeviceCount(),
            LogicalCores = GetLogicalCoreCount(),
            PhysicalCores = GetPhysicalCoreCount(),
            AvxSupported = IsAvxSupported(),
            Avx2Supported = IsAvx2Supported(),
            Avx512Supported = IsAvx512Supported(),
            TotalMemory = GetTotalSystemMemory(),
            AvailableMemory = GetAvailableSystemMemory(),
            Platform = GetPlatformString(),
            Architecture = GetArchitectureString(),
            GpuNames = GetGpuNames(),
            CpuName = GetCpuName()
        };
    }


    private static string GetPlatformString()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";
        return "Unknown";
    }


    private static string GetArchitectureString() => RuntimeInformation.ProcessArchitecture.ToString();


    private static List<string> GetGpuNames()
    {
        var gpuNames = new List<string>();


        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                gpuNames.AddRange(GetWindowsGpuNames());
            }
            // Add Linux and macOS implementations as needed
        }
        catch
        {
            // Ignore errors in GPU name detection
        }


        return gpuNames;
    }


    private static string GetCpuName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsCpuName();
            }
            // Add Linux and macOS implementations as needed
        }
        catch
        {
            // Ignore errors in CPU name detection
        }


        return "Unknown CPU";
    }

    #region Platform-Specific Implementations


    private static bool CheckWindowsCuda()
    {
        try
        {
#if WINDOWS
            // Check for NVIDIA display driver
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController WHERE Name LIKE '%NVIDIA%'");
            var hasNvidiaGpu = searcher.Get().Cast<ManagementObject>().Any();
#else
            var hasNvidiaGpu = false; // TODO: Implement GPU detection for non-Windows platforms
#endif


            if (!hasNvidiaGpu)
                return false;

            // Check for CUDA runtime libraries

            var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
            if (!string.IsNullOrEmpty(cudaPath))
            {
                return Directory.Exists(cudaPath);
            }

            // Check common CUDA installation paths

            var commonPaths = new[]
            {
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA",
                @"C:\Program Files (x86)\NVIDIA GPU Computing Toolkit\CUDA"
            };


            return commonPaths.Any(Directory.Exists);
        }
        catch
        {
            return false;
        }
    }


    private static bool CheckLinuxCuda()
    {
        try
        {
            // Check for nvidia-smi
            var result = ExecuteCommand("which", "nvidia-smi");
            if (string.IsNullOrEmpty(result))
                return false;

            // Check for CUDA libraries

            var ldLibraryPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
            return ldLibraryPath.Contains("cuda", StringComparison.OrdinalIgnoreCase) ||

                   Directory.Exists("/usr/local/cuda") ||
                   Directory.Exists("/opt/cuda");
        }
        catch
        {
            return false;
        }
    }


    private static bool CheckWindowsOpenCL()
    {
        try
        {
            return File.Exists(@"C:\Windows\System32\OpenCL.dll") ||
                   File.Exists(@"C:\Windows\SysWOW64\OpenCL.dll");
        }
        catch
        {
            return false;
        }
    }


    private static bool CheckLinuxOpenCL()
    {
        try
        {
            var result = ExecuteCommand("ldconfig", "-p");
            return result.Contains("libOpenCL.so", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }


    private static bool CheckMacOSOpenCL()
    {
        try
        {
            return Directory.Exists("/System/Library/Frameworks/OpenCL.framework");
        }
        catch
        {
            return false;
        }
    }


    private static int GetNvidiaGpuCount()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#if WINDOWS
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController WHERE Name LIKE '%NVIDIA%'");
                return searcher.Get().Cast<ManagementObject>().Count();
#else
                return 0; // TODO: Implement cross-platform GPU detection
#endif
            }

            // For Linux/macOS, we'd need different approaches

            return 0;
        }
        catch
        {
            return 0;
        }
    }


    private static int GetAmdGpuCount()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#if WINDOWS
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController WHERE Name LIKE '%AMD%' OR Name LIKE '%Radeon%'");
                return searcher.Get().Cast<ManagementObject>().Count();
#else
                return 0; // TODO: Implement cross-platform GPU detection
#endif
            }


            return 0;
        }
        catch
        {
            return 0;
        }
    }


    private static int GetIntelGpuCount()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#if WINDOWS
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController WHERE Name LIKE '%Intel%'");
                return searcher.Get().Cast<ManagementObject>().Count();
#else
                return 0; // TODO: Implement cross-platform GPU detection
#endif
            }


            return 0;
        }
        catch
        {
            return 0;
        }
    }


    private static int GetWindowsPhysicalCores()
    {
#if WINDOWS
        using var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
        return searcher.Get().Cast<ManagementObject>()
            .Sum(item => Convert.ToInt32(item["NumberOfCores"]));
#else
        return Environment.ProcessorCount;
#endif
    }


    private static int GetLinuxPhysicalCores()
    {
        try
        {
            var cpuInfo = File.ReadAllText("/proc/cpuinfo");
            var coreIds = cpuInfo.Split('\n')
                .Where(line => line.StartsWith("core id", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.Split(':')[1].Trim())
                .Distinct()
                .Count();


            return Math.Max(coreIds, 1);
        }
        catch
        {
            return Environment.ProcessorCount;
        }
    }


    private static int GetMacOSPhysicalCores()
    {
        try
        {
            var result = ExecuteCommand("sysctl", "-n hw.physicalcpu");
            return int.TryParse(result.Trim(), out var cores) ? cores : Environment.ProcessorCount;
        }
        catch
        {
            return Environment.ProcessorCount;
        }
    }


    private static long GetWindowsTotalMemory()
    {
#if WINDOWS
        using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
        var memory = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
        return memory != null ? Convert.ToInt64(memory["TotalPhysicalMemory"]) : 0;
#else
        return GC.GetTotalMemory(false);
#endif
    }


    private static long GetLinuxTotalMemory()
    {
        try
        {
            var memInfo = File.ReadAllText("/proc/meminfo");
            var totalLine = memInfo.Split('\n').FirstOrDefault(line => line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase));
            if (totalLine != null)
            {
                var parts = totalLine.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                {
                    return kb * 1024; // Convert from KB to bytes
                }
            }
        }
        catch
        {
            // Fallback
        }


        return 0;
    }


    private static long GetMacOSTotalMemory()
    {
        try
        {
            var result = ExecuteCommand("sysctl", "-n hw.memsize");
            return long.TryParse(result.Trim(), out var memory) ? memory : 0;
        }
        catch
        {
            return 0;
        }
    }


    private static long GetWindowsAvailableMemory()
    {
#if WINDOWS
        using var searcher = new ManagementObjectSearcher("SELECT AvailablePhysicalMemory FROM Win32_OperatingSystem");
        var memory = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
        return memory != null ? Convert.ToInt64(memory["AvailablePhysicalMemory"]) : 0;
#else
        return GC.GetTotalMemory(false);
#endif
    }


    private static long GetLinuxAvailableMemory()
    {
        try
        {
            var memInfo = File.ReadAllText("/proc/meminfo");
            var availableLine = memInfo.Split('\n').FirstOrDefault(line => line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase));
            if (availableLine != null)
            {
                var parts = availableLine.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                {
                    return kb * 1024; // Convert from KB to bytes
                }
            }
        }
        catch
        {
            // Fallback
        }


        return 0;
    }


    private static long GetMacOSAvailableMemory()
    {
        try
        {
            var result = ExecuteCommand("vm_stat");
            // Parse vm_stat output to calculate available memory
            // This is simplified; actual implementation would be more complex
            return GetMacOSTotalMemory() / 2; // Conservative estimate
        }
        catch
        {
            return 0;
        }
    }


    private static List<string> GetWindowsGpuNames()
    {
        var names = new List<string>();
        try
        {
#if WINDOWS
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var gpu in searcher.Get().Cast<ManagementObject>())
            {
                var name = gpu["Name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }
#else
            // On non-Windows platforms, GPU enumeration is not implemented
            // Could use nvidia-ml or other platform-specific APIs TODO
#endif
        }
        catch
        {
            // Ignore errors
        }


        return names;
    }


    private static string GetWindowsCpuName()
    {
        try
        {
#if WINDOWS
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            var cpu = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            return cpu?["Name"]?.ToString() ?? "Unknown CPU";
#else
            return "Unknown CPU"; // TODO: Implement cross-platform CPU name detection
#endif
        }
        catch
        {
            return "Unknown CPU";
        }
    }


    private static string ExecuteCommand(string command, string arguments = "")
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };


            using var process = Process.Start(processStartInfo);
            return process?.StandardOutput.ReadToEnd() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion


    #endregion
}
