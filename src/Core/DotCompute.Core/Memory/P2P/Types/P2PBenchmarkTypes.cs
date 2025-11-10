// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Memory.P2P.Types;

/// <summary>
/// P2P benchmark options for customizing benchmark execution.
/// </summary>
/// <remarks>
/// <para>
/// Configures benchmark parameters including transfer sizes, iteration counts,
/// and caching behavior for P2P performance measurements.
/// </para>
/// <para>
/// Warmup iterations ensure stable GPU clocks before measurement iterations.
/// </para>
/// </remarks>
public sealed class P2PBenchmarkOptions
{
    /// <summary>
    /// Gets the default benchmark options with recommended settings.
    /// </summary>
    public static P2PBenchmarkOptions Default => new();

    /// <summary>
    /// Gets or sets the minimum transfer size in megabytes.
    /// </summary>
    /// <remarks>
    /// Default: 1 MB. Smaller transfers measure latency-bound performance.
    /// </remarks>
    public int MinTransferSizeMB { get; init; } = 1;

    /// <summary>
    /// Gets or sets the maximum transfer size in megabytes.
    /// </summary>
    /// <remarks>
    /// Default: 64 MB. Larger transfers measure bandwidth-bound performance.
    /// </remarks>
    public int MaxTransferSizeMB { get; init; } = 64;

    /// <summary>
    /// Gets or sets whether to use cached benchmark results.
    /// </summary>
    /// <remarks>
    /// Default: true. Cached results reduce redundant benchmarking overhead.
    /// </remarks>
    public bool UseCachedResults { get; init; } = true;

    /// <summary>
    /// Gets or sets the number of warmup iterations.
    /// </summary>
    /// <remarks>
    /// Default: 3. Warmup stabilizes GPU clocks and caches.
    /// </remarks>
    public int WarmupIterations { get; init; } = 3;

    /// <summary>
    /// Gets or sets the number of measurement iterations.
    /// </summary>
    /// <remarks>
    /// Default: 10. More iterations improve statistical confidence.
    /// </remarks>
    public int MeasurementIterations { get; init; } = 10;
}

/// <summary>
/// P2P benchmark result for a specific device pair.
/// </summary>
/// <remarks>
/// <para>
/// Contains comprehensive benchmark results for P2P transfers between two GPUs
/// across multiple transfer sizes, including throughput and latency metrics.
/// </para>
/// <para>
/// Results include statistical summaries (peak, average) and raw measurements
/// for detailed performance analysis.
/// </para>
/// </remarks>
public sealed class P2PBenchmarkResult
{
    /// <summary>
    /// Gets or sets the unique benchmark identifier.
    /// </summary>
    public required string BenchmarkId { get; init; }

    /// <summary>
    /// Gets or sets the source device identifier.
    /// </summary>
    public required string SourceDevice { get; init; }

    /// <summary>
    /// Gets or sets the target device identifier.
    /// </summary>
    public required string TargetDevice { get; init; }

    /// <summary>
    /// Gets or sets when the benchmark was executed.
    /// </summary>
    public required DateTimeOffset BenchmarkTime { get; init; }

    /// <summary>
    /// Gets or sets the benchmark options used.
    /// </summary>
    public required P2PBenchmarkOptions BenchmarkOptions { get; init; }

    /// <summary>
    /// Gets or sets the collection of transfer size benchmarks.
    /// </summary>
    public required IList<P2PTransferBenchmark> TransferSizes { get; init; }

    /// <summary>
    /// Gets or sets the peak throughput in GB/s.
    /// </summary>
    /// <remarks>
    /// Maximum observed throughput across all transfer sizes.
    /// </remarks>
    public double PeakThroughputGBps { get; set; }

    /// <summary>
    /// Gets or sets the average throughput in GB/s.
    /// </summary>
    /// <remarks>
    /// Mean throughput across all transfer sizes.
    /// </remarks>
    public double AverageThroughputGBps { get; set; }

    /// <summary>
    /// Gets or sets the minimum latency in milliseconds.
    /// </summary>
    /// <remarks>
    /// Lowest observed latency (typically for smallest transfer size).
    /// </remarks>
    public double MinimumLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public double AverageLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets whether the benchmark completed successfully.
    /// </summary>
    public required bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the error message if benchmark failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Individual transfer size benchmark result with detailed measurements.
/// </summary>
/// <remarks>
/// Captures performance metrics for a single transfer size including
/// throughput statistics and raw measurement data.
/// </remarks>
public sealed class P2PTransferBenchmark
{
    /// <summary>
    /// Gets or sets the transfer size in bytes.
    /// </summary>
    public required long TransferSizeBytes { get; init; }

    /// <summary>
    /// Gets or sets the average throughput in GB/s.
    /// </summary>
    public required double ThroughputGBps { get; init; }

    /// <summary>
    /// Gets or sets the peak throughput in GB/s.
    /// </summary>
    public required double PeakThroughputGBps { get; init; }

    /// <summary>
    /// Gets or sets the minimum throughput in GB/s.
    /// </summary>
    public required double MinThroughputGBps { get; init; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public required double LatencyMs { get; init; }

    /// <summary>
    /// Gets or sets the raw throughput measurements in GB/s.
    /// </summary>
    /// <remarks>
    /// One measurement per iteration, useful for variance analysis.
    /// </remarks>
    public required IList<double> Measurements { get; init; }

    /// <summary>
    /// Gets or sets the raw latency measurements in milliseconds.
    /// </summary>
    public required IList<double> LatencyMeasurements { get; init; }
}

/// <summary>
/// Multi-GPU benchmark options for comprehensive P2P topology evaluation.
/// </summary>
/// <remarks>
/// <para>
/// Configures which benchmark patterns to execute in multi-GPU scenarios:
/// pairwise (all pairs), scatter (1-to-N), gather (N-to-1), all-to-all.
/// </para>
/// <para>
/// Different transfer sizes optimize each pattern for typical workload characteristics.
/// </para>
/// </remarks>
public sealed class P2PMultiGpuBenchmarkOptions
{
    /// <summary>
    /// Gets the default multi-GPU benchmark options.
    /// </summary>
    public static P2PMultiGpuBenchmarkOptions Default => new();

    /// <summary>
    /// Gets or sets whether to benchmark all pairwise GPU transfers.
    /// </summary>
    public bool EnablePairwiseBenchmarks { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to benchmark scatter (1-to-N) patterns.
    /// </summary>
    public bool EnableScatterBenchmarks { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to benchmark gather (N-to-1) patterns.
    /// </summary>
    public bool EnableGatherBenchmarks { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to benchmark all-to-all communication patterns.
    /// </summary>
    public bool EnableAllToAllBenchmarks { get; init; } = true;

    /// <summary>
    /// Gets or sets the transfer size for pairwise benchmarks in megabytes.
    /// </summary>
    public int PairwiseTransferSizeMB { get; init; } = 64;

    /// <summary>
    /// Gets or sets the transfer size for scatter benchmarks in megabytes.
    /// </summary>
    public int ScatterTransferSizeMB { get; init; } = 32;

    /// <summary>
    /// Gets or sets the transfer size for gather benchmarks in megabytes.
    /// </summary>
    public int GatherTransferSizeMB { get; init; } = 32;

    /// <summary>
    /// Gets or sets the transfer size for all-to-all benchmarks in megabytes.
    /// </summary>
    public int AllToAllTransferSizeMB { get; init; } = 16;

    /// <summary>
    /// Gets or sets whether to use cached benchmark results.
    /// </summary>
    public bool UseCachedResults { get; init; } = true;
}

/// <summary>
/// Multi-GPU benchmark result aggregating all P2P performance patterns.
/// </summary>
/// <remarks>
/// <para>
/// Comprehensive benchmark results for multi-GPU systems including pairwise,
/// scatter, gather, and all-to-all communication patterns.
/// </para>
/// <para>
/// Used for topology analysis and distributed workload optimization.
/// </para>
/// </remarks>
public sealed class P2PMultiGpuBenchmarkResult
{
    /// <summary>
    /// Gets or sets the unique benchmark identifier.
    /// </summary>
    public required string BenchmarkId { get; init; }

    /// <summary>
    /// Gets or sets when the benchmark was executed.
    /// </summary>
    public required DateTimeOffset BenchmarkTime { get; init; }

    /// <summary>
    /// Gets or sets the number of GPUs benchmarked.
    /// </summary>
    public required int DeviceCount { get; init; }

    /// <summary>
    /// Gets or sets the pairwise (all pairs) benchmark results.
    /// </summary>
    public required IList<P2PBenchmarkResult> PairwiseBenchmarks { get; init; }

    /// <summary>
    /// Gets or sets the scatter (1-to-N) benchmark results.
    /// </summary>
    public required IList<P2PScatterBenchmarkResult> ScatterBenchmarks { get; init; }

    /// <summary>
    /// Gets or sets the gather (N-to-1) benchmark results.
    /// </summary>
    public required IList<P2PGatherBenchmarkResult> GatherBenchmarks { get; init; }

    /// <summary>
    /// Gets or sets the all-to-all communication benchmark results.
    /// </summary>
    public required IList<P2PAllToAllBenchmarkResult> AllToAllBenchmarks { get; init; }

    /// <summary>
    /// Gets or sets the peak pairwise throughput in GB/s.
    /// </summary>
    public double PeakPairwiseThroughputGBps { get; set; }

    /// <summary>
    /// Gets or sets the average pairwise throughput in GB/s.
    /// </summary>
    public double AveragePairwiseThroughputGBps { get; set; }

    /// <summary>
    /// Gets or sets the total benchmark execution time in milliseconds.
    /// </summary>
    public double TotalBenchmarkTimeMs { get; set; }

    /// <summary>
    /// Gets or sets whether all benchmarks completed successfully.
    /// </summary>
    public required bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the error message if any benchmark failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// P2P scatter (1-to-N) benchmark result.
/// </summary>
/// <remarks>
/// Measures performance of broadcasting data from one GPU to multiple target GPUs.
/// Common in distributed training for broadcasting model parameters.
/// </remarks>
public sealed class P2PScatterBenchmarkResult
{
    /// <summary>
    /// Gets or sets the scatter pattern description.
    /// </summary>
    /// <remarks>
    /// Example: "GPU0 → [GPU1, GPU2, GPU3]"
    /// </remarks>
    public required string ScatterPattern { get; init; }

    /// <summary>
    /// Gets or sets the aggregate throughput in GB/s.
    /// </summary>
    /// <remarks>
    /// Total data transferred divided by execution time.
    /// </remarks>
    public required double TotalThroughputGBps { get; init; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public required double AverageLatencyMs { get; init; }
}

/// <summary>
/// P2P gather (N-to-1) benchmark result.
/// </summary>
/// <remarks>
/// Measures performance of collecting data from multiple GPUs to one target GPU.
/// Common in distributed training for gradient aggregation.
/// </remarks>
public sealed class P2PGatherBenchmarkResult
{
    /// <summary>
    /// Gets or sets the gather pattern description.
    /// </summary>
    /// <remarks>
    /// Example: "[GPU1, GPU2, GPU3] → GPU0"
    /// </remarks>
    public required string GatherPattern { get; init; }

    /// <summary>
    /// Gets or sets the aggregate throughput in GB/s.
    /// </summary>
    public required double TotalThroughputGBps { get; init; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public required double AverageLatencyMs { get; init; }
}

/// <summary>
/// P2P all-to-all communication benchmark result.
/// </summary>
/// <remarks>
/// Measures performance of simultaneous bidirectional transfers between all GPU pairs.
/// Stresses the full P2P topology and identifies bottlenecks in complex communication patterns.
/// </remarks>
public sealed class P2PAllToAllBenchmarkResult
{
    /// <summary>
    /// Gets or sets the communication pattern description.
    /// </summary>
    /// <remarks>
    /// Example: "4-GPU bidirectional ring", "8-GPU full mesh"
    /// </remarks>
    public required string CommunicationPattern { get; init; }

    /// <summary>
    /// Gets or sets the aggregate throughput in GB/s.
    /// </summary>
    /// <remarks>
    /// Total data transferred across all simultaneous transfers.
    /// </remarks>
    public required double AggregateThroughputGBps { get; init; }

    /// <summary>
    /// Gets or sets the maximum latency in milliseconds.
    /// </summary>
    /// <remarks>
    /// Worst-case latency across all concurrent transfers.
    /// </remarks>
    public required double MaxLatencyMs { get; init; }
}
