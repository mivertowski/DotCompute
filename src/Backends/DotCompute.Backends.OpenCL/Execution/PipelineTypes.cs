// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Backends.OpenCL.Profiling;
using DotCompute.Backends.OpenCL.Types.Native;

namespace DotCompute.Backends.OpenCL.Execution;

/// <summary>
/// Represents a configured OpenCL kernel execution pipeline ready for execution.
/// </summary>
public sealed class OpenCLPipeline
{
    /// <summary>Gets the unique identifier for this pipeline.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the pipeline name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the stages in this pipeline.</summary>
    public required IReadOnlyCollection<PipelineStage> Stages { get; init; }

    /// <summary>Gets the output buffer/value names.</summary>
    public required IReadOnlyCollection<string> OutputNames { get; init; }
}

/// <summary>
/// Represents a single stage in an OpenCL kernel pipeline.
/// </summary>
public sealed class PipelineStage
{
    /// <summary>Gets the stage name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the compiled kernel to execute.</summary>
    public required OpenCLKernel Kernel { get; init; }

    /// <summary>Gets the execution configuration for this stage.</summary>
    public required PipelineExecutionConfig Config { get; init; }

    /// <summary>Gets the names of stages this stage depends on (inputs).</summary>
    public IReadOnlyList<string>? InputStages { get; init; }

    /// <summary>Gets the names of stages that depend on this stage (outputs).</summary>
    public IReadOnlyList<string>? OutputStages { get; init; }

    /// <summary>Gets the output buffer names produced by this stage.</summary>
    public IReadOnlyList<string>? OutputBuffers { get; init; }
}

/// <summary>
/// Configuration for pipeline stage execution.
/// </summary>
public sealed class PipelineExecutionConfig
{
    /// <summary>Gets the global work size for kernel execution.</summary>
    public required NDRange GlobalSize { get; init; }

    /// <summary>Gets the optional local work size.</summary>
    public NDRange? LocalSize { get; init; }

    /// <summary>Gets the kernel argument specifications.</summary>
    public required IReadOnlyList<PipelineArgumentSpec> Arguments { get; init; }
}

/// <summary>
/// Specification for a kernel argument in a pipeline.
/// </summary>
public sealed class PipelineArgumentSpec
{
    /// <summary>Gets the argument name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether this is a buffer argument.</summary>
    public bool IsBuffer { get; init; }

    /// <summary>Gets whether this is a local memory argument.</summary>
    public bool IsLocalMemory { get; init; }

    /// <summary>Gets the buffer size (if buffer argument).</summary>
    public ulong? BufferSize { get; init; }

    /// <summary>Gets the buffer memory flags (if buffer argument).</summary>
    public MemoryFlags? BufferFlags { get; init; }

    /// <summary>Gets the local memory size (if local memory argument).</summary>
    public int LocalMemorySize { get; init; }
}

/// <summary>
/// Result of pipeline execution.
/// </summary>
public sealed class OpenCLPipelineResult
{
    /// <summary>Gets the pipeline name.</summary>
    public required string PipelineName { get; init; }

    /// <summary>Gets the results from each stage.</summary>
    public required IReadOnlyList<PipelineStageResult> StageResults { get; init; }

    /// <summary>Gets the final pipeline outputs.</summary>
    public required Dictionary<string, object> Outputs { get; init; }

    /// <summary>Gets the total pipeline execution time.</summary>
    public required TimeSpan TotalTime { get; init; }

    /// <summary>Gets detected fusion opportunities.</summary>
    public required IReadOnlyList<KernelFusionOpportunity> FusionOpportunities { get; init; }

    /// <summary>Gets the profiling session containing detailed metrics.</summary>
    public required ProfilingSession ProfilingSession { get; init; }
}

/// <summary>
/// Result of a single stage execution.
/// </summary>
public sealed class PipelineStageResult
{
    /// <summary>Gets the stage name.</summary>
    public required string StageName { get; init; }

    /// <summary>Gets the stage execution duration.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Gets the execution event for synchronization.</summary>
    public required OpenCLEventHandle ExecutionEvent { get; init; }

    /// <summary>Gets the stage outputs (buffers/values).</summary>
    public Dictionary<string, object>? Outputs { get; init; }
}

/// <summary>
/// Describes an opportunity for kernel fusion optimization.
/// </summary>
public sealed class KernelFusionOpportunity
{
    /// <summary>Gets the first stage name.</summary>
    public required string Stage1 { get; init; }

    /// <summary>Gets the second stage name.</summary>
    public required string Stage2 { get; init; }

    /// <summary>Gets the estimated speedup from fusion (e.g., 1.5 = 50% faster).</summary>
    public required double EstimatedSpeedup { get; init; }

    /// <summary>Gets the reason why these stages can be fused.</summary>
    public required string Reason { get; init; }

    /// <summary>Returns a string representation of this opportunity.</summary>
    public override string ToString() => $"{Stage1} + {Stage2}: {EstimatedSpeedup:F2}x speedup ({Reason})";
}

/// <summary>
/// Statistics about pipeline execution.
/// </summary>
public sealed record OpenCLPipelineStatistics
{
    /// <summary>Gets the total number of pipelines executed.</summary>
    public long TotalPipelinesExecuted { get; init; }

    /// <summary>Gets the total number of stages executed across all pipelines.</summary>
    public long TotalStagesExecuted { get; init; }

    /// <summary>Gets the total fusion opportunities detected.</summary>
    public long TotalFusionOpportunitiesDetected { get; init; }

    /// <summary>Gets the number of currently active pipelines.</summary>
    public int ActivePipelines { get; init; }
}
