using DotCompute.Backends.CUDA.Graphs.Types;

namespace DotCompute.Backends.CUDA.Graphs.Models
{
    /// <summary>
    /// Analysis results for a CUDA graph structure.
    /// </summary>
    public class GraphAnalysis
    {
        /// <summary>
        /// Gets or sets the number of nodes in the graph.
        /// </summary>
        public int NodeCount { get; set; }

        /// <summary>
        /// Gets or sets the number of edges in the graph.
        /// </summary>
        public int EdgeCount { get; set; }

        /// <summary>
        /// Gets the dictionary of node types and their counts.
        /// </summary>
        public Dictionary<CudaGraphNodeType, int> NodeTypes { get; } = [];

        /// <summary>
        /// Gets or sets the maximum depth of the graph.
        /// </summary>
        public int MaxDepth { get; set; }

        /// <summary>
        /// Gets or sets the number of parallel branches.
        /// </summary>
        public int ParallelBranches { get; set; }

        /// <summary>
        /// Gets or sets whether the graph contains cycles.
        /// </summary>
        public bool HasCycles { get; set; }

        /// <summary>
        /// Gets or sets the critical path length.
        /// </summary>
        public int CriticalPathLength { get; set; }

        /// <summary>
        /// Gets the list of optimization opportunities identified.
        /// </summary>
        public List<string> OptimizationOpportunities { get; } = [];

        /// <summary>
        /// Gets or sets the number of parallelization opportunities.
        /// </summary>
        public int ParallelizationOpportunities { get; set; }

        /// <summary>
        /// Gets or sets the number of kernel fusion opportunities.
        /// </summary>
        public int FusionOpportunities { get; set; }

        /// <summary>
        /// Gets or sets the estimated memory footprint in bytes.
        /// </summary>
        public long MemoryFootprint { get; set; }

        /// <summary>
        /// Gets or sets the number of optimizations applied.
        /// </summary>
        public int OptimizationsApplied { get; set; }
    }
}