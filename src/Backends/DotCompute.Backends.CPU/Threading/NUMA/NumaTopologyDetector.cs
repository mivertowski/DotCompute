// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Text.RegularExpressions;

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Hardware topology detection for NUMA systems.
/// </summary>
public static partial class NumaTopologyDetector
{
    private static readonly Lazy<NumaTopology> _topology = new(DiscoverTopology);

    /// <summary>
    /// Gets the discovered NUMA topology.
    /// </summary>
    public static NumaTopology Topology => _topology.Value;

    /// <summary>
    /// Discovers the NUMA topology for the current system.
    /// </summary>
    /// <returns>Discovered NUMA topology.</returns>
    public static NumaTopology DiscoverTopology()
    {
        try
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
            else if (OperatingSystem.IsMacOS())
            {
                return DiscoverMacOSTopology();
            }
            else
            {
                return CreateFallbackTopology();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to discover NUMA topology: {ex.Message}");
            return CreateFallbackTopology();
        }
    }

    /// <summary>
    /// Forces a re-discovery of the NUMA topology.
    /// </summary>
    /// <returns>Newly discovered topology.</returns>
    public static NumaTopology RediscoverTopology()
        // Clear the cached topology and discover again

        => DiscoverTopology();

    #region Windows Discovery

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
        var nodeCount = NumaInterop.GetNumaHighestNodeNumber() + 1;
        var processorCount = Environment.ProcessorCount;
        var distanceMatrix = CreateDistanceMatrix(nodeCount);

        for (var nodeId = 0; nodeId < nodeCount; nodeId++)
        {
            // Get processor mask for this node
            if (NumaInterop.GetNumaNodeProcessorMaskEx((ushort)nodeId, out var affinity))
            {
                var memorySize = NumaInterop.GetNumaNodeMemorySize(nodeId);
                var hugePagesInfo = GetWindowsHugePagesInfo(nodeId);
                var cacheInfo = GetWindowsCacheInfo(nodeId);

                nodes.Add(new NumaNode
                {
                    NodeId = nodeId,
                    ProcessorMask = affinity.Mask,
                    ProcessorCount = CpuUtilities.CountSetBits(affinity.Mask),
                    MemorySize = memorySize,
                    Group = affinity.Group,
                    CacheCoherencyDomain = nodeId,
                    HugePagesInfo = hugePagesInfo,
                    CacheHierarchy = cacheInfo
                });
            }

            // Build distance matrix
            for (var otherNodeId = 0; otherNodeId < nodeCount; otherNodeId++)
            {
                distanceMatrix[nodeId][otherNodeId] = GetWindowsNodeDistance(nodeId, otherNodeId);
            }
        }

        return new NumaTopology
        {
            NodeCount = nodeCount,
            ProcessorCount = processorCount,
            Nodes = nodes.AsReadOnly(),
            DistanceMatrix = distanceMatrix.Select(row => row.AsReadOnly()).ToList().AsReadOnly(),
            CacheLineSize = GetWindowsCacheLineSize(),
            PageSize = NumaInterop.GetPageSize(),
            SupportsMemoryBinding = true,
            SupportsMemoryPolicy = true
        };
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static NumaTopology DiscoverWindowsTopologyWmi()
    {
        var nodes = new List<NumaNode>();
        var processorCount = Environment.ProcessorCount;

        try
        {
            // Use WMI as fallback for Windows topology discovery
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NumaNode");
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
                        ProcessorCount = CpuUtilities.CountSetBits(processorMask),
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
            return CreateFallbackTopology();
        }

        if (nodes.Count == 0)
        {
            return CreateFallbackTopology();
        }

        var nodeCount = nodes.Count;
        var distanceMatrix = EstimateDistanceMatrix(nodeCount);

        return new NumaTopology
        {
            NodeCount = nodeCount,
            ProcessorCount = processorCount,
            Nodes = nodes.AsReadOnly(),
            DistanceMatrix = distanceMatrix.Select(row => row.AsReadOnly()).ToList().AsReadOnly(),
            CacheLineSize = NumaSizes.CacheLineSize,
            PageSize = NumaSizes.PageSize,
            SupportsMemoryBinding = false,
            SupportsMemoryPolicy = false
        };
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static HugePagesInfo GetWindowsHugePagesInfo(int nodeId)
        // Windows huge pages detection would go here

        => new()
        { SupportedSizes = [] };

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static CacheHierarchy GetWindowsCacheInfo(int nodeId)
        // Windows cache hierarchy detection would go here

        => new()
        { Levels = [] };

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int GetWindowsNodeDistance(int fromNode, int toNode) => fromNode == toNode ? NumaDistances.Local : NumaDistances.Remote;

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int GetWindowsCacheLineSize() => NumaSizes.CacheLineSize; // Windows detection would go here

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static ulong GetProcessorMaskFromWmi(ManagementObject node)
    {
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
    private static long GetMemorySizeFromWmi(ManagementObject node)
    {
        try
        {
            var memorySize = node["MemorySize"];
            if (memorySize != null)
            {
                return Convert.ToInt64(memorySize, CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // Ignore errors
        }
        return 0;
    }

    #endregion

    #region Linux Discovery

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
        if (NumaInterop.TryLoadLibnuma())
        {
            var nodeCount = NumaInterop.numa_max_node() + 1;
            var distanceMatrix = CreateDistanceMatrix(nodeCount);

            for (var nodeId = 0; nodeId < nodeCount; nodeId++)
            {
                if (NumaPlatformDetector.GetNodeCpuMapping(nodeId, out var cpuMask, out var cpuCount))
                {
                    var memorySize = NumaPlatformDetector.GetNodeMemorySize(nodeId);
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
                    distanceMatrix[nodeId][otherNodeId] = NumaInterop.numa_distance(nodeId, otherNodeId);
                }
            }

            return new NumaTopology
            {
                NodeCount = nodeCount,
                ProcessorCount = processorCount,
                Nodes = nodes.AsReadOnly(),
                DistanceMatrix = distanceMatrix.Select(row => row.AsReadOnly()).ToList().AsReadOnly(),
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
                var distanceMatrix = CreateDistanceMatrix(nodeCount);

                for (var i = 0; i < nodeDirs.Length; i++)
                {
                    var nodeDir = nodeDirs[i];
                    var nodeId = int.Parse(Path.GetFileName(nodeDir)[4..], CultureInfo.InvariantCulture);

                    var cpuListFile = Path.Combine(nodeDir, "cpulist");
                    if (File.Exists(cpuListFile))
                    {
                        var cpuList = File.ReadAllText(cpuListFile).Trim();
                        var (mask, count) = CpuUtilities.ParseCpuList(cpuList);
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
                        var distances = File.ReadAllText(distanceFile).Trim()
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
                        Nodes = nodes.AsReadOnly(),
                        DistanceMatrix = distanceMatrix.Select(row => row.AsReadOnly()).ToList().AsReadOnly(),
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
        return CreateFallbackTopology();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static HugePagesInfo GetLinuxHugePagesInfo(int nodeId) => GetLinuxHugePagesSysfs($"/sys/devices/system/node/node{nodeId}");

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
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

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static CacheHierarchy GetLinuxCacheInfo(int nodeId) => GetLinuxCacheInfoSysfs(nodeId);

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
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
                var firstCpu = CpuUtilities.ParseFirstCpu(cpuList);
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

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
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
        return NumaSizes.CacheLineSize;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
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
        return NumaSizes.PageSize;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static long GetLinuxNodeMemorySizeSysfs(string nodeDir)
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
                        if (long.TryParse(parts[0], CultureInfo.InvariantCulture, out var kb))
                        {
                            return kb * 1024; // Convert from KB to bytes
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

    #region macOS Discovery

    [System.Runtime.Versioning.SupportedOSPlatform("osx")]
    private static NumaTopology DiscoverMacOSTopology()
    {
        // macOS doesn't have traditional NUMA, but we can simulate based on CPU topology
        var processorCount = Environment.ProcessorCount;
        var nodeCount = 2; // Simulate efficiency + performance cores

        var nodes = new List<NumaNode>();
        for (var nodeId = 0; nodeId < nodeCount; nodeId++)
        {
            if (NumaPlatformDetector.GetNodeCpuMapping(nodeId, out var cpuMask, out var cpuCount) && cpuCount > 0)
            {
                nodes.Add(new NumaNode
                {
                    NodeId = nodeId,
                    ProcessorMask = cpuMask,
                    ProcessorCount = cpuCount,
                    MemorySize = 0, // macOS doesn't expose per-node memory
                    Group = 0,
                    CacheCoherencyDomain = nodeId
                });
            }
        }

        if (nodes.Count == 0)
        {
            return CreateFallbackTopology();
        }

        return new NumaTopology
        {
            NodeCount = nodes.Count,
            ProcessorCount = processorCount,
            Nodes = nodes.AsReadOnly(),
            DistanceMatrix = EstimateDistanceMatrix(nodes.Count).Select(row => row.AsReadOnly()).ToList().AsReadOnly(),
            CacheLineSize = NumaSizes.CacheLineSize,
            PageSize = NumaSizes.PageSize,
            SupportsMemoryBinding = false,
            SupportsMemoryPolicy = false
        };
    }

    #endregion

    #region Helper Methods

    private static NumaTopology CreateFallbackTopology()
    {
        var processorCount = Environment.ProcessorCount;
        return new NumaTopology
        {
            NodeCount = 1,
            ProcessorCount = processorCount,
            Nodes = new List<NumaNode>
            {
                new()
                {
                    NodeId = 0,
                    ProcessorMask = (1UL << Math.Min(processorCount, NumaLimits.MaxCpusInMask)) - 1,
                    ProcessorCount = processorCount,
                    MemorySize = 0,
                    Group = 0,
                    CacheCoherencyDomain = 0
                }
            }.AsReadOnly(),
            DistanceMatrix = new List<IReadOnlyList<int>>
            {
                new List<int> { NumaDistances.Local }.AsReadOnly()
            }.AsReadOnly(),
            CacheLineSize = NumaSizes.CacheLineSize,
            PageSize = NumaSizes.PageSize,
            SupportsMemoryBinding = false,
            SupportsMemoryPolicy = false
        };
    }

    private static int[][] CreateDistanceMatrix(int nodeCount)
    {
        var matrix = new int[nodeCount][];
        for (var i = 0; i < nodeCount; i++)
        {
            matrix[i] = new int[nodeCount];
            for (var j = 0; j < nodeCount; j++)
            {
                matrix[i][j] = i == j ? NumaDistances.Local : NumaDistances.Remote;
            }
        }
        return matrix;
    }

    private static int[][] EstimateDistanceMatrix(int nodeCount) => CreateDistanceMatrix(nodeCount);

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
                        LineSize = NumaSizes.CacheLineSize
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

    [GeneratedRegex(@"hugepages-(\d+)kB")]
    private static partial Regex HugePagesRegex();

    [GeneratedRegex(@"(\d+)([KMG]?)")]
    private static partial Regex CacheSizeRegex();

    #endregion
}