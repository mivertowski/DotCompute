// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Memory.P2P.Types;

/// <summary>
/// Internal topology information for a single device.
/// </summary>
internal sealed class DeviceTopologyInfo
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Gets or sets the PCIe bus ID.
    /// </summary>
    public required string PcieBusId { get; init; }

    /// <summary>
    /// Gets or sets the NUMA node.
    /// </summary>
    public int NumaNode { get; init; }

    /// <summary>
    /// Gets or sets the list of directly connected peer device IDs.
    /// </summary>
    public required IList<string> DirectPeers { get; init; }

    /// <summary>
    /// Gets or sets when this topology info was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>
/// Internal candidate path for topology analysis.
/// </summary>
internal sealed class P2PPathCandidate
{
    /// <summary>
    /// Gets or sets the source device ID.
    /// </summary>
    public required string SourceDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the target device ID.
    /// </summary>
    public required string TargetDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the intermediate hop device IDs.
    /// </summary>
    public required IList<string> IntermediateHops { get; init; }

    /// <summary>
    /// Gets or sets the estimated total bandwidth in GB/s.
    /// </summary>
    public double EstimatedBandwidth { get; set; }
}

/// <summary>
/// Represents a P2P communication path between devices.
/// </summary>
public sealed class P2PPath
{
    /// <summary>
    /// Gets or sets the source device ID.
    /// </summary>
    public required string SourceDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the target device ID.
    /// </summary>
    public required string TargetDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the ordered list of device IDs forming the path.
    /// </summary>
    /// <remarks>
    /// Includes source, all intermediate hops, and target.
    /// </remarks>
    public required IList<string> PathDevices { get; init; }

    /// <summary>
    /// Gets or sets the number of hops in this path.
    /// </summary>
    public int HopCount => PathDevices.Count - 1;

    /// <summary>
    /// Gets or sets the estimated bandwidth in GB/s.
    /// </summary>
    public double EstimatedBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the estimated latency in microseconds.
    /// </summary>
    public double EstimatedLatencyUs { get; init; }
}

/// <summary>
/// Represents a P2P connection between two devices.
/// </summary>
public sealed class P2PConnection
{
    /// <summary>
    /// Gets or sets the source device ID.
    /// </summary>
    public required string SourceDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the target device ID.
    /// </summary>
    public required string TargetDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the P2P capability for this connection.
    /// </summary>
    public required P2PConnectionCapability Capability { get; init; }

    /// <summary>
    /// Gets or sets whether this is a direct connection (not routed).
    /// </summary>
    public bool IsDirect { get; init; }
}

/// <summary>
/// Comprehensive P2P topology analysis result.
/// </summary>
public sealed class P2PTopologyAnalysis
{
    /// <summary>
    /// Gets or sets the total number of devices analyzed.
    /// </summary>
    public int TotalDevices { get; init; }

    /// <summary>
    /// Gets or sets the number of direct P2P connections.
    /// </summary>
    public int DirectConnections { get; init; }

    /// <summary>
    /// Gets or sets the number of routed (multi-hop) connections.
    /// </summary>
    public int RoutedConnections { get; init; }

    /// <summary>
    /// Gets or sets the number of unsupported connections.
    /// </summary>
    public int UnsupportedConnections { get; init; }

    /// <summary>
    /// Gets or sets the identified topology clusters.
    /// </summary>
    public required IList<P2PTopologyCluster> Clusters { get; init; }

    /// <summary>
    /// Gets or sets the high-performance communication paths.
    /// </summary>
    public required IList<P2PHighPerformancePath> HighPerformancePaths { get; init; }

    /// <summary>
    /// Gets or sets the identified bandwidth bottlenecks.
    /// </summary>
    public required IList<P2PBandwidthBottleneck> Bottlenecks { get; init; }

    /// <summary>
    /// Gets or sets the average connection bandwidth in GB/s.
    /// </summary>
    public double AverageConnectionBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the peak connection bandwidth in GB/s.
    /// </summary>
    public double PeakConnectionBandwidth { get; init; }

    /// <summary>
    /// Gets or sets when this analysis was performed.
    /// </summary>
    public DateTimeOffset AnalysisTime { get; init; }
}

/// <summary>
/// Represents a cluster of devices with high-speed interconnect.
/// </summary>
/// <remarks>
/// Devices in a cluster typically share PCIe switch or NVLink domain.
/// </remarks>
public sealed class P2PTopologyCluster
{
    /// <summary>
    /// Gets or sets the cluster identifier.
    /// </summary>
    public required string ClusterId { get; init; }

    /// <summary>
    /// Gets or sets the device IDs in this cluster.
    /// </summary>
    public required IList<string> DeviceIds { get; init; }

    /// <summary>
    /// Gets or sets the cluster type (e.g., "NVLink", "PCIe-Switch").
    /// </summary>
    public required string ClusterType { get; init; }

    /// <summary>
    /// Gets or sets the average intra-cluster bandwidth in GB/s.
    /// </summary>
    public double AverageIntraClusterBandwidth { get; init; }
}

/// <summary>
/// Represents a bandwidth bottleneck in the P2P topology.
/// </summary>
public sealed class P2PBandwidthBottleneck
{
    /// <summary>
    /// Gets or sets the source device ID.
    /// </summary>
    public required string SourceDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the target device ID.
    /// </summary>
    public required string TargetDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the bottleneck description.
    /// </summary>
    /// <remarks>
    /// Example: "PCIe bandwidth limited", "Multi-hop routing overhead".
    /// </remarks>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the actual bandwidth in GB/s.
    /// </summary>
    public double ActualBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the potential bandwidth without bottleneck.
    /// </summary>
    public double PotentialBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the bottleneck severity (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// 1.0 indicates severe bottleneck (actual &lt;&lt; potential).
    /// </remarks>
    public double BottleneckSeverity => 
        PotentialBandwidth > 0 ? 1.0 - (ActualBandwidth / PotentialBandwidth) : 0.0;
}

/// <summary>
/// Represents a high-performance P2P path optimized for throughput.
/// </summary>
public sealed class P2PHighPerformancePath
{
    /// <summary>
    /// Gets or sets the P2P path.
    /// </summary>
    public required P2PPath Path { get; init; }

    /// <summary>
    /// Gets or sets the measured bandwidth in GB/s.
    /// </summary>
    public double MeasuredBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the measured latency in microseconds.
    /// </summary>
    public double MeasuredLatencyUs { get; init; }

    /// <summary>
    /// Gets or sets the performance score (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// Composite score based on bandwidth and latency relative to hardware limits.
    /// </remarks>
    public double PerformanceScore { get; init; }
}

/// <summary>
/// Result of P2P capability matrix validation.
/// </summary>
public sealed class P2PMatrixValidationResult
{
    /// <summary>
    /// Gets or sets whether the matrix is valid and consistent.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public required IList<string> ValidationErrors { get; init; }
}

/// <summary>
/// Statistics for P2P capability matrix operations.
/// </summary>
public sealed class P2PMatrixStatistics
{
    /// <summary>
    /// Gets or sets the total number of capability lookups.
    /// </summary>
    public long TotalLookups { get; set; }

    /// <summary>
    /// Gets or sets the number of cache hits.
    /// </summary>
    public long CacheHits { get; set; }

    /// <summary>
    /// Gets or sets the number of capability refreshes.
    /// </summary>
    public long TotalRefreshes { get; set; }

    /// <summary>
    /// Gets or sets the total number of device pairs tracked.
    /// </summary>
    public int TotalDevicePairs { get; set; }

    /// <summary>
    /// Gets or sets when the matrix was last built.
    /// </summary>
    public DateTimeOffset LastMatrixBuildTime { get; set; }

    /// <summary>
    /// Gets the cache hit rate (0.0-1.0).
    /// </summary>
    public double CacheHitRate => 
        TotalLookups > 0 ? (double)CacheHits / TotalLookups : 0.0;
}
