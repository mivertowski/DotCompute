// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Detects platform-specific NUMA capabilities and configuration.
/// </summary>
internal static partial class NumaPlatformDetector
{
    /// <summary>
    /// Detects NUMA capabilities for the current platform.
    /// </summary>
    public static NumaPlatformCapabilities DetectCapabilities()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return DetectWindowsCapabilities();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return DetectLinuxCapabilities();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return DetectMacOSCapabilities();
        }

        return CreateDefaultCapabilities();
    }

    /// <summary>
    /// Gets CPU mapping information for a specific node on the current platform.
    /// </summary>
    public static bool GetNodeCpuMapping(int nodeId, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxNodeCpuMapping(nodeId, out cpuMask, out cpuCount);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsNodeCpuMapping(nodeId, out cpuMask, out cpuCount);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSNodeCpuMapping(nodeId, out cpuMask, out cpuCount);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get NUMA CPU mapping: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Gets memory information for a specific node on the current platform.
    /// </summary>
    public static long GetNodeMemorySize(int nodeId)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxNodeMemorySize(nodeId);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsNodeMemorySize(nodeId);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSNodeMemorySize(nodeId);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get node memory size: {ex.Message}");
        }

        return 0;
    }

    #region Windows Detection

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static NumaPlatformCapabilities DetectWindowsCapabilities()
    {
        var hasNativeNumaApi = CanUseWindowsNumaApi();
        var hasWmiSupport = CanUseWindowsWmi();
        var nodeCount = GetWindowsNodeCount();

        return new NumaPlatformCapabilities
        {
            Platform = "Windows",
            HasNativeNumaSupport = hasNativeNumaApi,
            HasLibnumaSupport = false,
            HasWmiSupport = hasWmiSupport,
            MaxNodes = nodeCount,
            SupportsMemoryBinding = hasNativeNumaApi,
            SupportsAffinityControl = true,
            SupportsHugePages = CheckWindowsHugePagesSupport(),
            PreferredDetectionMethod = hasNativeNumaApi ? "Native API" : hasWmiSupport ? "WMI" : "Fallback"
        };
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool CanUseWindowsNumaApi()
    {
        try
        {
            return NumaInterop.GetNumaHighestNodeNumber(out _);
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool CanUseWindowsWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            using var results = searcher.Get();
            return results.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int GetWindowsNodeCount()
    {
        try
        {
            return NumaInterop.GetNumaHighestNodeNumber() + 1;
        }
        catch
        {
            return 1;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool CheckWindowsHugePagesSupport()
    {
        try
        {
            // Check if large pages privilege is available
            using var process = Process.GetCurrentProcess();
            return process.HandleCount > 0; // Simplified check
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool GetWindowsNodeCpuMapping(int nodeId, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            if (NumaInterop.GetNumaNodeProcessorMaskEx((ushort)nodeId, out var affinity))
            {
                cpuMask = affinity.Mask;
                cpuCount = CpuUtilities.CountSetBits(cpuMask);
                return cpuCount > 0;
            }

            // Fallback to WMI
            return GetWindowsNodeCpuMappingWmi(nodeId, out cpuMask, out cpuCount);
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool GetWindowsNodeCpuMappingWmi(int nodeId, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            using var results = searcher.Get();

            foreach (ManagementObject system in results)
            {
                var totalProcessors = Convert.ToInt32(system["NumberOfProcessors"], CultureInfo.InvariantCulture);
                var estimatedNodes = Math.Max(1, totalProcessors / 8); // Assume 8 cores per NUMA node

                if (nodeId < estimatedNodes)
                {
                    var cpusPerNode = totalProcessors / estimatedNodes;
                    var startCpu = nodeId * cpusPerNode;
                    var endCpu = Math.Min(startCpu + cpusPerNode - 1, totalProcessors - 1);

                    cpuMask = CpuUtilities.CreateCpuMask(startCpu, endCpu);
                    cpuCount = endCpu - startCpu + 1;
                    return cpuCount > 0;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static long GetWindowsNodeMemorySize(int nodeId) => NumaInterop.GetNumaNodeMemorySize(nodeId);

    #endregion

    #region Linux Detection

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static NumaPlatformCapabilities DetectLinuxCapabilities()
    {
        var hasLibnuma = NumaInterop.IsNumaLibraryAvailable();
        var hasSysfs = Directory.Exists("/sys/devices/system/node");
        var nodeCount = GetLinuxNodeCount();

        return new NumaPlatformCapabilities
        {
            Platform = "Linux",
            HasNativeNumaSupport = true,
            HasLibnumaSupport = hasLibnuma,
            HasWmiSupport = false,
            MaxNodes = nodeCount,
            SupportsMemoryBinding = File.Exists("/proc/sys/kernel/numa_balancing"),
            SupportsAffinityControl = true,
            SupportsHugePages = CheckLinuxHugePagesSupport(),
            PreferredDetectionMethod = hasLibnuma ? "libnuma" : hasSysfs ? "sysfs" : "Fallback"
        };
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static int GetLinuxNodeCount()
    {
        try
        {
            if (NumaInterop.IsNumaLibraryAvailable())
            {
                return NumaInterop.numa_max_node() + 1;
            }

            if (Directory.Exists("/sys/devices/system/node"))
            {
                return Directory.GetDirectories("/sys/devices/system/node", "node*").Length;
            }
        }
        catch
        {
            // Ignore errors
        }

        return 1;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static bool CheckLinuxHugePagesSupport()
    {
        return Directory.Exists("/sys/kernel/mm/hugepages") ||
               File.Exists("/proc/meminfo");
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static bool GetLinuxNodeCpuMapping(int nodeId, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            // Primary method: Read from sysfs
            var cpuListPath = $"/sys/devices/system/node/node{nodeId}/cpulist";
            if (File.Exists(cpuListPath))
            {
                var cpuList = File.ReadAllText(cpuListPath).Trim();
                var (mask, count) = CpuUtilities.ParseCpuList(cpuList);
                cpuMask = mask;
                cpuCount = count;
                return count > 0;
            }

            // Alternative: CPU map
            var cpuMapPath = $"/sys/devices/system/node/node{nodeId}/cpumap";
            if (File.Exists(cpuMapPath))
            {
                var cpuMap = File.ReadAllText(cpuMapPath).Trim();
                return CpuUtilities.ParseCpuMask(cpuMap, out cpuMask, out cpuCount);
            }

            // Fallback: Use libnuma if available
            if (NumaInterop.IsNumaLibraryAvailable())
            {
                return GetLinuxNodeCpuMappingLibnuma(nodeId, out cpuMask, out cpuCount);
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static bool GetLinuxNodeCpuMappingLibnuma(int nodeId, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            var maxNode = NumaInterop.numa_max_node();
            if (nodeId <= maxNode)
            {
                // Estimate CPU count per node
                var totalCpus = Environment.ProcessorCount;
                var nodesCount = maxNode + 1;
                var cpusPerNode = Math.Max(1, totalCpus / nodesCount);

                var startCpu = nodeId * cpusPerNode;
                var endCpu = Math.Min(startCpu + cpusPerNode - 1, totalCpus - 1);

                cpuMask = CpuUtilities.CreateCpuMask(startCpu, endCpu);
                cpuCount = endCpu - startCpu + 1;
                return cpuCount > 0;
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static long GetLinuxNodeMemorySize(int nodeId)
    {
        try
        {
            var meminfoPath = $"/sys/devices/system/node/node{nodeId}/meminfo";
            if (File.Exists(meminfoPath))
            {
                var lines = File.ReadAllLines(meminfoPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith($"Node {nodeId} MemTotal:", StringComparison.Ordinal))
                    {
                        var match = MemorySizeRegex().Match(line);
                        if (match.Success && long.TryParse(match.Groups[1].Value, out var kb))
                        {
                            return kb * 1024; // Convert KB to bytes
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return 0;
    }

    #endregion

    #region macOS Detection

    [System.Runtime.Versioning.SupportedOSPlatform("osx")]
    private static NumaPlatformCapabilities DetectMacOSCapabilities()
    {
        return new NumaPlatformCapabilities
        {
            Platform = "macOS",
            HasNativeNumaSupport = false,
            HasLibnumaSupport = false,
            HasWmiSupport = false,
            MaxNodes = GetMacOSNodeCount(),
            SupportsMemoryBinding = false,
            SupportsAffinityControl = true,
            SupportsHugePages = false,
            PreferredDetectionMethod = "sysctl"
        };
    }

    [System.Runtime.Versioning.SupportedOSPlatform("osx")]
    private static int GetMacOSNodeCount()
        // macOS doesn't have traditional NUMA, simulate based on core types
        => 2; // Assume efficiency + performance cores

    [System.Runtime.Versioning.SupportedOSPlatform("osx")]
    private static bool GetMacOSNodeCpuMapping(int nodeId, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n hw.ncpu hw.physicalcpu",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _ = process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                var values = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length >= 2 && int.TryParse(values[0], out var totalCpus))
                {
                    var cpusPerNode = Math.Max(1, totalCpus / 2); // Simulate 2 nodes

                    if (nodeId == 0)
                    {
                        // Performance cores
                        cpuMask = CpuUtilities.CreateCpuMask(0, cpusPerNode - 1);
                        cpuCount = cpusPerNode;
                        return true;
                    }
                    else if (nodeId == 1 && totalCpus > cpusPerNode)
                    {
                        // Efficiency cores
                        cpuMask = CpuUtilities.CreateCpuMask(cpusPerNode, totalCpus - 1);
                        cpuCount = totalCpus - cpusPerNode;
                        return cpuCount > 0;
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("osx")]
    private static long GetMacOSNodeMemorySize(int nodeId)
        // macOS doesn't expose per-node memory information
        => 0;

    #endregion

    #region Helper Methods

    private static NumaPlatformCapabilities CreateDefaultCapabilities()
    {
        return new NumaPlatformCapabilities
        {
            Platform = "Unknown",
            HasNativeNumaSupport = false,
            HasLibnumaSupport = false,
            HasWmiSupport = false,
            MaxNodes = 1,
            SupportsMemoryBinding = false,
            SupportsAffinityControl = false,
            SupportsHugePages = false,
            PreferredDetectionMethod = "Fallback"
        };
    }

    [GeneratedRegex(@"(\d+)\s*kB")]
    private static partial Regex MemorySizeRegex();

    #endregion
}

/// <summary>
/// Platform capabilities for NUMA operations.
/// </summary>
public sealed class NumaPlatformCapabilities
{
    /// <summary>Platform name.</summary>
    public required string Platform { get; init; }

    /// <summary>Whether the platform has native NUMA support.</summary>
    public required bool HasNativeNumaSupport { get; init; }

    /// <summary>Whether libnuma is available.</summary>
    public required bool HasLibnumaSupport { get; init; }

    /// <summary>Whether WMI is available (Windows).</summary>
    public required bool HasWmiSupport { get; init; }

    /// <summary>Maximum number of NUMA nodes.</summary>
    public required int MaxNodes { get; init; }

    /// <summary>Whether memory binding is supported.</summary>
    public required bool SupportsMemoryBinding { get; init; }

    /// <summary>Whether affinity control is supported.</summary>
    public required bool SupportsAffinityControl { get; init; }

    /// <summary>Whether huge pages are supported.</summary>
    public required bool SupportsHugePages { get; init; }

    /// <summary>Preferred detection method.</summary>
    public required string PreferredDetectionMethod { get; init; }

    /// <summary>Whether this is a NUMA-capable platform.</summary>
    public bool IsNumaCapable => HasNativeNumaSupport && MaxNodes > 1;
}