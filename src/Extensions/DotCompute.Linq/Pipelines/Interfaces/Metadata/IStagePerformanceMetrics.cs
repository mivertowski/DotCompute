// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
namespace DotCompute.Linq.Pipelines.Interfaces.Metadata
{
    /// <summary>
    /// Stage performance metrics interface.
    /// </summary>
    public interface IStagePerformanceMetrics
    {
        /// <summary>Gets the stage name.</summary>
        string StageName { get; }
        /// <summary>Gets the execution time for this stage.</summary>
        TimeSpan ExecutionTime { get; }
        /// <summary>Gets the memory usage for this stage.</summary>
        long MemoryUsage { get; }
        /// <summary>Gets the backend used for this stage.</summary>
        string Backend { get; }
        /// <summary>Gets stage-specific performance data.</summary>
        IReadOnlyDictionary<string, object> PerformanceData { get; }
    }
}
