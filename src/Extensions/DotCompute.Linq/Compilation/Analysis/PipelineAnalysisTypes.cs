// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotCompute.Linq.Compilation.Analysis;

/// <summary>
/// Information about pipeline operators used in expression analysis.
/// </summary>
public class PipelineOperatorInfo
{
    /// <summary>Gets or sets the operator name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the operator type.</summary>
    public Type OperatorType { get; set; } = typeof(object);

    /// <summary>Gets or sets the complexity score.</summary>
    public int ComplexityScore { get; set; }

    /// <summary>Gets or sets whether the operator can be parallelized.</summary>
    public bool CanParallelize { get; set; }

    /// <summary>Gets or sets the memory requirement estimate.</summary>
    public long MemoryRequirement { get; set; }

    /// <summary>Gets or sets operator-specific metadata.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>Gets or sets the input types for this operator.</summary>
    public List<Type> InputTypes { get; set; } = new();

    /// <summary>Gets or sets the output type for this operator.</summary>
    public Type OutputType { get; set; } = typeof(object);

    /// <summary>Gets or sets whether this operator supports GPU execution.</summary>
    public bool SupportsGpu { get; set; }

    /// <summary>Gets or sets whether this operator supports CPU execution.</summary>
    public bool SupportsCpu { get; set; } = true;

    /// <summary>Gets or sets whether this operator is natively supported.</summary>
    public bool IsNativelySupported { get; set; } = true;

    /// <summary>Gets or sets the implementation strategy.</summary>
    public string Implementation { get; set; } = "Default";

    /// <summary>Gets or sets the performance cost estimate.</summary>
    public double PerformanceCost { get; set; } = 1.0;

    /// <summary>Gets or sets the accuracy level.</summary>
    public double Accuracy { get; set; } = 1.0;
}

/// <summary>
/// Information about type usage in pipeline expressions.
/// </summary>
public class TypeUsageInfo
{
    /// <summary>Gets or sets the type being used.</summary>
    public Type Type { get; set; } = typeof(object);

    /// <summary>Gets or sets the usage frequency.</summary>
    public int UsageCount { get; set; }

    /// <summary>Gets or sets the usage frequency (alias for UsageCount).</summary>
    public int UsageFrequency
    {
        get => UsageCount;
        set => UsageCount = value;
    }

    /// <summary>Gets or sets whether the type is supported on GPU.</summary>
    public bool IsGpuCompatible { get; set; }

    /// <summary>Gets or sets the memory size of the type.</summary>
    public int TypeSize { get; set; }

    /// <summary>Gets or sets the estimated size (alias for TypeSize).</summary>
    public int EstimatedSize
    {
        get => TypeSize;
        set => TypeSize = value;
    }

    /// <summary>Gets or sets conversion requirements.</summary>
    public List<string> ConversionRequirements { get; set; } = new();

    /// <summary>Gets or sets whether the type requires specialization.</summary>
    public bool RequiresSpecialization { get; set; }

    /// <summary>Gets or sets the memory access pattern for this type.</summary>
    public MemoryAccessPattern MemoryPattern { get; set; } = MemoryAccessPattern.Sequential;

    /// <summary>Gets or sets whether this type supports SIMD operations.</summary>
    public bool SupportsSimd { get; set; }

    /// <summary>Gets or sets optimization hints for this type.</summary>
    public List<string> Hints { get; set; } = new();
}

/// <summary>
/// Information about dependencies between pipeline operations.
/// </summary>
public class DependencyInfo
{
    /// <summary>Gets or sets the dependent operation name.</summary>
    public string DependentOperation { get; set; } = string.Empty;

    /// <summary>Gets or sets the operations this one depends on.</summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>Gets or sets the dependency type (data, control, etc.).</summary>
    public DependencyType Type { get; set; }

    /// <summary>Gets or sets whether the dependency allows parallel execution.</summary>
    public bool AllowsParallelization { get; set; }
}

/// <summary>
/// Types of dependencies between operations.
/// </summary>
public enum DependencyType
{
    /// <summary>Data dependency.</summary>
    Data,
    /// <summary>Control flow dependency.</summary>
    Control,
    /// <summary>Memory dependency.</summary>
    Memory,
    /// <summary>Resource dependency.</summary>
    Resource,
    /// <summary>Method call dependency.</summary>
    Method
}

/// <summary>
/// Metrics about pipeline complexity for optimization decisions.
/// </summary>
public class PipelineComplexityMetrics
{
    /// <summary>Gets or sets the total complexity score.</summary>
    public int TotalComplexity { get; set; }

    /// <summary>Gets or sets the overall complexity score (alias for TotalComplexity).</summary>
    public int OverallComplexity
    {
        get => TotalComplexity;
        set => TotalComplexity = value;
    }

    /// <summary>Gets or sets the number of operations.</summary>
    public int OperationCount { get; set; }

    /// <summary>Gets or sets the estimated memory usage.</summary>
    public long EstimatedMemoryUsage { get; set; }

    /// <summary>Gets or sets the memory usage (alias for EstimatedMemoryUsage).</summary>
    public long MemoryUsage
    {
        get => EstimatedMemoryUsage;
        set => EstimatedMemoryUsage = value;
    }

    /// <summary>Gets or sets the parallelization potential (0.0 to 1.0).</summary>
    public double ParallelizationPotential { get; set; }

    /// <summary>Gets or sets whether GPU execution is recommended.</summary>
    public bool GpuRecommended { get; set; }

    /// <summary>Gets or sets complexity by category.</summary>
    public Dictionary<string, int> ComplexityByCategory { get; set; } = new();

    /// <summary>Gets or sets the memory complexity score.</summary>
    public int MemoryComplexity { get; set; }

    /// <summary>Gets or sets the compute complexity score.</summary>
    public int ComputeComplexity { get; set; }

    /// <summary>Gets or sets the computational complexity score.</summary>
    public int ComputationalComplexity { get; set; }

    /// <summary>Gets or sets the communication complexity score.</summary>
    public int CommunicationComplexity { get; set; }

    /// <summary>Gets or sets the parallelization complexity score.</summary>
    public int ParallelizationComplexity { get; set; }
}

/// <summary>
/// Information about parallelization opportunities in pipeline.
/// </summary>
public class ParallelizationInfo
{
    /// <summary>
    /// Initializes a new instance of the ParallelizationInfo class.
    /// </summary>
    public ParallelizationInfo() { }

    /// <summary>
    /// Initializes a new instance of the ParallelizationInfo class with parallelization opportunities and bottlenecks.
    /// </summary>
    /// <param name="parallelizationOpportunities">Collection of parallelization opportunities</param>
    /// <param name="dataFlowBottlenecks">Collection of data flow bottlenecks</param>
    public ParallelizationInfo(IEnumerable<object> parallelizationOpportunities, IEnumerable<object> dataFlowBottlenecks)
    {
        // Convert opportunities to operations list
        if (parallelizationOpportunities != null)
        {
            foreach (var opportunity in parallelizationOpportunities)
            {
                ParallelizableOperations.Add(opportunity.ToString() ?? "Unknown");
            }
        }

        // Convert bottlenecks to bottlenecks list
        if (dataFlowBottlenecks != null)
        {
            foreach (var bottleneck in dataFlowBottlenecks)
            {
                Bottlenecks.Add(bottleneck.ToString() ?? "Unknown");
            }
        }

        // Set defaults based on the data
        CanParallelize = ParallelizableOperations.Count > 0;
        DegreeOfParallelism = CanParallelize ? Math.Max(1, ParallelizableOperations.Count) : 1;
        MaxParallelism = Environment.ProcessorCount;
        ParallelEfficiency = CanParallelize ? Math.Max(0.1, 1.0 - (Bottlenecks.Count * 0.2)) : 0.0;
        ParallelizationMethod = CanParallelize ? "DataParallel" : "None";
    }
    /// <summary>Gets or sets the degree of parallelism.</summary>
    public int DegreeOfParallelism { get; set; }

    /// <summary>Gets or sets parallelizable operations.</summary>
    public List<string> ParallelizableOperations { get; set; } = new();

    /// <summary>Gets or sets operations that must run sequentially.</summary>
    public List<string> SequentialOperations { get; set; } = new();

    /// <summary>Gets or sets the parallel efficiency estimate (0.0 to 1.0).</summary>
    public double ParallelEfficiency { get; set; }

    /// <summary>Gets or sets bottleneck operations that limit parallelization.</summary>
    public List<string> Bottlenecks { get; set; } = new();

    /// <summary>Gets or sets whether this pipeline can be parallelized.</summary>
    public bool CanParallelize { get; set; }

    /// <summary>Gets or sets the maximum degree of parallelism possible.</summary>
    public int MaxParallelism { get; set; }

    /// <summary>Gets or sets the parallelization method to use.</summary>
    public string ParallelizationMethod { get; set; } = "None";
}

/// <summary>
/// Information about global memory access patterns for optimization.
/// </summary>
public class GlobalMemoryAccessPattern
{
    /// <summary>Gets or sets the access pattern type.</summary>
    public MemoryAccessType AccessType { get; set; }

    /// <summary>Gets or sets the predominant pattern (alias for AccessType).</summary>
    public MemoryAccessPattern PredominantPattern
    {
        get => (MemoryAccessPattern)(int)AccessType;
        set => AccessType = (MemoryAccessType)(int)value;
    }

    /// <summary>Gets or sets whether accesses are coalesced.</summary>
    public bool IsCoalesced { get; set; }

    /// <summary>Gets or sets whether there are coalescing opportunities (alias for IsCoalesced).</summary>
    public bool HasCoalescingOpportunities
    {
        get => IsCoalesced;
        set => IsCoalesced = value;
    }

    /// <summary>Gets or sets the stride pattern.</summary>
    public int StridePattern { get; set; }

    /// <summary>Gets or sets memory locations accessed.</summary>
    public List<MemoryLocation> AccessedLocations { get; set; } = new();

    /// <summary>Gets or sets cache efficiency estimate (0.0 to 1.0).</summary>
    public double CacheEfficiency { get; set; }

    /// <summary>Gets or sets the estimated cache hit ratio (alias for CacheEfficiency).</summary>
    public double EstimatedCacheHitRatio
    {
        get => CacheEfficiency;
        set => CacheEfficiency = value;
    }

    /// <summary>Gets or sets the locality factor (0.0 to 1.0).</summary>
    public double LocalityFactor { get; set; } = 0.5;

    /// <summary>Gets or sets whether this pattern benefits from prefetching.</summary>
    public bool BenefitsFromPrefetching { get; set; }

    /// <summary>Gets or sets the total memory footprint in bytes.</summary>
    public long TotalMemoryFootprint { get; set; }

    /// <summary>Gets or sets the working set size in bytes.</summary>
    public long WorkingSetSize { get; set; }

    /// <summary>Gets or sets the bandwidth utilization (0.0 to 1.0).</summary>
    public double BandwidthUtilization { get; set; } = 0.5;

    /// <summary>Gets or sets the pattern type as a string.</summary>
    public string PatternType
    {
        get => AccessType.ToString();
        set => AccessType = Enum.TryParse<MemoryAccessType>(value, true, out var result) ? result : MemoryAccessType.Sequential;
    }

    /// <summary>Gets or sets the global memory access pattern.</summary>
    public MemoryAccessType Pattern { get; set; } = MemoryAccessType.Sequential;
}

/// <summary>
/// Types of memory access patterns.
/// </summary>
public enum MemoryAccessType
{
    /// <summary>Sequential access pattern.</summary>
    Sequential,
    /// <summary>Random access pattern.</summary>
    Random,
    /// <summary>Strided access pattern.</summary>
    Strided,
    /// <summary>Coalesced access pattern (GPU).</summary>
    Coalesced,
    /// <summary>Scattered access pattern.</summary>
    Scattered
}

/// <summary>
/// Represents a memory location in the access pattern.
/// </summary>
public class MemoryLocation
{
    /// <summary>Gets or sets the memory address offset.</summary>
    public long Offset { get; set; }

    /// <summary>Gets or sets the size of the access.</summary>
    public int Size { get; set; }

    /// <summary>Gets or sets the access frequency.</summary>
    public int AccessFrequency { get; set; }

    /// <summary>Gets or sets whether this is a read or write access.</summary>
    public bool IsWrite { get; set; }
}

/// <summary>
/// Analysis result containing all pipeline analysis information.
/// </summary>
public class PipelineAnalysisResult
{
    /// <summary>Gets or sets pipeline operator information.</summary>
    public List<PipelineOperatorInfo> OperatorInfo { get; set; } = new();

    /// <summary>Gets or sets type usage information.</summary>
    public List<TypeUsageInfo> TypeUsage { get; set; } = new();

    /// <summary>Gets or sets dependency information.</summary>
    public List<DependencyInfo> Dependencies { get; set; } = new();

    /// <summary>Gets or sets complexity metrics.</summary>
    public PipelineComplexityMetrics ComplexityMetrics { get; set; } = new();

    /// <summary>Gets or sets parallelization information.</summary>
    public ParallelizationInfo ParallelizationInfo { get; set; } = new();

    /// <summary>Gets or sets memory access patterns.</summary>
    public GlobalMemoryAccessPattern MemoryAccessPattern { get; set; } = new();

    /// <summary>Gets or sets analysis timestamp.</summary>
    public DateTimeOffset AnalysisTimestamp { get; set; } = DateTimeOffset.UtcNow;
}