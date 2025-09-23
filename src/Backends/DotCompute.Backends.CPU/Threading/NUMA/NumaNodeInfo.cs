// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Threading.NUMA;

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
            for (var i = 0; i < NumaConstants.Limits.MaxCpusInMask; i++)
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
    /// <param name="cpuId">The CPU ID to check.</param>
    /// <returns>True if the CPU belongs to this node.</returns>
    public bool ContainsCpu(int cpuId) =>
        cpuId >= 0 && cpuId < NumaConstants.Limits.MaxCpusInMask &&
        (ProcessorMask & (1UL << cpuId)) != 0;

    /// <summary>
    /// Gets the CPU utilization estimate for this node.
    /// </summary>
    /// <returns>Estimated utilization between 0.0 and 1.0.</returns>
    public static double GetEstimatedUtilization()
    {
        // This would be implemented with actual performance counters in production
        return 0.0;
    }

    /// <summary>
    /// Gets the memory bandwidth estimate for this node.
    /// </summary>
    /// <returns>Estimated memory bandwidth in GB/s.</returns>
    public double GetEstimatedMemoryBandwidth()
    {
        // This would be implemented with actual memory benchmarks in production
        return ProcessorCount * 25.0; // Rough estimate: 25 GB/s per core
    }
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
    public int CacheLineSize { get; init; } = NumaConstants.Sizes.CacheLineSize;

    /// <summary>
    /// Gets the page size in bytes.
    /// </summary>
    public int PageSize { get; init; } = NumaConstants.Sizes.PageSize;

    /// <summary>
    /// Gets whether the system supports memory binding to specific nodes.
    /// </summary>
    public bool SupportsMemoryBinding { get; init; }

    /// <summary>
    /// Gets whether the system supports memory policy configuration.
    /// </summary>
    public bool SupportsMemoryPolicy { get; init; }

    /// <summary>
    /// Gets the total memory size across all NUMA nodes in bytes.
    /// </summary>
    public long TotalMemoryBytes => Nodes.Sum(n => n.MemorySize);

    /// <summary>
    /// Gets the node for a specific processor.
    /// </summary>
    /// <param name="processorId">The processor ID.</param>
    /// <returns>The node ID containing the processor.</returns>
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
    /// <param name="nodeId">The node ID.</param>
    /// <returns>Enumerable of processor IDs in the node.</returns>
    public IEnumerable<int> GetProcessorsForNode(int nodeId)
    {
        if (nodeId < 0 || nodeId >= Nodes.Count)
        {
            yield break;
        }

        var mask = Nodes[nodeId].ProcessorMask;
        for (var i = 0; i < NumaConstants.Limits.MaxCpusInMask; i++)
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
    /// <param name="fromNode">Source node ID.</param>
    /// <param name="toNode">Target node ID.</param>
    /// <returns>Distance value (lower is closer).</returns>
    public int GetDistance(int fromNode, int toNode)
    {
        if (DistanceMatrix == null ||
            fromNode < 0 || fromNode >= NodeCount ||
            toNode < 0 || toNode >= NodeCount)
        {
            return fromNode == toNode ? NumaConstants.Distances.Local : NumaConstants.Distances.Remote;
        }

        return DistanceMatrix[fromNode][toNode];
    }

    /// <summary>
    /// Gets the closest nodes to a given node, ordered by distance.
    /// </summary>
    /// <param name="nodeId">The reference node ID.</param>
    /// <returns>Enumerable of node IDs ordered by proximity.</returns>
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
    /// <param name="processorIds">Processor IDs to consider.</param>
    /// <returns>Optimal node ID for allocation.</returns>
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
    /// <returns>Enumerable of node IDs sorted by memory availability.</returns>
    public IEnumerable<int> GetNodesByAvailableMemory()
    {
        return Nodes
            .Select((node, index) => new { NodeId = index, node.MemorySize })
            .OrderByDescending(x => x.MemorySize)
            .Select(x => x.NodeId);
    }

    /// <summary>
    /// Checks if the system is a NUMA system.
    /// </summary>
    public bool IsNumaSystem => NodeCount > 1;

    /// <summary>
    /// Gets load balancing statistics for all nodes.
    /// </summary>
    /// <returns>Load balancing information.</returns>
    public NumaLoadBalanceInfo GetLoadBalanceInfo()
    {
        var totalProcessors = ProcessorCount;
        var avgProcessorsPerNode = (double)totalProcessors / NodeCount;
        var variance = Nodes.Select(n => Math.Pow(n.ProcessorCount - avgProcessorsPerNode, 2)).Average();

        return new NumaLoadBalanceInfo
        {
            TotalNodes = NodeCount,
            TotalProcessors = totalProcessors,
            AverageProcessorsPerNode = avgProcessorsPerNode,
            ProcessorDistributionVariance = variance,
            IsBalanced = variance < (avgProcessorsPerNode * 0.1) // Within 10% is considered balanced
        };
    }
}

/// <summary>
/// Load balancing information for NUMA topology.
/// </summary>
public sealed class NumaLoadBalanceInfo
{
    /// <summary>Total number of NUMA nodes.</summary>
    public required int TotalNodes { get; init; }

    /// <summary>Total number of processors.</summary>
    public required int TotalProcessors { get; init; }

    /// <summary>Average processors per node.</summary>
    public required double AverageProcessorsPerNode { get; init; }

    /// <summary>Variance in processor distribution.</summary>
    public required double ProcessorDistributionVariance { get; init; }

    /// <summary>Whether the topology is well-balanced.</summary>
    public required bool IsBalanced { get; init; }
}