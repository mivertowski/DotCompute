using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents the results of a memory coalescing analysis for a CUDA kernel.
    /// Contains detailed metrics and optimization recommendations.
    /// </summary>
    public class CoalescingAnalysis
    {
        /// <summary>
        /// Gets or initializes the name of the kernel that was analyzed.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets or initializes the timestamp when the analysis was performed.
        /// </summary>
        public DateTimeOffset Timestamp { get; init; }

        /// <summary>
        /// Gets or sets the coalescing efficiency as a percentage (0.0 to 1.0).
        /// 1.0 represents perfect coalescing.
        /// </summary>
        public double CoalescingEfficiency { get; set; }

        /// <summary>
        /// Gets or sets the amount of wasted bandwidth in bytes per second.
        /// </summary>
        public double WastedBandwidth { get; set; }

        /// <summary>
        /// Gets or sets the optimal memory access size for the target architecture.
        /// </summary>
        public int OptimalAccessSize { get; set; }

        /// <summary>
        /// Gets or sets the number of memory transactions required.
        /// </summary>
        public int TransactionCount { get; set; }

        /// <summary>
        /// Gets or sets the actual number of bytes transferred including overhead.
        /// </summary>
        public long ActualBytesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the useful bytes transferred (excluding overhead).
        /// </summary>
        public long UsefulBytesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the list of coalescing issues identified.
        /// </summary>
        public List<CoalescingIssue> Issues { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of optimization recommendations.
        /// </summary>
        public List<string> Optimizations { get; set; } = [];

        /// <summary>
        /// Gets or sets architecture-specific notes about the analysis.
        /// </summary>
        public List<string> ArchitectureNotes { get; set; } = [];
    }
}