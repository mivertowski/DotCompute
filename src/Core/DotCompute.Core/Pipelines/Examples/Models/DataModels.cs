// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Results;

namespace DotCompute.Core.Pipelines.Examples.Models;

/// <summary>
/// Raw input data for processing examples.
/// </summary>
public class RawInputData { }

/// <summary>
/// Analysis result containing processing recommendations.
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// Gets or sets whether GPU processing is recommended for this data.
    /// </summary>
    public bool RequiresGpuProcessing { get; set; }
}

/// <summary>
/// Processed data output from kernel chains.
/// </summary>
public class ProcessedData { }

/// <summary>
/// Machine learning input data with hash support.
/// </summary>
public class MLInputData
{
    /// <summary>
    /// Gets a hash code for caching purposes.
    /// </summary>
    public override int GetHashCode() => base.GetHashCode();
}

/// <summary>
/// Machine learning prediction result.
/// </summary>
public class MLPrediction { }

/// <summary>
/// Simulation parameters for scientific computing.
/// </summary>
public class SimulationParameters { }

/// <summary>
/// Grid state for scientific simulations.
/// </summary>
public class GridState
{
    /// <summary>
    /// Gets or sets whether high precision computation is required.
    /// </summary>
    public bool RequiresHighPrecision { get; set; }
}

/// <summary>
/// Scientific simulation result.
/// </summary>
public class SimulationResult { }

/// <summary>
/// Data chunk for streaming processing.
/// </summary>
public class DataChunk
{
    /// <summary>
    /// Gets or sets the unique identifier for this chunk.
    /// </summary>
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Processing metrics for real-time operations.
/// </summary>
public class ProcessingMetrics
{
    /// <summary>
    /// Gets or sets the total number of chunks processed.
    /// </summary>
    public int TotalChunksProcessed { get; set; }

    /// <summary>
    /// Gets or sets the total number of errors encountered.
    /// </summary>
    public int TotalErrors { get; set; }

    /// <summary>
    /// Gets or sets the total processing time.
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets the throughput in chunks per second.
    /// </summary>
    public double ThroughputPerSecond { get; set; }
}

/// <summary>
/// Complex workload for advanced processing examples.
/// </summary>
public class ComplexWorkload { }

/// <summary>
/// Detailed execution report with metrics and recommendations.
/// </summary>
public class DetailedExecutionReport
{
    /// <summary>
    /// Gets or sets whether the execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the per-step execution metrics.
    /// </summary>
    public IList<KernelStepMetrics> StepMetrics { get; init; } = [];

    /// <summary>
    /// Gets or sets the memory usage metrics.
    /// </summary>
    public KernelChainMemoryMetrics? MemoryMetrics { get; set; }

    /// <summary>
    /// Gets or sets the backend that was used for execution.
    /// </summary>
    public string BackendUsed { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets any errors that occurred during execution.
    /// </summary>
    public IList<Exception> Errors { get; init; } = [];

    /// <summary>
    /// Gets or sets optimization recommendations.
    /// </summary>
    public IList<string> Recommendations { get; init; } = [];
}

/// <summary>
/// Placeholder for program class in examples.
/// </summary>
public class Program { }
