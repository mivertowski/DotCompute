// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DotCompute.Backends.CPU.Threading;


/// <summary>
/// Provides information about NUMA (Non-Uniform Memory Access) topology.
/// </summary>
public static partial class NumaInfo
{
    private static readonly Lazy<NumaTopology> _topology = new(DiscoverTopology);

    /// <summary>
    /// Gets the NUMA topology of the system.
    /// </summary>
    public static NumaTopology Topology => _topology.Value;

    /// <summary>
    /// Gets whether the system has NUMA support.
    /// </summary>
    public static bool IsNumaSystem => Topology.NodeCount > 1;

    private static NumaTopology DiscoverTopology()
    {
        // Platform-specific NUMA discovery
        if (OperatingSystem.IsWindows())
        {
            return DiscoverWindowsTopology();
        }
        else if (OperatingSystem.IsLinux())
        {
            return DiscoverLinuxTopology();
        }
        else
        {
            // Default to single node for unsupported platforms
            return new NumaTopology
            {
                NodeCount = 1,
                ProcessorCount = Environment.ProcessorCount,
                Nodes =
                [
                    new NumaNode
                {
                    NodeId = 0,
                    ProcessorMask = (1UL << Environment.ProcessorCount) - 1,
                    ProcessorCount = Environment.ProcessorCount,
                    MemorySize = 0 // Unknown
                }
                ]
            };
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static NumaTopology DiscoverWindowsTopology()
    {
        try
        {
            // Use Windows NUMA API for comprehensive topology discovery
            return DiscoverWindowsTopologyNative();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to discover Windows NUMA topology: {ex.Message}");
            // Fall back to WMI-based discovery
            return DiscoverWindowsTopologyWmi();
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static NumaTopology DiscoverWindowsTopologyNative()
    {
        var nodes = new List<NumaNode>();
        var nodeCount = GetNumaHighestNodeNumber() + 1;
        var processorCount = Environment.ProcessorCount;
        var distanceMatrix = new int[nodeCount][];
        for (var i = 0; i < nodeCount; i++)
        {
            distanceMatrix[i] = new int[nodeCount];
        }

        for (var nodeId = 0; nodeId < nodeCount; nodeId++)
        {
            // Get processor mask for this node
            if (GetNumaNodeProcessorMaskEx((ushort)nodeId, out var affinity))
            {
                var memorySize = GetNumaNodeMemorySize(nodeId);

                nodes.Add(new NumaNode
                {
                    NodeId = nodeId,
                    ProcessorMask = affinity.Mask,
                    ProcessorCount = PopCount(affinity.Mask),
                    MemorySize = memorySize,
                    Group = affinity.Group,
                    CacheCoherencyDomain = GetCacheCoherencyDomain(nodeId)
                });
            }

            // Build distance matrix
            for (var otherNodeId = 0; otherNodeId < nodeCount; otherNodeId++)
            {
                distanceMatrix[nodeId][otherNodeId] = GetNumaNodeDistance(nodeId, otherNodeId);
            }
        }

        return new NumaTopology
        {
            NodeCount = nodeCount,
            ProcessorCount = processorCount,
            Nodes = [.. nodes],
            DistanceMatrix = distanceMatrix,
            CacheLineSize = GetCacheLineSize(),
            PageSize = GetPageSize()
        };
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static NumaTopology DiscoverWindowsTopologyWmi()
    {
        var nodes = new List<NumaNode>();
        var processorCount = Environment.ProcessorCount;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return CreateFallbackTopology(processorCount);
            }

            // Use WMI as fallback for Windows topology discovery
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_NumaNode");

            var results = searcher.Get();
            if (results.Count > 0)
            {
                foreach (var node in results.Cast<ManagementObject>())
                {
                    var nodeId = Convert.ToInt32(node["NodeId"], CultureInfo.InvariantCulture);
                    var processorMask = GetProcessorMaskFromWmi(node);

                    nodes.Add(new NumaNode
                    {
                        NodeId = nodeId,
                        ProcessorMask = processorMask,
                        ProcessorCount = PopCount(processorMask),
                        MemorySize = GetMemorySizeFromWmi(node),
                        Group = 0, // Default group
                        CacheCoherencyDomain = nodeId
                    });
                }
            }
        }
        catch
        {
            // Fall back to simplified topology
            return CreateFallbackTopology(processorCount);
        }

        if (nodes.Count == 0)
        {
            return CreateFallbackTopology(processorCount);
        }

        var nodeCount = nodes.Count;
        var distanceMatrix = EstimateDistanceMatrix(nodeCount);

        return new NumaTopology
        {
            NodeCount = nodeCount,
            ProcessorCount = processorCount,
            Nodes = [.. nodes],
            DistanceMatrix = distanceMatrix,
            CacheLineSize = 64, // Standard cache line size
            PageSize = 4096 // Standard page size
        };
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static NumaTopology DiscoverLinuxTopology()
    {
        try
        {
            // Use advanced Linux NUMA discovery with libnuma integration
            return DiscoverLinuxTopologyAdvanced();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to discover Linux NUMA topology: {ex.Message}");
            // Fall back to sysfs-based discovery
            return DiscoverLinuxTopologySysfs();
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static NumaTopology DiscoverLinuxTopologyAdvanced()
    {
        var processorCount = Environment.ProcessorCount;
        var nodes = new List<NumaNode>();

        // First try libnuma integration
        if (TryLoadLibnuma())
        {
            var nodeCount = numa_max_node() + 1;
            var distanceMatrix = new int[nodeCount][];
            for (var i = 0; i < nodeCount; i++)
            {
                distanceMatrix[i] = new int[nodeCount];
            }

            for (var nodeId = 0; nodeId < nodeCount; nodeId++)
            {
                if (numa_node_to_cpus(nodeId, out var cpuMask, out var cpuCount))
                {
                    var memorySize = GetLinuxNodeMemorySize(nodeId);
                    var hugePagesInfo = GetLinuxHugePagesInfo(nodeId);
                    var cacheInfo = GetLinuxCacheInfo(nodeId);

                    nodes.Add(new NumaNode
                    {
                        NodeId = nodeId,
                        ProcessorMask = cpuMask,
                        ProcessorCount = cpuCount,
                        MemorySize = memorySize,
                        Group = 0, // Linux doesn't use processor groups
                        CacheCoherencyDomain = nodeId,
                        HugePagesInfo = hugePagesInfo,
                        CacheHierarchy = cacheInfo
                    });
                }

                // Build distance matrix using libnuma
                for (var otherNodeId = 0; otherNodeId < nodeCount; otherNodeId++)
                {
                    distanceMatrix[nodeId][otherNodeId] = numa_distance(nodeId, otherNodeId);
                }
            }

            return new NumaTopology
            {
                NodeCount = nodeCount,
                ProcessorCount = processorCount,
                Nodes = [.. nodes],
                DistanceMatrix = distanceMatrix,
                CacheLineSize = GetLinuxCacheLineSize(),
                PageSize = GetLinuxPageSize(),
                SupportsMemoryBinding = true,
                SupportsMemoryPolicy = true
            };
        }

        // Fall back to sysfs discovery
        return DiscoverLinuxTopologySysfs();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static NumaTopology DiscoverLinuxTopologySysfs()
    {
        var processorCount = Environment.ProcessorCount;

        try
        {
            // Enhanced sysfs-based discovery
            if (Directory.Exists("/sys/devices/system/node"))
            {
                var nodeDirs = Directory.GetDirectories("/sys/devices/system/node", "node*")
                    .OrderBy(d => int.Parse(Path.GetFileName(d)[4..], CultureInfo.InvariantCulture))
                    .ToArray();

                var nodes = new List<NumaNode>();
                var nodeCount = nodeDirs.Length;
                var distanceMatrix = new int[nodeCount][];
                for (var i = 0; i < nodeCount; i++)
                {
                    distanceMatrix[i] = new int[nodeCount];
                }

                for (var i = 0; i < nodeDirs.Length; i++)
                {
                    var nodeDir = nodeDirs[i];
                    var nodeId = int.Parse(Path.GetFileName(nodeDir)[4..], CultureInfo.InvariantCulture);

                    var cpuListFile = Path.Combine(nodeDir, "cpulist");
                    if (File.Exists(cpuListFile))
                    {
                        var cpuList = File.ReadAllText(cpuListFile).Trim();
                        var (mask, count) = ParseCpuList(cpuList);
                        var memorySize = GetLinuxNodeMemorySizeSysfs(nodeDir);
                        var hugePagesInfo = GetLinuxHugePagesSysfs(nodeDir);
                        var cacheInfo = GetLinuxCacheInfoSysfs(nodeId);

                        nodes.Add(new NumaNode
                        {
                            NodeId = nodeId,
                            ProcessorMask = mask,
                            ProcessorCount = count,
                            MemorySize = memorySize,
                            Group = 0,
                            CacheCoherencyDomain = nodeId,
                            HugePagesInfo = hugePagesInfo,
                            CacheHierarchy = cacheInfo
                        });
                    }

                    // Build distance matrix from sysfs
                    var distanceFile = Path.Combine(nodeDir, "distance");
                    if (File.Exists(distanceFile))
                    {
                        var distances = File.ReadAllText(distanceFile).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        for (var j = 0; j < Math.Min(distances.Length, nodeCount); j++)
                        {
                            if (int.TryParse(distances[j], out var distance))
                            {
                                distanceMatrix[i][j] = distance;
                            }
                        }
                    }
                }

                if (nodes.Count > 0)
                {
                    return new NumaTopology
                    {
                        NodeCount = nodes.Count,
                        ProcessorCount = processorCount,
                        Nodes = [.. nodes],
                        DistanceMatrix = distanceMatrix,
                        CacheLineSize = GetLinuxCacheLineSize(),
                        PageSize = GetLinuxPageSize(),
                        SupportsMemoryBinding = File.Exists("/proc/sys/kernel/numa_balancing"),
                        SupportsMemoryPolicy = Directory.Exists("/sys/kernel/mm/numa")
                    };
                }
            }
        }
        catch
        {
            // Fall through to default
        }

        // Default single node topology
        return CreateFallbackTopology(processorCount);
    }

    private static (ulong mask, int count) ParseCpuList(string cpuList)
    {
        // Parse Linux CPU list format: "0-3,8-11"
        ulong mask = 0;
        var count = 0;

        var parts = cpuList.Split(',');
        foreach (var part in parts)
        {
            if (part.Contains('-', StringComparison.Ordinal))
            {
                var range = part.Split('-');
                var start = int.Parse(range[0], CultureInfo.InvariantCulture);
                var end = int.Parse(range[1], CultureInfo.InvariantCulture);

                for (var i = start; i <= end; i++)
                {
                    mask |= (1UL << i);
                    count++;
                }
            }
            else
            {
                var cpu = int.Parse(part, CultureInfo.InvariantCulture);
                mask |= (1UL << cpu);
                count++;
            }
        }

        return (mask, count);
    }

    private static long GetNodeMemorySize(string nodeDir)
    {
        try
        {
            var meminfoFile = Path.Combine(nodeDir, "meminfo");
            if (File.Exists(meminfoFile))
            {
                var lines = File.ReadAllLines(meminfoFile);
                foreach (var line in lines)
                {
                    if (line.StartsWith($"Node {Path.GetFileName(nodeDir)[4..]} MemTotal:", StringComparison.Ordinal))
                    {
                        var parts = line.Split(':')[1].Trim().Split(' ');
                        return long.Parse(parts[0], CultureInfo.InvariantCulture) * 1024; // Convert from KB to bytes
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

    #region Windows Native API Declarations

    [StructLayout(LayoutKind.Sequential)]
    private struct GROUP_AFFINITY
    {
        public ulong Mask;
        public ushort Group;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#pragma warning disable IDE1006 // Naming Styles
    private static extern bool GetNumaNodeProcessorMaskEx(ushort Node, out GROUP_AFFINITY ProcessorMask);
#pragma warning restore IDE1006 // Naming Styles

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#pragma warning disable IDE1006 // Naming Styles
    private static extern bool GetNumaHighestNodeNumber(out uint HighestNodeNumber);
#pragma warning restore IDE1006 // Naming Styles

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int GetNumaHighestNodeNumber() => GetNumaHighestNodeNumber(out var highest) ? (int)highest : 0;

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#pragma warning disable IDE1006 // Naming Styles
    private static extern bool GetNumaNodeProcessorMask(byte Node, out ulong ProcessorMask);
#pragma warning restore IDE1006 // Naming Styles

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#pragma warning disable IDE1006 // Naming Styles
    private static extern bool GetNumaAvailableMemoryNodeEx(ushort Node, out ulong AvailableBytes);
#pragma warning restore IDE1006 // Naming Styles

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_INFO
    {
        public ushort ProcessorArchitecture;
        public ushort Reserved;
        public uint PageSize;
        public IntPtr MinimumApplicationAddress;
        public IntPtr MaximumApplicationAddress;
        public IntPtr ActiveProcessorMask;
        public uint NumberOfProcessors;
        public uint ProcessorType;
        public uint AllocationGranularity;
        public ushort ProcessorLevel;
        public ushort ProcessorRevision;
    }

    #endregion

    #region Linux libnuma Integration

    private static bool _libnumaLoaded;
    private static bool _libnumaAvailable;

    private static bool TryLoadLibnuma()
    {
        if (_libnumaLoaded)
        {
            return _libnumaAvailable;
        }

        try
        {
            // Try to load libnuma
            _libnumaAvailable = numa_available() != -1;
        }
        catch
        {
            _libnumaAvailable = false;
        }
        finally
        {
            _libnumaLoaded = true;
        }

        return _libnumaAvailable;
    }

    [DllImport("numa", EntryPoint = "numa_available")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int numa_available();

    [DllImport("numa", EntryPoint = "numa_max_node")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int numa_max_node();

    [DllImport("numa", EntryPoint = "numa_distance")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int numa_distance(int from, int to);

    private static bool numa_node_to_cpus(int node, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetNumaCpusLinux(node, out cpuMask, out cpuCount);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetNumaCpusWindows(node, out cpuMask, out cpuCount);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetNumaCpusMacOS(node, out cpuMask, out cpuCount);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get NUMA CPU mapping: {ex.Message}");
        }

        return false;
    }

    private static bool GetNumaCpusLinux(int node, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            // Primary method: Read from /sys/devices/system/node/nodeX/cpulist
            var cpuListPath = $"/sys/devices/system/node/node{node}/cpulist";
            if (File.Exists(cpuListPath))
            {
                var cpuList = File.ReadAllText(cpuListPath).Trim();
                var (mask, count) = ParseCpuList(cpuList);
                cpuMask = mask;
                cpuCount = count;
                return count > 0;
            }

            // Alternative method: Read from /sys/devices/system/node/nodeX/cpumap
            var cpuMapPath = $"/sys/devices/system/node/node{node}/cpumap";
            if (File.Exists(cpuMapPath))
            {
                var cpuMap = File.ReadAllText(cpuMapPath).Trim();
                if (ParseCpuMask(cpuMap, out cpuMask, out cpuCount))
                {
                    return cpuCount > 0;
                }
            }

            // Fallback: Try to use numa library if available
            if (IsNumaLibraryAvailable())
            {
                return TryGetNumaCpusNative(node, out cpuMask, out cpuCount);
            }

            // Last resort: Parse /proc/cpuinfo for topology information
            return TryParseNumaFromCpuInfo(node, out cpuMask, out cpuCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting Linux NUMA CPUs: {ex.Message}");
        }

        return false;
    }

    private static bool GetNumaCpusWindows(int node, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            // Use WMI to query NUMA node information
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT * FROM Win32_ComputerSystem");

            foreach (System.Management.ManagementObject system in searcher.Get())
            {
                var totalProcessors = Convert.ToInt32(system["NumberOfProcessors"], CultureInfo.InvariantCulture);
                var logicalProcessors = Convert.ToInt32(system["NumberOfLogicalProcessors"], CultureInfo.InvariantCulture);

                // Simple heuristic for Windows NUMA mapping
                // In a real implementation, you'd use GetNumaNodeProcessorMask Win32 API
                var processorsPerNode = Math.Max(1, totalProcessors / Math.Max(1, GetMaxNumaNodeWindows() + 1));

                if (node * processorsPerNode < totalProcessors)
                {
                    var startCpu = node * processorsPerNode;
                    var endCpu = Math.Min(startCpu + processorsPerNode - 1, totalProcessors - 1);

                    cpuMask = CreateCpuMask(startCpu, endCpu);
                    cpuCount = endCpu - startCpu + 1;
                    return cpuCount > 0;
                }
            }

            // Try alternative Windows-specific approach using P/Invoke
            return TryGetNumaCpusWindowsNative(node, out cpuMask, out cpuCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting Windows NUMA CPUs: {ex.Message}");
        }

        return false;
    }

    private static bool GetNumaCpusMacOS(int node, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            // macOS doesn't have traditional NUMA, but we can simulate based on CPU topology
            using (var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n hw.ncpu hw.physicalcpu hw.logicalcpu",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            })
            {
                _ = process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    var values = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length >= 3)
                    {
                        if (int.TryParse(values[0], out var totalCpus))
                        {
                            // Simulate NUMA nodes for macOS (typically single node or efficiency/performance cores)
                            var cpusPerNode = Math.Max(1, totalCpus / 2); // Assume 2 "nodes" for Apple Silicon

                            if (node == 0)
                            {
                                // Performance cores
                                cpuMask = CreateCpuMask(0, cpusPerNode - 1);
                                cpuCount = cpusPerNode;
                                return true;
                            }
                            else if (node == 1 && totalCpus > cpusPerNode)
                            {
                                // Efficiency cores
                                cpuMask = CreateCpuMask(cpusPerNode, totalCpus - 1);
                                cpuCount = totalCpus - cpusPerNode;
                                return cpuCount > 0;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting macOS NUMA CPUs: {ex.Message}");
        }

        return false;
    }

    private static bool ParseCpuMask(string cpuMap, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            // CPU mask is typically in hex format like "00000000,00000fff"
            var cleanMask = cpuMap.Replace(",", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);

            // Parse as hex and convert to CPU mask
            if (ulong.TryParse(cleanMask, System.Globalization.NumberStyles.HexNumber, null, out cpuMask))
            {
                cpuCount = CountSetBits(cpuMask);
                return cpuCount > 0;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return false;
    }

    private static bool IsNumaLibraryAvailable()
    {
        try
        {
            // Try to call numa_available() to check if library is present
            return numa_available() != -1;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetNumaCpusNative(int node, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            // This would call the actual numa_node_to_cpus function from libnuma
            // For now, we'll use a simplified approach
            var maxNode = numa_max_node();
            if (node <= maxNode)
            {
                // Estimate CPU count per node
                var totalCpus = Environment.ProcessorCount;
                var nodesCount = maxNode + 1;
                var cpusPerNode = Math.Max(1, totalCpus / nodesCount);

                var startCpu = node * cpusPerNode;
                var endCpu = Math.Min(startCpu + cpusPerNode - 1, totalCpus - 1);

                cpuMask = CreateCpuMask(startCpu, endCpu);
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

    private static bool TryParseNumaFromCpuInfo(int node, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            var cpuInfoPath = "/proc/cpuinfo";
            if (!File.Exists(cpuInfoPath))
            {
                return false;
            }

            var lines = File.ReadAllLines(cpuInfoPath);
            var cpuToNode = new Dictionary<int, int>();
            var currentProcessor = -1;

            foreach (var line in lines)
            {
                if (line.StartsWith("processor", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var processorId))
                    {
                        currentProcessor = processorId;
                    }
                }
                else if (line.StartsWith("physical id", StringComparison.OrdinalIgnoreCase) && currentProcessor >= 0)
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var physicalId))
                    {
                        cpuToNode[currentProcessor] = physicalId;
                    }
                }
            }

            // Build CPU mask for the specified node
            var nodeCpus = cpuToNode.Where(kvp => kvp.Value == node).Select(kvp => kvp.Key).ToList();
            if (nodeCpus.Count > 0)
            {
                foreach (var cpu in nodeCpus)
                {
                    if (cpu < 64) // Limit to 64 CPUs for ulong mask
                    {
                        cpuMask |= (1UL << cpu);
                    }
                }
                cpuCount = nodeCpus.Count;
                return cpuCount > 0;
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    private static int GetMaxNumaNodeWindows()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return 0;
            }

            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_ComputerSystem");

            foreach (System.Management.ManagementObject system in searcher.Get())
            {
                // Simple heuristic: assume 1 NUMA node per 8 logical processors
                var logicalProcessors = Convert.ToInt32(system["NumberOfLogicalProcessors"], CultureInfo.InvariantCulture);
                return Math.Max(0, (logicalProcessors - 1) / 8);
            }
        }
        catch
        {
            // Ignore errors
        }

        return 0;
    }

    private static bool TryGetNumaCpusWindowsNative(int node, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            // In a full implementation, this would use GetNumaNodeProcessorMask
            // For now, provide a reasonable fallback
            var totalProcessors = Environment.ProcessorCount;
            var estimatedNodes = Math.Max(1, totalProcessors / 8); // Assume 8 cores per NUMA node

            if (node < estimatedNodes)
            {
                var cpusPerNode = totalProcessors / estimatedNodes;
                var startCpu = node * cpusPerNode;
                var endCpu = Math.Min(startCpu + cpusPerNode - 1, totalProcessors - 1);

                cpuMask = CreateCpuMask(startCpu, endCpu);
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

    private static ulong CreateCpuMask(int startCpu, int endCpu)
    {
        ulong mask = 0;
        for (var i = startCpu; i <= endCpu && i < 64; i++)
        {
            mask |= (1UL << i);
        }
        return mask;
    }

    private static int CountSetBits(ulong value)
    {
        var count = 0;
        while (value != 0)
        {
            count++;
            value &= value - 1; // Clear lowest set bit
        }
        return count;
    }

    #endregion

    #region Helper Methods

    private static NumaTopology CreateFallbackTopology(int processorCount)
    {
        return new NumaTopology
        {
            NodeCount = 1,
            ProcessorCount = processorCount,
            Nodes =
            [
                new NumaNode
            {
                NodeId = 0,
                ProcessorMask = (1UL << Math.Min(processorCount, 64)) - 1,
                ProcessorCount = processorCount,
                MemorySize = 0,
                Group = 0,
                CacheCoherencyDomain = 0
            }
            ],
            DistanceMatrix = [[10]],
            CacheLineSize = 64,
            PageSize = 4096,
            SupportsMemoryBinding = false,
            SupportsMemoryPolicy = false
        };
    }

    private static int[][] EstimateDistanceMatrix(int nodeCount)
    {
        var matrix = new int[nodeCount][];
        for (var i = 0; i < nodeCount; i++)
        {
            matrix[i] = new int[nodeCount];
            for (var j = 0; j < nodeCount; j++)
            {
                matrix[i][j] = i == j ? 10 : 20; // Standard NUMA distances
            }
        }
        return matrix;
    }

    private static int PopCount(ulong value)
    {
        // Population count (number of set bits)
        var count = 0;
        while (value != 0)
        {
            count++;
            value &= value - 1; // Clear the lowest set bit
        }
        return count;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static ulong GetProcessorMaskFromWmi(System.Management.ManagementObject node)
    {
        // Extract processor mask from WMI object
        try
        {
            var processorMask = node["ProcessorMask"];
            if (processorMask != null)
            {
                return Convert.ToUInt64(processorMask, CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // Ignore errors
        }
        return 0;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static long GetMemorySizeFromWmi(System.Management.ManagementObject node)
    {
        try
        {
            var memorySize = node["MemorySize"];
            if (memorySize != null)
            {
                return Convert.ToInt64(memorySize, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // Ignore errors
        }
        return 0;
    }

    private static long GetNumaNodeMemorySize(int nodeId)
    {
        // Windows-specific memory size query
        if (GetNumaAvailableMemoryNodeEx((ushort)nodeId, out var availableBytes))
        {
            return (long)availableBytes;
        }
        return 0;
    }

    private static int GetCacheCoherencyDomain(int nodeId) => nodeId; // For most systems, cache coherency domain matches NUMA node

    private static int GetNumaNodeDistance(int fromNode, int toNode) => fromNode == toNode ? 10 : 20; // Default NUMA distances if system doesn't provide them

    private static int GetCacheLineSize() => 64; // Most modern processors use 64-byte cache lines

    private static int GetPageSize()
    {
        if (GetSystemInfo(out var sysInfo))
        {
            return (int)sysInfo.PageSize;
        }
        return 4096; // Default page size
    }

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

    private static long GetLinuxNodeMemorySizeSysfs(string nodeDir) => GetNodeMemorySize(nodeDir);

    private static HugePagesInfo GetLinuxHugePagesInfo(int nodeId) => GetLinuxHugePagesSysfs($"/sys/devices/system/node/node{nodeId}");

    private static HugePagesInfo GetLinuxHugePagesSysfs(string nodeDir)
    {
        var hugePagesDir = Path.Combine(nodeDir, "hugepages");
        var supportedSizes = new List<HugePageSize>();

        try
        {
            if (Directory.Exists(hugePagesDir))
            {
                var sizeDirectories = Directory.GetDirectories(hugePagesDir, "hugepages-*");
                foreach (var sizeDir in sizeDirectories)
                {
                    var sizeName = Path.GetFileName(sizeDir);
                    var match = HugePagesRegex().Match(sizeName);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var sizeKb))
                    {
                        var freeFile = Path.Combine(sizeDir, "free_hugepages");
                        var totalFile = Path.Combine(sizeDir, "nr_hugepages");

                        var freePages = File.Exists(freeFile) ? ReadIntFromFile(freeFile) : 0;
                        var totalPages = File.Exists(totalFile) ? ReadIntFromFile(totalFile) : 0;

                        supportedSizes.Add(new HugePageSize
                        {
                            SizeInBytes = sizeKb * 1024L,
                            FreePages = freePages,
                            TotalPages = totalPages
                        });
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return new HugePagesInfo { SupportedSizes = supportedSizes };
    }

    private static CacheHierarchy GetLinuxCacheInfo(int nodeId) => GetLinuxCacheInfoSysfs(nodeId);

    private static CacheHierarchy GetLinuxCacheInfoSysfs(int nodeId)
    {
        var levels = new List<CacheLevel>();

        try
        {
            // Get cache information from first CPU in the node
            var cpuListPath = $"/sys/devices/system/node/node{nodeId}/cpulist";
            if (File.Exists(cpuListPath))
            {
                var cpuList = File.ReadAllText(cpuListPath).Trim();
                var firstCpu = ParseFirstCpu(cpuList);
                if (firstCpu >= 0)
                {
                    var cacheDir = $"/sys/devices/system/cpu/cpu{firstCpu}/cache";
                    if (Directory.Exists(cacheDir))
                    {
                        var cacheDirs = Directory.GetDirectories(cacheDir, "index*");
                        foreach (var indexDir in cacheDirs)
                        {
                            var level = ReadCacheLevel(indexDir);
                            if (level != null)
                            {
                                levels.Add(level);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return new CacheHierarchy { Levels = levels };
    }

    private static int GetLinuxCacheLineSize()
    {
        try
        {
            var coherencyLineSizePath = "/sys/devices/system/cpu/cpu0/cache/index0/coherency_line_size";
            if (File.Exists(coherencyLineSizePath))
            {
                return ReadIntFromFile(coherencyLineSizePath);
            }
        }
        catch
        {
            // Ignore errors
        }
        return 64; // Default cache line size
    }

    private static int GetLinuxPageSize()
    {
        try
        {
            // Get page size from getconf
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "getconf",
                Arguments = "PAGESIZE",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (int.TryParse(output.Trim(), out var pageSize))
                {
                    return pageSize;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return 4096; // Default page size
    }

    private static int ReadIntFromFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath).Trim();
            return int.TryParse(content, out var value) ? value : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static int ParseFirstCpu(string cpuList)
    {
        var parts = cpuList.Split(',');
        if (parts.Length > 0)
        {
            var firstPart = parts[0];
            if (firstPart.Contains('-', StringComparison.Ordinal))
            {
                var range = firstPart.Split('-');
                if (int.TryParse(range[0], out var start))
                {
                    return start;
                }
            }
            else if (int.TryParse(firstPart, out var cpu))
            {
                return cpu;
            }
        }
        return -1;
    }

    private static CacheLevel? ReadCacheLevel(string indexDir)
    {
        try
        {
            var levelFile = Path.Combine(indexDir, "level");
            var sizeFile = Path.Combine(indexDir, "size");
            var typeFile = Path.Combine(indexDir, "type");

            if (File.Exists(levelFile) && File.Exists(sizeFile) && File.Exists(typeFile))
            {
                var level = ReadIntFromFile(levelFile);
                var sizeStr = File.ReadAllText(sizeFile).Trim();
                var type = File.ReadAllText(typeFile).Trim();

                // Parse size (e.g., "32K", "256K", "8M")
                var sizeMatch = CacheSizeRegex().Match(sizeStr);
                if (sizeMatch.Success && int.TryParse(sizeMatch.Groups[1].Value, out var size))
                {
                    var multiplier = sizeMatch.Groups[2].Value switch
                    {
                        "K" => 1024,
                        "M" => 1024 * 1024,
                        "G" => 1024 * 1024 * 1024,
                        _ => 1
                    };

                    return new CacheLevel
                    {
                        Level = level,
                        SizeInBytes = size * multiplier,
                        Type = type,
                        LineSize = GetCacheLineSize()
                    };
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    #endregion
}

/// <summary>
/// NUMA topology information with advanced features.
/// </summary>
public sealed class NumaTopology
{
    /// <summary>
    /// Gets the number of NUMA nodes.
    /// </summary>
    public required int NodeCount { get; init; }

    /// <summary>
    /// Gets the total processor count.
    /// </summary>
    public required int ProcessorCount { get; init; }

    /// <summary>
    /// Gets the NUMA nodes.
    /// </summary>
    public required IReadOnlyList<NumaNode> Nodes { get; init; }

    /// <summary>
    /// Gets the NUMA distance matrix (relative latency between nodes).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<int>>? DistanceMatrix { get; init; }

    /// <summary>
    /// Gets the cache line size in bytes.
    /// </summary>
    public int CacheLineSize { get; init; } = 64;

    /// <summary>
    /// Gets the page size in bytes.
    /// </summary>
    public int PageSize { get; init; } = 4096;

    /// <summary>
    /// Gets whether the system supports memory binding to specific nodes.
    /// </summary>
    public bool SupportsMemoryBinding { get; init; }

    /// <summary>
    /// Gets whether the system supports memory policy configuration.
    /// </summary>
    public bool SupportsMemoryPolicy { get; init; }

    /// <summary>
    /// Gets the node for a specific processor.
    /// </summary>
    public int GetNodeForProcessor(int processorId)
    {
        for (var i = 0; i < Nodes.Count; i++)
        {
            if ((Nodes[i].ProcessorMask & (1UL << processorId)) != 0)
            {
                return i;
            }
        }
        return 0;
    }

    /// <summary>
    /// Gets processors for a specific node.
    /// </summary>
    public IEnumerable<int> GetProcessorsForNode(int nodeId)
    {
        if (nodeId < 0 || nodeId >= Nodes.Count)
        {
            yield break;
        }

        var mask = Nodes[nodeId].ProcessorMask;
        for (var i = 0; i < 64; i++)
        {
            if ((mask & (1UL << i)) != 0)
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// Gets the distance between two NUMA nodes.
    /// </summary>
    public int GetDistance(int fromNode, int toNode)
    {
        if (DistanceMatrix == null ||
            fromNode < 0 || fromNode >= NodeCount ||
            toNode < 0 || toNode >= NodeCount)
        {
            return fromNode == toNode ? 10 : 20; // Default distances
        }

        return DistanceMatrix[fromNode][toNode];
    }

    /// <summary>
    /// Gets the closest nodes to a given node, ordered by distance.
    /// </summary>
    public IEnumerable<int> GetClosestNodes(int nodeId)
    {
        if (nodeId < 0 || nodeId >= NodeCount || DistanceMatrix == null)
        {
            yield break;
        }

        var distances = new List<(int node, int distance)>();
        for (var i = 0; i < NodeCount; i++)
        {
            if (i != nodeId)
            {
                distances.Add((i, DistanceMatrix[nodeId][i]));
            }
        }

        distances.Sort((a, b) => a.distance.CompareTo(b.distance));

        foreach (var (node, _) in distances)
        {
            yield return node;
        }
    }

    /// <summary>
    /// Gets the optimal node for memory allocation based on processor affinity.
    /// </summary>
    public int GetOptimalNodeForProcessors(IEnumerable<int> processorIds)
    {
        var processorList = processorIds.ToArray();
        if (processorList.Length == 0)
        {
            return 0;
        }

        // Count processors per node
        var nodeProcessorCounts = new int[NodeCount];
        foreach (var processorId in processorList)
        {
            var nodeId = GetNodeForProcessor(processorId);
            nodeProcessorCounts[nodeId]++;
        }

        // Return node with most processors
        var maxCount = 0;
        var bestNode = 0;
        for (var i = 0; i < NodeCount; i++)
        {
            if (nodeProcessorCounts[i] > maxCount)
            {
                maxCount = nodeProcessorCounts[i];
                bestNode = i;
            }
        }

        return bestNode;
    }

    /// <summary>
    /// Gets nodes sorted by available memory.
    /// </summary>
    public IEnumerable<int> GetNodesByAvailableMemory()
    {
        return Nodes
            .Select((node, index) => new { NodeId = index, node.MemorySize })
            .OrderByDescending(x => x.MemorySize)
            .Select(x => x.NodeId);
    }
}

/// <summary>
/// Information about a NUMA node with advanced features.
/// </summary>
public sealed class NumaNode
{
    /// <summary>
    /// Gets the node ID.
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// Gets the processor affinity mask.
    /// </summary>
    public required ulong ProcessorMask { get; init; }

    /// <summary>
    /// Gets the number of processors in this node.
    /// </summary>
    public required int ProcessorCount { get; init; }

    /// <summary>
    /// Gets the memory size in bytes (0 if unknown).
    /// </summary>
    public required long MemorySize { get; init; }

    /// <summary>
    /// Gets the processor group (Windows only, 0 for other platforms).
    /// </summary>
    public ushort Group { get; init; }

    /// <summary>
    /// Gets the cache coherency domain identifier.
    /// </summary>
    public int CacheCoherencyDomain { get; init; }

    /// <summary>
    /// Gets information about huge pages support on this node.
    /// </summary>
    public HugePagesInfo? HugePagesInfo { get; init; }

    /// <summary>
    /// Gets the cache hierarchy information for this node.
    /// </summary>
    public CacheHierarchy? CacheHierarchy { get; init; }

    /// <summary>
    /// Gets the list of CPU IDs in this node.
    /// </summary>
    public IEnumerable<int> CpuList
    {
        get
        {
            for (var i = 0; i < 64; i++)
            {
                if ((ProcessorMask & (1UL << i)) != 0)
                {
                    yield return i;
                }
            }
        }
    }

    /// <summary>
    /// Checks if a specific CPU belongs to this node.
    /// </summary>
    public bool ContainsCpu(int cpuId) => cpuId >= 0 && cpuId < 64 && (ProcessorMask & (1UL << cpuId)) != 0;
}

/// <summary>
/// Information about huge pages support on a NUMA node.
/// </summary>
public sealed class HugePagesInfo
{
    /// <summary>
    /// Gets the supported huge page sizes.
    /// </summary>
    public required IReadOnlyList<HugePageSize> SupportedSizes { get; init; }

    /// <summary>
    /// Gets whether huge pages are supported on this node.
    /// </summary>
    public bool IsSupported => SupportedSizes.Count > 0;

    /// <summary>
    /// Gets the largest available huge page size.
    /// </summary>
    public long LargestPageSize => SupportedSizes.Count > 0 ? SupportedSizes.Max(s => s.SizeInBytes) : 0;
}

/// <summary>
/// Information about a specific huge page size.
/// </summary>
public sealed class HugePageSize
{
    /// <summary>
    /// Gets the page size in bytes.
    /// </summary>
    public required long SizeInBytes { get; init; }

    /// <summary>
    /// Gets the number of free pages of this size.
    /// </summary>
    public required int FreePages { get; init; }

    /// <summary>
    /// Gets the total number of pages of this size.
    /// </summary>
    public required int TotalPages { get; init; }

    /// <summary>
    /// Gets the total free memory in bytes for this page size.
    /// </summary>
    public long FreeMemoryBytes => FreePages * SizeInBytes;

    /// <summary>
    /// Gets the total memory in bytes for this page size.
    /// </summary>
    public long TotalMemoryBytes => TotalPages * SizeInBytes;
}

/// <summary>
/// Information about cache hierarchy on a NUMA node.
/// </summary>
public sealed class CacheHierarchy
{
    /// <summary>
    /// Gets the cache levels.
    /// </summary>
    public required IReadOnlyList<CacheLevel> Levels { get; init; }

    /// <summary>
    /// Gets a specific cache level.
    /// </summary>
    public CacheLevel? GetLevel(int level) => Levels.FirstOrDefault(l => l.Level == level);

    /// <summary>
    /// Gets the total cache size across all levels.
    /// </summary>
    public long TotalCacheSize => Levels.Sum(l => l.SizeInBytes);
}

/// <summary>
/// Information about a specific cache level.
/// </summary>
public sealed class CacheLevel
{
    /// <summary>
    /// Gets the cache level (1, 2, 3, etc.).
    /// </summary>
    public required int Level { get; init; }

    /// <summary>
    /// Gets the cache size in bytes.
    /// </summary>
    public required long SizeInBytes { get; init; }

    /// <summary>
    /// Gets the cache type (Data, Instruction, Unified).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the cache line size in bytes.
    /// </summary>
    public required int LineSize { get; init; }
}

// Add GeneratedRegex methods at the end for AOT compatibility
public static partial class NumaInfo
{
    [GeneratedRegex(@"(\d+)\s*kB")]
    private static partial Regex MemorySizeRegex();

    [GeneratedRegex(@"hugepages-(\d+)kB")]
    private static partial Regex HugePagesRegex();

    [GeneratedRegex(@"(\d+)([KMG]?)")]
    private static partial Regex CacheSizeRegex();
}
