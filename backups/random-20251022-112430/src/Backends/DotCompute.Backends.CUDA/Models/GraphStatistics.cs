namespace DotCompute.Backends.CUDA.Graphs.Models
{
    /// <summary>
    /// Statistics for a CUDA graph execution.
    /// </summary>
    public class GraphStatistics
    {
        /// <summary>
        /// Gets or initializes the graph name.
        /// </summary>
        public required string GraphName { get; init; }

        /// <summary>
        /// Gets or initializes when the graph was created.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets or sets when the graph was last used.
        /// </summary>
        public DateTimeOffset? LastUsedAt { get; set; }

        /// <summary>
        /// Gets or sets the number of times the graph has been launched.
        /// </summary>
        public int LaunchCount { get; set; }

        /// <summary>
        /// Gets or sets the number of failed launches.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the number of times the graph has been updated.
        /// </summary>
        public int UpdateCount { get; set; }

        /// <summary>
        /// Gets or sets the number of times the graph had to be recreated.
        /// </summary>
        public int RecreateCount { get; set; }

        /// <summary>
        /// Gets or sets the total launch time.
        /// </summary>
        public TimeSpan TotalLaunchTime { get; set; }

        /// <summary>
        /// Gets or sets the last launch time.
        /// </summary>
        public TimeSpan LastLaunchTime { get; set; }

        /// <summary>
        /// Gets or sets the minimum launch time.
        /// </summary>
        public TimeSpan MinLaunchTime { get; set; }

        /// <summary>
        /// Gets or sets the maximum launch time.
        /// </summary>
        public TimeSpan MaxLaunchTime { get; set; }

        /// <summary>
        /// Gets or initializes the number of nodes in the graph.
        /// </summary>
        public required int NodeCount { get; init; }

        /// <summary>
        /// Gets or initializes the number of edges in the graph.
        /// </summary>
        public required int EdgeCount { get; init; }

        /// <summary>
        /// Gets the average launch time.
        /// </summary>
        public TimeSpan AverageLaunchTime
            => LaunchCount > 0 ? TotalLaunchTime / LaunchCount : TimeSpan.Zero;
    }
}