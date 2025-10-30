// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CPU.Threading.NUMA;

namespace DotCompute.Backends.CPU.Threading;

/// <summary>
/// Provides information about NUMA (Non-Uniform Memory Access) topology.
/// This is a thin facade over the modular NUMA components.
/// </summary>
public static class NumaInfo
{
    /// <summary>
    /// Gets the NUMA topology of the system.
    /// </summary>
    public static NumaTopology Topology => NumaTopologyDetector.Topology;

    /// <summary>
    /// Gets whether the system has NUMA support.
    /// </summary>
    public static bool IsNumaSystem => Topology.IsNumaSystem;

    /// <summary>
    /// Gets platform capabilities for NUMA operations.
    /// </summary>
    public static NumaPlatformCapabilities PlatformCapabilities => NumaPlatformDetector.DetectCapabilities();

    /// <summary>
    /// Forces a re-discovery of the NUMA topology.
    /// </summary>
    /// <returns>Newly discovered topology.</returns>
    public static NumaTopology RediscoverTopology() => NumaTopologyDetector.RediscoverTopology();

    /// <summary>
    /// Creates a new NUMA affinity manager.
    /// </summary>
    /// <returns>NUMA affinity manager instance.</returns>
    public static NumaAffinityManager CreateAffinityManager() => new(Topology);

    /// <summary>
    /// Creates a new NUMA memory manager.
    /// </summary>
    /// <returns>NUMA memory manager instance.</returns>
    public static NumaMemoryManager CreateMemoryManager() => new(Topology);

    /// <summary>
    /// Gets CPU mapping information for a specific node.
    /// </summary>
    /// <param name="nodeId">NUMA node ID.</param>
    /// <param name="cpuMask">CPU mask for the node.</param>
    /// <param name="cpuCount">Number of CPUs in the node.</param>
    /// <returns>True if mapping information was retrieved successfully.</returns>
    public static bool GetNodeCpuMapping(int nodeId, out ulong cpuMask, out int cpuCount) => NumaPlatformDetector.GetNodeCpuMapping(nodeId, out cpuMask, out cpuCount);

    /// <summary>
    /// Gets memory size information for a specific node.
    /// </summary>
    /// <param name="nodeId">NUMA node ID.</param>
    /// <returns>Memory size in bytes, or 0 if unknown.</returns>
    public static long GetNodeMemorySize(int nodeId) => NumaPlatformDetector.GetNodeMemorySize(nodeId);
}
