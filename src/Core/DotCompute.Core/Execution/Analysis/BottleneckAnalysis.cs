// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Analysis
{
    /// <summary>
    /// Represents the results of a bottleneck analysis operation.
    /// </summary>
    /// <remarks>
    /// This class encapsulates information about performance bottlenecks identified
    /// during execution plan analysis, including the type of bottleneck, its severity,
    /// and detailed diagnostic information.
    /// </remarks>
    public class BottleneckAnalysis
    {
        /// <summary>
        /// Gets or sets the type of bottleneck identified.
        /// </summary>
        /// <value>
        /// A <see cref="Types.BottleneckType"/> value indicating the category of bottleneck
        /// (e.g., Memory, Compute, Communication, IO).
        /// </value>
        public Types.BottleneckType Type { get; set; }

        /// <summary>
        /// Gets or sets the severity of the bottleneck.
        /// </summary>
        /// <value>
        /// A double value between 0.0 and 1.0, where 0.0 indicates no impact
        /// and 1.0 indicates a critical bottleneck that severely limits performance.
        /// </value>
        public double Severity { get; set; }

        /// <summary>
        /// Gets or sets detailed information about the bottleneck.
        /// </summary>
        /// <value>
        /// A string containing diagnostic information, performance metrics,
        /// and recommendations for addressing the bottleneck.
        /// </value>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the bottleneck is considered critical.
        /// </summary>
        /// <value>
        /// <c>true</c> if the severity is greater than 0.8; otherwise, <c>false</c>.
        /// </value>
        public bool IsCritical => Severity > 0.8;

        /// <summary>
        /// Gets a value indicating whether the bottleneck requires immediate attention.
        /// </summary>
        /// <value>
        /// <c>true</c> if the severity is greater than 0.6; otherwise, <c>false</c>.
        /// </value>
        public bool RequiresAttention => Severity > 0.6;

        /// <summary>
        /// Returns a string representation of the bottleneck analysis.
        /// </summary>
        /// <returns>
        /// A string containing the bottleneck type, severity percentage, and details.
        /// </returns>
        public override string ToString() => $"{Type} Bottleneck (Severity: {Severity:P1}): {Details}";
    }
}