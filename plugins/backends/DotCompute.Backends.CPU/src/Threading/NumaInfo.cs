// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DotCompute.Backends.CPU.Threading;

/// <summary>
/// Provides information about NUMA (Non-Uniform Memory Access) topology.
/// </summary>
public static class NumaInfo
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
                Nodes = new[]
                {
                    new NumaNode
                    {
                        NodeId = 0,
                        ProcessorMask = (1UL << Environment.ProcessorCount) - 1,
                        ProcessorCount = Environment.ProcessorCount,
                        MemorySize = 0 // Unknown
                    }
                }
            };
        }
    }
    
    private static NumaTopology DiscoverWindowsTopology()
    {
        try
        {
            // Use Windows NUMA API for production-grade topology discovery
            return DiscoverWindowsTopologyNative();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to discover Windows NUMA topology: {ex.Message}");
            // Fall back to WMI-based discovery
            return DiscoverWindowsTopologyWmi();
        }
    }
    
    private static NumaTopology DiscoverWindowsTopologyNative()
    {
        var nodes = new List<NumaNode>();
        var nodeCount = GetNumaHighestNodeNumber() + 1;
        var processorCount = Environment.ProcessorCount;
        var distanceMatrix = new int[nodeCount, nodeCount];
        
        for (int nodeId = 0; nodeId < nodeCount; nodeId++)
        {
            // Get processor mask for this node
            if (GetNumaNodeProcessorMaskEx((ushort)nodeId, out GROUP_AFFINITY affinity))
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
            for (int otherNodeId = 0; otherNodeId < nodeCount; otherNodeId++)
            {
                distanceMatrix[nodeId, otherNodeId] = GetNumaNodeDistance(nodeId, otherNodeId);
            }
        }
        
        return new NumaTopology
        {
            NodeCount = nodeCount,
            ProcessorCount = processorCount,
            Nodes = nodes.ToArray(),
            DistanceMatrix = distanceMatrix,
            CacheLineSize = GetCacheLineSize(),
            PageSize = GetPageSize()
        };
    }
    
    private static NumaTopology DiscoverWindowsTopologyWmi()
    {
        var nodes = new List<NumaNode>();
        var processorCount = Environment.ProcessorCount;
        
        try
        {
            // Use WMI as fallback for Windows topology discovery
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_NumaNode");
            
            var results = searcher.Get();
            if (results.Count > 0)
            {
                foreach (System.Management.ManagementObject node in results)
                {
                    var nodeId = Convert.ToInt32(node["NodeId"]);
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
            Nodes = nodes.ToArray(),
            DistanceMatrix = distanceMatrix,
            CacheLineSize = 64, // Standard cache line size
            PageSize = 4096 // Standard page size
        };
    }
    
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
    
    private static NumaTopology DiscoverLinuxTopologyAdvanced()
    {
        var processorCount = Environment.ProcessorCount;
        var nodes = new List<NumaNode>();
        
        // First try libnuma integration
        if (TryLoadLibnuma())
        {
            var nodeCount = numa_max_node() + 1;
            var distanceMatrix = new int[nodeCount, nodeCount];
            
            for (int nodeId = 0; nodeId < nodeCount; nodeId++)
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
                for (int otherNodeId = 0; otherNodeId < nodeCount; otherNodeId++)
                {
                    distanceMatrix[nodeId, otherNodeId] = numa_distance(nodeId, otherNodeId);
                }
            }
            
            return new NumaTopology
            {
                NodeCount = nodeCount,
                ProcessorCount = processorCount,
                Nodes = nodes.ToArray(),
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
    
    private static NumaTopology DiscoverLinuxTopologySysfs()
    {
        var processorCount = Environment.ProcessorCount;
        
        try
        {
            // Enhanced sysfs-based discovery
            if (Directory.Exists("/sys/devices/system/node"))
            {
                var nodeDirs = Directory.GetDirectories("/sys/devices/system/node", "node*")
                    .OrderBy(d => int.Parse(Path.GetFileName(d).Substring(4)))
                    .ToArray();
                
                var nodes = new List<NumaNode>();
                var nodeCount = nodeDirs.Length;
                var distanceMatrix = new int[nodeCount, nodeCount];
                
                for (int i = 0; i < nodeDirs.Length; i++)
                {
                    var nodeDir = nodeDirs[i];
                    var nodeId = int.Parse(Path.GetFileName(nodeDir).Substring(4));
                    
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
                        for (int j = 0; j < Math.Min(distances.Length, nodeCount); j++)
                        {
                            if (int.TryParse(distances[j], out var distance))
                            {
                                distanceMatrix[i, j] = distance;
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
                        Nodes = nodes.ToArray(),
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
        int count = 0;
        
        var parts = cpuList.Split(',');
        foreach (var part in parts)
        {
            if (part.Contains('-'))
            {
                var range = part.Split('-');
                var start = int.Parse(range[0]);
                var end = int.Parse(range[1]);
                
                for (int i = start; i <= end; i++)
                {
                    mask |= (1UL << i);
                    count++;
                }
            }
            else
            {
                var cpu = int.Parse(part);
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
                    if (line.StartsWith($"Node {Path.GetFileName(nodeDir).Substring(4)} MemTotal:"))
                    {
                        var parts = line.Split(':')[1].Trim().Split(' ');
                        return long.Parse(parts[0]) * 1024; // Convert from KB to bytes
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
    private static extern bool GetNumaNodeProcessorMaskEx(ushort Node, out GROUP_AFFINITY ProcessorMask);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNumaHighestNodeNumber(out uint HighestNodeNumber);
    
    private static int GetNumaHighestNodeNumber()
    {
        return GetNumaHighestNodeNumber(out var highest) ? (int)highest : 0;
    }
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNumaNodeProcessorMask(byte Node, out ulong ProcessorMask);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNumaAvailableMemoryNodeEx(ushort Node, out ulong AvailableBytes);
    
    [DllImport("kernel32.dll", SetLastError = true)]
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
    
    private static bool _libnumaLoaded = false;
    private static bool _libnumaAvailable = false;
    
    private static bool TryLoadLibnuma()
    {
        if (_libnumaLoaded) return _libnumaAvailable;
        
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
    private static extern int numa_available();
    
    [DllImport("numa", EntryPoint = "numa_max_node")]
    private static extern int numa_max_node();
    
    [DllImport("numa", EntryPoint = "numa_distance")]
    private static extern int numa_distance(int from, int to);
    
    private static bool numa_node_to_cpus(int node, out ulong cpuMask, out int cpuCount)
    {
        // Simplified implementation - in reality would call numa_node_to_cpus
        cpuMask = 0;
        cpuCount = 0;
        
        try
        {
            var cpuListPath = $"/sys/devices/system/node/node{node}/cpulist";
            if (File.Exists(cpuListPath))
            {
                var cpuList = File.ReadAllText(cpuListPath).Trim();
                var (mask, count) = ParseCpuList(cpuList);
                cpuMask = mask;
                cpuCount = count;
                return true;
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return false;
    }
    
    #endregion
    
    #region Helper Methods
    
    private static NumaTopology CreateFallbackTopology(int processorCount)
    {
        return new NumaTopology
        {
            NodeCount = 1,
            ProcessorCount = processorCount,
            Nodes = new[]
            {
                new NumaNode
                {
                    NodeId = 0,
                    ProcessorMask = (1UL << Math.Min(processorCount, 64)) - 1,
                    ProcessorCount = processorCount,
                    MemorySize = 0,
                    Group = 0,
                    CacheCoherencyDomain = 0
                }
            },
            DistanceMatrix = new int[,] { { 10 } },
            CacheLineSize = 64,
            PageSize = 4096,
            SupportsMemoryBinding = false,
            SupportsMemoryPolicy = false
        };
    }
    
    private static int[,] EstimateDistanceMatrix(int nodeCount)
    {
        var matrix = new int[nodeCount, nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            for (int j = 0; j < nodeCount; j++)
            {
                matrix[i, j] = i == j ? 10 : 20; // Standard NUMA distances
            }
        }
        return matrix;
    }
    
    private static int PopCount(ulong value)
    {
        // Population count (number of set bits)
        int count = 0;
        while (value != 0)
        {
            count++;
            value &= value - 1; // Clear the lowest set bit
        }
        return count;
    }
    
    private static ulong GetProcessorMaskFromWmi(System.Management.ManagementObject node)
    {
        // Extract processor mask from WMI object
        try
        {
            var processorMask = node["ProcessorMask"];
            if (processorMask != null)
            {
                return Convert.ToUInt64(processorMask);
            }
        }
        catch
        {
            // Ignore errors
        }
        return 0;
    }
    
    private static long GetMemorySizeFromWmi(System.Management.ManagementObject node)
    {
        try
        {
            var memorySize = node["MemorySize"];
            if (memorySize != null)
            {
                return Convert.ToInt64(memorySize);
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
    
    private static int GetCacheCoherencyDomain(int nodeId)
    {
        // For most systems, cache coherency domain matches NUMA node
        return nodeId;
    }
    
    private static int GetNumaNodeDistance(int fromNode, int toNode)
    {
        // Default NUMA distances if system doesn't provide them
        return fromNode == toNode ? 10 : 20;
    }
    
    private static int GetCacheLineSize()
    {
        // Most modern processors use 64-byte cache lines
        return 64;
    }
    
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
                    if (line.StartsWith($"Node {nodeId} MemTotal:"))
                    {
                        var match = Regex.Match(line, @"(\d+)\s*kB");
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
    
    private static long GetLinuxNodeMemorySizeSysfs(string nodeDir)
    {
        return GetNodeMemorySize(nodeDir);
    }
    
    private static HugePagesInfo GetLinuxHugePagesInfo(int nodeId)
    {
        return GetLinuxHugePagesSysfs($"/sys/devices/system/node/node{nodeId}");
    }
    
    private static HugePagesInfo GetLinuxHugePagesSysfs(string nodeDir)
    {
        var hugePagesDir = Path.Combine(nodeDir, "hugepages");
        var hugePagesInfo = new HugePagesInfo { SupportedSizes = new List<HugePageSize>() };
        
        try
        {
            if (Directory.Exists(hugePagesDir))
            {
                var sizeDirectories = Directory.GetDirectories(hugePagesDir, "hugepages-*");
                foreach (var sizeDir in sizeDirectories)
                {
                    var sizeName = Path.GetFileName(sizeDir);
                    var match = Regex.Match(sizeName, @"hugepages-(\d+)kB");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var sizeKb))
                    {
                        var freeFile = Path.Combine(sizeDir, "free_hugepages");
                        var totalFile = Path.Combine(sizeDir, "nr_hugepages");
                        
                        var freePages = File.Exists(freeFile) ? ReadIntFromFile(freeFile) : 0;
                        var totalPages = File.Exists(totalFile) ? ReadIntFromFile(totalFile) : 0;
                        
                        hugePagesInfo.SupportedSizes.Add(new HugePageSize
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
        
        return hugePagesInfo;
    }
    
    private static CacheHierarchy GetLinuxCacheInfo(int nodeId)
    {
        return GetLinuxCacheInfoSysfs(nodeId);
    }
    
    private static CacheHierarchy GetLinuxCacheInfoSysfs(int nodeId)
    {
        var cacheInfo = new CacheHierarchy { Levels = new List<CacheLevel>() };
        
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
                                cacheInfo.Levels.Add(level);
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
        
        return cacheInfo;
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
            if (firstPart.Contains('-'))
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
                var sizeMatch = Regex.Match(sizeStr, @"(\d+)([KMG]?)");
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
    public required NumaNode[] Nodes { get; init; }
    
    /// <summary>
    /// Gets the NUMA distance matrix (relative latency between nodes).
    /// </summary>
    public int[,]? DistanceMatrix { get; init; }
    
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
        for (int i = 0; i < Nodes.Length; i++)
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
        if (nodeId < 0 || nodeId >= Nodes.Length)
            yield break;
            
        var mask = Nodes[nodeId].ProcessorMask;
        for (int i = 0; i < 64; i++)
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
        
        return DistanceMatrix[fromNode, toNode];
    }
    
    /// <summary>
    /// Gets the closest nodes to a given node, ordered by distance.
    /// </summary>
    public IEnumerable<int> GetClosestNodes(int nodeId)
    {
        if (nodeId < 0 || nodeId >= NodeCount || DistanceMatrix == null)
            yield break;
            
        var distances = new List<(int node, int distance)>();
        for (int i = 0; i < NodeCount; i++)
        {
            if (i != nodeId)
            {
                distances.Add((i, DistanceMatrix[nodeId, i]));
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
            return 0;
        
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
        for (int i = 0; i < NodeCount; i++)
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
            .Select((node, index) => new { NodeId = index, MemorySize = node.MemorySize })
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
    public IEnumerable<int> GetCpuList()
    {
        for (int i = 0; i < 64; i++)
        {
            if ((ProcessorMask & (1UL << i)) != 0)
            {
                yield return i;
            }
        }
    }
    
    /// <summary>
    /// Checks if a specific CPU belongs to this node.
    /// </summary>
    public bool ContainsCpu(int cpuId)
    {
        return cpuId >= 0 && cpuId < 64 && (ProcessorMask & (1UL << cpuId)) != 0;
    }
}

/// <summary>
/// Information about huge pages support on a NUMA node.
/// </summary>
public sealed class HugePagesInfo
{
    /// <summary>
    /// Gets the supported huge page sizes.
    /// </summary>
    public required List<HugePageSize> SupportedSizes { get; init; }
    
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
    public required List<CacheLevel> Levels { get; init; }
    
    /// <summary>
    /// Gets a specific cache level.
    /// </summary>
    public CacheLevel? GetLevel(int level)
    {
        return Levels.FirstOrDefault(l => l.Level == level);
    }
    
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