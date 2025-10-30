namespace DotCompute.Backends.CUDA.Graphs.Configuration
{
    /// <summary>
    /// Options for CUDA graph capture.
    /// </summary>
    public class GraphCaptureOptions
    {
        /// <summary>
        /// Gets or sets whether to allow graph invalidation.
        /// </summary>
        public bool AllowInvalidation { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to export debug visualization.
        /// </summary>
        public bool ExportDebugVisualization { get; set; }


        /// <summary>
        /// Gets or sets the maximum number of nodes in the graph.
        /// </summary>
        public int MaxNodeCount { get; set; } = 10000;

        /// <summary>
        /// Gets or sets whether to enable automatic optimization.
        /// </summary>
        public bool EnableAutoOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to allow graph updates.
        /// </summary>
        public bool AllowUpdates { get; set; } = true;

        /// <summary>
        /// Gets a default set of options.
        /// </summary>
        public static GraphCaptureOptions Default => new();

        /// <summary>
        /// Gets options optimized for performance.
        /// </summary>
        public static GraphCaptureOptions Performance => new()
        {
            AllowInvalidation = false,
            ExportDebugVisualization = false,
            EnableAutoOptimization = true,
            AllowUpdates = false
        };

        /// <summary>
        /// Gets options for debugging.
        /// </summary>
        public static GraphCaptureOptions Debug => new()
        {
            AllowInvalidation = true,
            ExportDebugVisualization = true,
            EnableAutoOptimization = false,
            AllowUpdates = true
        };
    }
}
