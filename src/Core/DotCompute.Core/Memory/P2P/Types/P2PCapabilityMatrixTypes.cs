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
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the device name.
    /// </summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the device type.
    /// </summary>
    public string DeviceType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the device supports P2P.
    /// </summary>
    public bool SupportsP2P { get; init; }

    /// <summary>
    /// Gets or sets the memory bandwidth in GB/s.
    /// </summary>
    public double MemoryBandwidthGBps { get; init; }

    /// <summary>
    /// Gets or sets the maximum P2P bandwidth in GB/s.
    /// </summary>
    public double MaxP2PBandwidthGBps { get; init; }

    /// <summary>
    /// Gets or sets the PCIe bus ID.
    /// </summary>
    public string PcieBusId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the NUMA node.
    /// </summary>
    public int NumaNode { get; init; }

    /// <summary>
    /// Gets or sets the list of directly connected peer device IDs.
    /// </summary>
    public IList<string> DirectPeers { get; init; } = [];

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
    public string SourceDeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the target device ID.
    /// </summary>
    public string TargetDeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the intermediate hop device IDs.
    /// </summary>
    public IList<string> IntermediateHops { get; init; } = [];

    /// <summary>
    /// Gets or sets the estimated total bandwidth in GB/s.
    /// </summary>
    public double EstimatedBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the current device in the path.
    /// </summary>
    public string? CurrentDevice { get; set; }

    /// <summary>
    /// Gets or sets the path device list.
    /// </summary>
    public IList<string> Path { get; set; } = [];

    /// <summary>
    /// Gets or sets the total bandwidth.
    /// </summary>
    public double TotalBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the total latency.
    /// </summary>
    public double TotalLatency { get; set; }
}

/// <summary>
/// Represents a P2P communication path between devices.
/// </summary>
public sealed class P2PPath
{
    /// <summary>
    /// Gets or sets the source device ID.
    /// </summary>
    public string SourceDeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the source device (alias for source accelerator).
    /// </summary>
    public IAccelerator? SourceDevice { get; set; }

    /// <summary>
    /// Gets or sets the target device ID.
    /// </summary>
    public string TargetDeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the target device (alias for target accelerator).
    /// </summary>
    public IAccelerator? TargetDevice { get; set; }

    /// <summary>
    /// Gets or sets the ordered list of device IDs forming the path.
    /// </summary>
    /// <remarks>
    /// Includes source, all intermediate hops, and target.
    /// </remarks>
    public IList<string> PathDevices { get; init; } = [];

    /// <summary>
    /// Gets or sets the intermediate devices in the path.
    /// </summary>
    public IList<string> IntermediateDevices { get; init; } = [];

    /// <summary>
    /// Gets or sets the number of hops in this path.
    /// </summary>
    public int HopCount => PathDevices.Count - 1;

    /// <summary>
    /// Gets or sets the number of hops (alias).
    /// </summary>
    public int Hops { get; set; }

    /// <summary>
    /// Gets or sets the estimated bandwidth in GB/s.
    /// </summary>
    public double EstimatedBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the total bandwidth in GB/s.
    /// </summary>
    public double TotalBandwidthGBps { get; set; }

    /// <summary>
    /// Gets or sets the estimated latency in microseconds.
    /// </summary>
    public double EstimatedLatencyUs { get; init; }

    /// <summary>
    /// Gets or sets the estimated latency in milliseconds.
    /// </summary>
    public double EstimatedLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets whether this is a direct P2P connection.
    /// </summary>
    public bool IsDirectP2P { get; set; }
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
    /// Gets or sets the target accelerator device, if available.
    /// </summary>
    public IAccelerator? TargetDevice { get; init; }

    /// <summary>
    /// Gets or sets the P2P capability for this connection.
    /// </summary>
    public required P2PConnectionCapability Capability { get; init; }

    /// <summary>
    /// Gets or sets whether this is a direct connection (not routed).
    /// </summary>
    public bool IsDirect { get; init; }

    /// <summary>
    /// Gets or sets when the connection was last validated.
    /// </summary>
    public DateTimeOffset LastValidated { get; set; }
}

/// <summary>
/// Comprehensive P2P topology analysis result.
/// </summary>
public sealed class P2PTopologyAnalysis
{
    /// <summary>
    /// Gets or sets the total number of devices analyzed.
    /// </summary>
    public int TotalDevices { get; set; }

    /// <summary>
    /// Gets or sets the total number of possible connections.
    /// </summary>
    public int TotalPossibleConnections { get; set; }

    /// <summary>
    /// Gets or sets the number of P2P-enabled connections.
    /// </summary>
    public int P2PEnabledConnections { get; set; }

    /// <summary>
    /// Gets or sets the P2P connectivity ratio (0.0-1.0).
    /// </summary>
    public double P2PConnectivityRatio { get; set; }

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
    public IList<P2PTopologyCluster> Clusters { get; init; } = [];

    /// <summary>
    /// Gets or sets the topology clusters (alias).
    /// </summary>
    public IList<P2PTopologyCluster> TopologyClusters { get; init; } = [];

    /// <summary>
    /// Gets or sets the high-performance communication paths.
    /// </summary>
    public IList<P2PHighPerformancePath> HighPerformancePaths { get; init; } = [];

    /// <summary>
    /// Gets or sets the identified bandwidth bottlenecks.
    /// </summary>
    public IList<P2PBandwidthBottleneck> Bottlenecks { get; init; } = [];

    /// <summary>
    /// Gets or sets the bandwidth bottlenecks (alias).
    /// </summary>
    public IList<P2PBandwidthBottleneck> BandwidthBottlenecks { get; init; } = [];

    /// <summary>
    /// Gets or sets the average connection bandwidth in GB/s.
    /// </summary>
    public double AverageConnectionBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the peak connection bandwidth in GB/s.
    /// </summary>
    public double PeakConnectionBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the average NVLink bandwidth in GB/s.
    /// </summary>
    public double AverageNVLinkBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the average PCIe bandwidth in GB/s.
    /// </summary>
    public double AveragePCIeBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the average InfiniBand bandwidth in GB/s.
    /// </summary>
    public double AverageInfiniBandBandwidth { get; set; }

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
    public string ClusterId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the device IDs in this cluster.
    /// </summary>
    public IList<string> DeviceIds { get; init; } = [];

    /// <summary>
    /// Gets or sets the cluster type (e.g., "NVLink", "PCIe-Switch").
    /// </summary>
    public string ClusterType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the average intra-cluster bandwidth in GB/s.
    /// </summary>
    public double AverageIntraClusterBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the average bandwidth in GB/s.
    /// </summary>
    public double AverageBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the interconnect density (connections per device).
    /// </summary>
    public double InterconnectDensity { get; init; }
}

/// <summary>
/// Represents a bandwidth bottleneck in the P2P topology.
/// </summary>
public sealed class P2PBandwidthBottleneck
{
    /// <summary>
    /// Gets or sets the source device ID.
    /// </summary>
    public string SourceDeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the target device ID.
    /// </summary>
    public string TargetDeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the bottleneck description.
    /// </summary>
    /// <remarks>
    /// Example: "PCIe bandwidth limited", "Multi-hop routing overhead".
    /// </remarks>
    public string Description { get; init; } = string.Empty;

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
    public double BottleneckSeverity => PotentialBandwidth > 0 ? 1.0 - (ActualBandwidth / PotentialBandwidth) : 0.0;

    // Additional properties used in implementation
    public string? Device1Id { get; init; }
    public string? Device2Id { get; init; }
    public double BandwidthGBps { get; init; }
    public double ExpectedBandwidthGBps { get; init; }
    public P2PConnectionType ConnectionType { get; init; }
}

/// <summary>
/// Represents a high-performance P2P path optimized for throughput.
/// </summary>
public sealed class P2PHighPerformancePath
{
    /// <summary>
    /// Gets or sets the P2P path.
    /// </summary>
    public P2PPath? Path { get; init; }

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

    // Additional properties used in implementation
    public string? Device1Id { get; init; }
    public string? Device2Id { get; init; }
    public double BandwidthGBps { get; init; }
    public P2PConnectionType ConnectionType { get; init; }
    public double PerformanceRatio { get; init; }
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
    public IList<string> ValidationErrors { get; init; } = [];

    /// <summary>
    /// Gets or sets the list of validation issues.
    /// </summary>
    public IList<string> Issues { get; init; } = [];

    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    public DateTimeOffset ValidationTime { get; set; }
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
    /// Gets or sets the total number of devices.
    /// </summary>
    public int TotalDevices { get; set; }

    /// <summary>
    /// Gets or sets the total number of connections.
    /// </summary>
    public int TotalConnections { get; set; }

    /// <summary>
    /// Gets or sets the number of P2P-enabled connections.
    /// </summary>
    public int P2PEnabledConnections { get; set; }

    /// <summary>
    /// Gets or sets the number of NVLink connections.
    /// </summary>
    public int NVLinkConnections { get; set; }

    /// <summary>
    /// Gets or sets the number of PCIe connections.
    /// </summary>
    public int PCIeConnections { get; set; }

    /// <summary>
    /// Gets or sets the number of InfiniBand connections.
    /// </summary>
    public int InfiniBandConnections { get; set; }

    /// <summary>
    /// Gets or sets the average bandwidth in GB/s.
    /// </summary>
    public double AverageBandwidthGBps { get; set; }

    /// <summary>
    /// Gets or sets the peak bandwidth in GB/s.
    /// </summary>
    public double PeakBandwidthGBps { get; set; }

    /// <summary>
    /// Gets or sets when the matrix was last refreshed.
    /// </summary>
    public DateTimeOffset LastRefreshTime { get; set; }

    /// <summary>
    /// Gets or sets the time taken to build the matrix.
    /// </summary>
    public TimeSpan MatrixBuildTime { get; set; }

    /// <summary>
    /// Gets the cache hit rate (0.0-1.0).
    /// </summary>
    public double CacheHitRate => TotalLookups > 0 ? (double)CacheHits / TotalLookups : 0.0;

    /// <summary>
    /// Gets or sets the cache hit ratio (0.0-1.0).
    /// </summary>
    public double CacheHitRatio { get; set; }
}
