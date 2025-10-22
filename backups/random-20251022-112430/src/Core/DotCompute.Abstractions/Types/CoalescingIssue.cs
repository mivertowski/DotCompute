namespace DotCompute.Abstractions.Types
{
    /// <summary>
    /// Represents a specific memory coalescing issue identified during analysis.
    /// </summary>
    public class CoalescingIssue
    {
        /// <summary>
        /// Gets or sets the type of coalescing issue.
        /// </summary>
        public IssueType Type { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the issue.
        /// </summary>
        public IssueSeverity Severity { get; set; }

        /// <summary>
        /// Gets or initializes a description of the issue.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// Gets or initializes the performance impact of this issue.
        /// </summary>
        public required string Impact { get; init; }
    }
}
