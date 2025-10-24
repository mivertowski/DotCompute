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
    /// Checks if Metal is available (macOS only).
    /// </summary>
    public static bool IsMetalAvailable()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return false;
            }

            // Check macOS version (Metal requires macOS 10.11+, compute shaders require 10.13+)
            var version = Environment.OSVersion.Version;
            if (version.Major < 10 || (version.Major == 10 && version.Minor < 13))
            {
                return false;
            }

            // For now, assume Metal is available on supported macOS versions
            // In production, this would check for actual Metal device availability
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if running on Apple Silicon (M1/M2/M3/M4).
    /// </summary>
    public static bool IsAppleSilicon()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
               RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }

    /// <summary>
    /// Gets estimated Metal GPU memory in bytes.
    /// </summary>
    public static long GetEstimatedMetalGpuMemory()
    {
        if (!IsMetalAvailable())
            return 0;

        try
        {
            if (IsAppleSilicon())
            {
                // Apple Silicon uses unified memory
                // Return conservative estimate based on total system memory
                var totalSystemMemory = GetTotalSystemMemory();
                return Math.Min(totalSystemMemory / 2, 32L * 1024 * 1024 * 1024); // Max 32GB for GPU
            }
            else
            {
                // Intel Mac with discrete GPU (estimates)
                return 4L * 1024 * 1024 * 1024; // Conservative 4GB estimate
            }
        }
        catch
        {
            return 0;
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
            // Try to use nvidia-ml-py equivalent or nvidia-smi for accurate CUDA device counting
            return GetNvidiaGpuCountViaNvidiaSmi() ?? GetNvidiaGpuCount();
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
            // Enumerate OpenCL platforms and devices using system commands
            var openClDeviceCount = GetOpenClDeviceCountViaSystemInfo();
            if (openClDeviceCount > 0)
                return openClDeviceCount;

            // Fallback: estimate based on GPU counts from different vendors
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
        /// Gets a value indicating whether Metal is available.
        /// </summary>
        /// <value>
        ///   <c>true</c> if Metal is available; otherwise, <c>false</c>.
        /// </value>
        public bool MetalAvailable { get; init; }

        /// <summary>
        /// Gets a value indicating whether running on Apple Silicon.
        /// </summary>
        /// <value>
        ///   <c>true</c> if running on Apple Silicon; otherwise, <c>false</c>.
        /// </value>
        public bool IsAppleSilicon { get; init; }

        /// <summary>
        /// Gets the estimated Metal GPU memory in bytes.
        /// </summary>
        /// <value>
        /// The estimated Metal GPU memory.
        /// </value>
        public long MetalGpuMemory { get; init; }

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
            MetalAvailable = IsMetalAvailable(),
            IsAppleSilicon = IsAppleSilicon(),
            MetalGpuMemory = GetEstimatedMetalGpuMemory(),
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
            // Add Linux and macOS implementations
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                gpuNames.AddRange(GetLinuxGpuNames());
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                gpuNames.AddRange(GetMacOSGpuNames());
            }
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
            // Add Linux and macOS implementations
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxCpuName();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSCpuName();
            }
        }
        catch
        {
            // Ignore errors in CPU name detection
        }


        return "Unknown CPU";
    }

    #region Platform-Specific Implementations


    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "It's tracked.")]
    private static bool CheckWindowsCuda()
    {
        try
        {
#if WINDOWS
            // Check for NVIDIA display driver
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController WHERE Name LIKE '%NVIDIA%'");
            var hasNvidiaGpu = searcher.Get().Cast<ManagementObject>().Any();
#else
            var hasNvidiaGpu = GetNvidiaGpuCountLinux() > 0;
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
                return 0;
#endif
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetNvidiaGpuCountLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetNvidiaGpuCountMacOS();
            }

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
                return GetAmdGpuCountLinux();
#endif
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetAmdGpuCountLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetAmdGpuCountMacOS();
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
                return GetIntelGpuCountLinux();
#endif
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetIntelGpuCountLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetIntelGpuCountMacOS();
            }


            return 0;
        }
        catch
        {
            return 0;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "It's tracked.")]
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "It's tracked.")]
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "It's tracked.")]
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "It's tracked.")]
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
            // Use cross-platform GPU enumeration
            names.AddRange(GetCrossPlatformGpuNames());
#endif
        }
        catch
        {
            // Ignore errors
        }


        return names;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "It's tracked.")]
    private static string GetWindowsCpuName()
    {
        try
        {
#if WINDOWS
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            var cpu = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            return cpu?["Name"]?.ToString() ?? "Unknown CPU";
#else
            return "Unknown CPU";
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

    #region Cross-Platform GPU Detection Methods

    /// <summary>
    /// Gets NVIDIA GPU count via nvidia-smi command.
    /// </summary>
    private static int? GetNvidiaGpuCountViaNvidiaSmi()
    {
        try
        {
            var output = ExecuteCommand("nvidia-smi", "--query-gpu=name --format=csv,noheader");
            if (!string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                return lines.Length;
            }
        }
        catch
        {
            // nvidia-smi not available or failed
        }
        return null;
    }

    /// <summary>
    /// Gets OpenCL device count via system information.
    /// </summary>
    private static int GetOpenClDeviceCountViaSystemInfo()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try clinfo command
                var output = ExecuteCommand("clinfo", "--list");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var deviceLines = output.Split('\n')
                        .Where(line => line.Contains("Device", StringComparison.OrdinalIgnoreCase))
                        .Count();
                    return deviceLines;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // On macOS, OpenCL is deprecated but we can check system_profiler
                var output = ExecuteCommand("system_profiler", "SPDisplaysDataType");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    // Count graphics cards that support OpenCL
                    return output.Split('\n')
                        .Count(line => line.Contains("Chipset Model:", StringComparison.OrdinalIgnoreCase));
                }
            }
        }
        catch
        {
            // Commands not available
        }
        return 0;
    }

    /// <summary>
    /// Gets NVIDIA GPU count on Linux systems.
    /// </summary>
    private static int GetNvidiaGpuCountLinux()
    {
        try
        {
            // Method 1: nvidia-smi
            var nvidiaSmiCount = GetNvidiaGpuCountViaNvidiaSmi();
            if (nvidiaSmiCount.HasValue)
                return nvidiaSmiCount.Value;

            // Method 2: Check /proc/driver/nvidia/gpus
            if (Directory.Exists("/proc/driver/nvidia/gpus"))
            {
                return Directory.GetDirectories("/proc/driver/nvidia/gpus").Length;
            }

            // Method 3: lspci command
            var output = ExecuteCommand("lspci", "-nn");
            if (!string.IsNullOrWhiteSpace(output))
            {
                return output.Split('\n')
                    .Count(line => line.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
                                   line.Contains("VGA", StringComparison.OrdinalIgnoreCase));
            }
        }
        catch
        {
            // Commands failed
        }
        return 0;
    }

    /// <summary>
    /// Gets NVIDIA GPU count on macOS systems.
    /// </summary>
    private static int GetNvidiaGpuCountMacOS()
    {
        try
        {
            // NVIDIA GPUs are rare on modern Macs, but check system_profiler
            var output = ExecuteCommand("system_profiler", "SPDisplaysDataType");
            if (!string.IsNullOrWhiteSpace(output))
            {
                return output.Split('\n')
                    .Count(line => line.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase));
            }
        }
        catch
        {
            // Command failed
        }
        return 0;
    }

    /// <summary>
    /// Gets AMD GPU count on Linux systems.
    /// </summary>
    private static int GetAmdGpuCountLinux()
    {
        try
        {
            // Method 1: lspci command
            var output = ExecuteCommand("lspci", "-nn");
            if (!string.IsNullOrWhiteSpace(output))
            {
                var amdCount = output.Split('\n')
                    .Count(line => (line.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("ATI", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) &&
                                   line.Contains("VGA", StringComparison.OrdinalIgnoreCase));
                if (amdCount > 0)
                    return amdCount;
            }

            // Method 2: Check /sys/class/drm
            if (Directory.Exists("/sys/class/drm"))
            {
                return Directory.GetDirectories("/sys/class/drm", "card*")
                    .Count(dir =>
                    {
                        try
                        {
                            var devicePath = Path.Combine(dir, "device", "vendor");
                            if (File.Exists(devicePath))
                            {
                                var vendor = File.ReadAllText(devicePath).Trim();
                                return vendor.Equals("0x1002", StringComparison.OrdinalIgnoreCase); // AMD vendor ID
                            }
                        }
                        catch { }
                        return false;
                    });
            }
        }
        catch
        {
            // Commands failed
        }
        return 0;
    }

    /// <summary>
    /// Gets AMD GPU count on macOS systems.
    /// </summary>
    private static int GetAmdGpuCountMacOS()
    {
        try
        {
            var output = ExecuteCommand("system_profiler", "SPDisplaysDataType");
            if (!string.IsNullOrWhiteSpace(output))
            {
                return output.Split('\n')
                    .Count(line => line.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                                   line.Contains("Radeon", StringComparison.OrdinalIgnoreCase));
            }
        }
        catch
        {
            // Command failed
        }
        return 0;
    }

    /// <summary>
    /// Gets Intel GPU count on Linux systems.
    /// </summary>
    private static int GetIntelGpuCountLinux()
    {
        try
        {
            // Method 1: lspci command
            var output = ExecuteCommand("lspci", "-nn");
            if (!string.IsNullOrWhiteSpace(output))
            {
                var intelCount = output.Split('\n')
                    .Count(line => line.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
                                   (line.Contains("VGA", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("Display", StringComparison.OrdinalIgnoreCase)));
                if (intelCount > 0)
                    return intelCount;
            }

            // Method 2: Check /sys/class/drm for Intel graphics
            if (Directory.Exists("/sys/class/drm"))
            {
                return Directory.GetDirectories("/sys/class/drm", "card*")
                    .Count(dir =>
                    {
                        try
                        {
                            var devicePath = Path.Combine(dir, "device", "vendor");
                            if (File.Exists(devicePath))
                            {
                                var vendor = File.ReadAllText(devicePath).Trim();
                                return vendor.Equals("0x8086", StringComparison.OrdinalIgnoreCase); // Intel vendor ID
                            }
                        }
                        catch { }
                        return false;
                    });
            }
        }
        catch
        {
            // Commands failed
        }
        return 0;
    }

    /// <summary>
    /// Gets Intel GPU count on macOS systems.
    /// </summary>
    private static int GetIntelGpuCountMacOS()
    {
        try
        {
            var output = ExecuteCommand("system_profiler", "SPDisplaysDataType");
            if (!string.IsNullOrWhiteSpace(output))
            {
                return output.Split('\n')
                    .Count(line => line.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
                                   (line.Contains("Iris", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("HD Graphics", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("UHD Graphics", StringComparison.OrdinalIgnoreCase)));
            }
        }
        catch
        {
            // Command failed
        }
        return 0;
    }

    #endregion

    #region Cross-Platform GPU Detection Methods

    /// <summary>
    /// Gets cross-platform GPU names for non-Windows systems.
    /// </summary>
    private static List<string> GetCrossPlatformGpuNames()
    {
        var names = new List<string>();
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                names.AddRange(GetLinuxGpuNames());
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                names.AddRange(GetMacOSGpuNames());
            }
        }
        catch
        {
            // Ignore errors in GPU name detection
        }
        return names;
    }

    /// <summary>
    /// Gets Linux GPU names using lspci and system commands.
    /// </summary>
    private static List<string> GetLinuxGpuNames()
    {
        var names = new List<string>();
        try
        {
            // Method 1: lspci command for PCI GPU devices
            var output = ExecuteCommand("lspci", "-nn");
            if (!string.IsNullOrWhiteSpace(output))
            {
                var gpuLines = output.Split('\n')
                    .Where(line => line.Contains("VGA", StringComparison.OrdinalIgnoreCase) ||
                                   line.Contains("3D", StringComparison.OrdinalIgnoreCase) ||
                                   line.Contains("Display", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var line in gpuLines)
                {
                    // Extract GPU name from lspci output
                    var parts = line.Split(':');
                    if (parts.Length >= 3)
                    {
                        var gpuInfo = string.Join(":", parts.Skip(2)).Trim();
                        if (!string.IsNullOrEmpty(gpuInfo))
                        {
                            names.Add(gpuInfo);
                        }
                    }
                }
            }

            // Method 2: Check /proc/driver/nvidia/gpus for NVIDIA GPUs
            if (Directory.Exists("/proc/driver/nvidia/gpus"))
            {
                var nvidiaGpuDirs = Directory.GetDirectories("/proc/driver/nvidia/gpus");
                foreach (var gpuDir in nvidiaGpuDirs)
                {
                    try
                    {
                        var infoFile = Path.Combine(gpuDir, "information");
                        if (File.Exists(infoFile))
                        {
                            var info = File.ReadAllText(infoFile);
                            var nameLine = info.Split('\n')
                                .FirstOrDefault(line => line.StartsWith("Model:", StringComparison.OrdinalIgnoreCase));
                            if (nameLine != null)
                            {
                                var gpuName = nameLine[6..].Trim();
                                if (!names.Contains(gpuName))
                                {
                                    names.Add($"NVIDIA {gpuName}");
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip problematic GPU directories
                    }
                }
            }

            // Method 3: nvidia-smi for NVIDIA GPU names
            var nvidiaOutput = ExecuteCommand("nvidia-smi", "--query-gpu=name --format=csv,noheader,nounits");
            if (!string.IsNullOrWhiteSpace(nvidiaOutput))
            {
                var nvidiaGpus = nvidiaOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var gpu in nvidiaGpus)
                {
                    var gpuName = gpu.Trim();
                    if (!string.IsNullOrEmpty(gpuName) && !names.Contains(gpuName))
                    {
                        names.Add(gpuName);
                    }
                }
            }
        }
        catch
        {
            // Ignore command errors
        }
        return names;
    }

    /// <summary>
    /// Gets macOS GPU names using system_profiler.
    /// </summary>
    private static List<string> GetMacOSGpuNames()
    {
        var names = new List<string>();
        try
        {
            var output = ExecuteCommand("system_profiler", "SPDisplaysDataType");
            if (!string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("Chipset Model:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length >= 2)
                        {
                            var gpuName = parts[1].Trim();
                            if (!string.IsNullOrEmpty(gpuName))
                            {
                                names.Add(gpuName);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore command errors
        }
        return names;
    }

    /// <summary>
    /// Gets Linux CPU name from /proc/cpuinfo.
    /// </summary>
    private static string GetLinuxCpuName()
    {
        try
        {
            var cpuInfo = File.ReadAllText("/proc/cpuinfo");
            var nameLine = cpuInfo.Split('\n')
                .FirstOrDefault(line => line.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
            if (nameLine != null)
            {
                var parts = nameLine.Split(':');
                if (parts.Length >= 2)
                {
                    return parts[1].Trim();
                }
            }
        }
        catch
        {
            // Fallback to generic detection
        }

        // Try alternative command
        try
        {
            var result = ExecuteCommand("cat", "/proc/cpuinfo | grep 'model name' | head -1");
            if (!string.IsNullOrEmpty(result))
            {
                var parts = result.Split(':');
                if (parts.Length >= 2)
                {
                    return parts[1].Trim();
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return "Unknown Linux CPU";
    }

    /// <summary>
    /// Gets macOS CPU name using system_profiler.
    /// </summary>
    private static string GetMacOSCpuName()
    {
        try
        {
            var result = ExecuteCommand("sysctl", "-n machdep.cpu.brand_string");
            if (!string.IsNullOrEmpty(result))
            {
                return result.Trim();
            }
        }
        catch
        {
            // Try alternative method
        }

        try
        {
            var output = ExecuteCommand("system_profiler", "SPHardwareDataType");
            if (!string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("Processor Name:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length >= 2)
                        {
                            return parts[1].Trim();
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return "Unknown macOS CPU";
    }

    #endregion

    #endregion
}
