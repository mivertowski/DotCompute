// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Models
{
    /// <summary>
    /// Stealing Statistics
    /// </summary>
    public class StealingStatistics
    {
        /// <summary>
        /// Gets or sets the total steal attempts.
        /// </summary>
        /// <value>
        /// The total steal attempts.
        /// </value>
        public long TotalStealAttempts { get; set; }

        /// <summary>
        /// Gets or sets the successful steals.
        /// </summary>
        /// <value>
        /// The successful steals.
        /// </value>
        public long SuccessfulSteals { get; set; }

        /// <summary>
        /// Gets or sets the failed steals.
        /// </summary>
        /// <value>
        /// The failed steals.
        /// </value>
        public long FailedSteals { get; set; }

        /// <summary>
        /// Gets or sets the steal success rate.
        /// </summary>
        /// <value>
        /// The steal success rate.
        /// </value>
        public double StealSuccessRate { get; set; }
    }
}