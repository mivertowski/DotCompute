// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types
{
    /// <summary>
    /// Defines the severity levels for memory coalescing issues.
    /// </summary>
    public enum IssueSeverity
    {
        /// <summary>
        /// Minor issue with minimal performance impact.
        /// </summary>
        Low,

        /// <summary>
        /// Moderate issue that should be addressed for optimal performance.
        /// </summary>
        Medium,

        /// <summary>
        /// Significant issue causing notable performance degradation.
        /// </summary>
        High,

        /// <summary>
        /// Critical issue severely impacting performance.
        /// </summary>
        Critical
    }
}