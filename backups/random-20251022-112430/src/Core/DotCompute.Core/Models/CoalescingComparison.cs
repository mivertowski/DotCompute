namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents a comparison of coalescing efficiency across different memory access patterns.
    /// </summary>
    public class CoalescingComparison
    {
        /// <summary>
        /// Gets the dictionary of analyses indexed by pattern name.
        /// </summary>
        public Dictionary<string, CoalescingAnalysis> Analyses { get; init; } = [];

        /// <summary>
        /// Gets or sets the name of the pattern with the best efficiency.
        /// </summary>
        public string? BestPattern { get; set; }

        /// <summary>
        /// Gets or sets the name of the pattern with the worst efficiency.
        /// </summary>
        public string? WorstPattern { get; set; }

        /// <summary>
        /// Gets or sets the best efficiency value observed.
        /// </summary>
        public double BestEfficiency { get; set; }

        /// <summary>
        /// Gets or sets the worst efficiency value observed.
        /// </summary>
        public double WorstEfficiency { get; set; }

        /// <summary>
        /// Gets or sets the potential performance improvement if worst pattern is optimized to match best.
        /// </summary>
        public double ImprovementPotential { get; set; }

        /// <summary>
        /// Gets or sets the list of recommendations based on the comparison.
        /// </summary>
        public IList<string> Recommendations { get; init; } = [];
    }
}