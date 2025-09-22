// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
namespace DotCompute.Linq.Compilation.Analysis;
{
/// <summary>
/// Represents a bottleneck in the data flow analysis.
/// </summary>
public record DataFlowBottleneck
{
    /// <summary>Gets the type of bottleneck.</summary>
    public BottleneckType Type { get; init; }
    /// <summary>Gets a description of the bottleneck.</summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>Gets the severity score (0-100).</summary>
    public int Severity { get; init; }
    /// <summary>Gets the estimated performance impact (0-1).</summary>
    public double Impact { get; init; }
    /// <summary>Gets suggestions to address this bottleneck.</summary>
    public List<string> Suggestions { get; init; } = [];
}
/// <summary>
/// Types of data flow bottlenecks.
/// </summary>
public enum BottleneckType
{
    /// <summary>Memory bandwidth bottleneck.</summary>
    MemoryBandwidth,

    /// <summary>Compute throughput bottleneck.</summary>
    ComputeThroughput,

    /// <summary>Data dependency bottleneck.</summary>
    DataDependency,

    /// <summary>Control flow bottleneck.</summary>
    ControlFlow,

    /// <summary>Resource contention bottleneck.</summary>
    ResourceContention
}
