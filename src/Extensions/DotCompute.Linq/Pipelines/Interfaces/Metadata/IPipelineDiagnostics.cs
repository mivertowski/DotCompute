// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;

namespace DotCompute.Linq.Pipelines.Interfaces.Metadata
{
    /// <summary>
    /// Represents pipeline diagnostic information.
    /// </summary>
    public interface IPipelineDiagnostics
    {
        /// <summary>Gets the number of stages in the pipeline.</summary>
        int StageCount { get; }

        /// <summary>Gets the total execution time in milliseconds.</summary>
        double TotalExecutionTimeMs { get; }

        /// <summary>Gets the cache hit rate as a percentage.</summary>
        double CacheHitRate { get; }

        /// <summary>Gets detailed stage performance metrics.</summary>
        IReadOnlyList<IStagePerformanceMetrics> StageMetrics { get; }

        /// <summary>Gets memory usage statistics.</summary>
        IMemoryUsageStatistics MemoryUsage { get; }

        /// <summary>Gets identified performance bottlenecks.</summary>
        IReadOnlyList<BottleneckInfo> Bottlenecks { get; }

        /// <summary>Gets the peak memory usage in bytes.</summary>
        long PeakMemoryUsage { get; }
    }
}