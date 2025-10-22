// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Bottleneck analysis results for kernel execution.
    /// </summary>
    public sealed class BottleneckAnalysis
    {
        /// <summary>
        /// Gets or sets the primary bottleneck type.
        /// </summary>
        public BottleneckType PrimaryBottleneck { get; set; }

        /// <summary>
        /// Gets or sets optimization suggestions to address the bottleneck.
        /// </summary>
        public IList<string> Suggestions { get; init; } = [];
    }
}