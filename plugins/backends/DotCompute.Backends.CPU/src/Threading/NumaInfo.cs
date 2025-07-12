// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

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
        // Windows NUMA API would be called here
        // For now, return a simple topology
        var processorCount = Environment.ProcessorCount;
        var nodeCount = Math.Max(1, processorCount / 8); // Assume 8 cores per node
        
        var nodes = new List<NumaNode>();
        var processorsPerNode = processorCount / nodeCount;
        
        for (int i = 0; i < nodeCount; i++)
        {
            var startProcessor = i * processorsPerNode;
            var endProcessor = (i == nodeCount - 1) ? processorCount : (i + 1) * processorsPerNode;
            var mask = 0UL;
            
            for (int p = startProcessor; p < endProcessor; p++)
            {
                mask |= (1UL << p);
            }
            
            nodes.Add(new NumaNode
            {
                NodeId = i,
                ProcessorMask = mask,
                ProcessorCount = endProcessor - startProcessor,
                MemorySize = 0 // Would be populated from Windows API
            });
        }
        
        return new NumaTopology
        {
            NodeCount = nodeCount,
            ProcessorCount = processorCount,
            Nodes = nodes.ToArray()
        };
    }
    
    private static NumaTopology DiscoverLinuxTopology()
    {
        // Linux NUMA discovery via /sys/devices/system/node/
        // For now, return a simple topology
        var processorCount = Environment.ProcessorCount;
        
        try
        {
            // Check if /sys/devices/system/node exists
            if (Directory.Exists("/sys/devices/system/node"))
            {
                var nodeDirs = Directory.GetDirectories("/sys/devices/system/node", "node*");
                var nodes = new List<NumaNode>();
                
                foreach (var nodeDir in nodeDirs)
                {
                    var nodeId = int.Parse(Path.GetFileName(nodeDir).Substring(4));
                    var cpuListFile = Path.Combine(nodeDir, "cpulist");
                    
                    if (File.Exists(cpuListFile))
                    {
                        var cpuList = File.ReadAllText(cpuListFile).Trim();
                        var (mask, count) = ParseCpuList(cpuList);
                        
                        nodes.Add(new NumaNode
                        {
                            NodeId = nodeId,
                            ProcessorMask = mask,
                            ProcessorCount = count,
                            MemorySize = GetNodeMemorySize(nodeDir)
                        });
                    }
                }
                
                if (nodes.Count > 0)
                {
                    return new NumaTopology
                    {
                        NodeCount = nodes.Count,
                        ProcessorCount = processorCount,
                        Nodes = nodes.ToArray()
                    };
                }
            }
        }
        catch
        {
            // Fall through to default
        }
        
        // Default single node
        return new NumaTopology
        {
            NodeCount = 1,
            ProcessorCount = processorCount,
            Nodes = new[]
            {
                new NumaNode
                {
                    NodeId = 0,
                    ProcessorMask = (1UL << processorCount) - 1,
                    ProcessorCount = processorCount,
                    MemorySize = 0
                }
            }
        };
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
}

/// <summary>
/// NUMA topology information.
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
}

/// <summary>
/// Information about a NUMA node.
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
}