// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Core.Pipelines.Types
{
    /// <summary>
    /// Internal pipeline event for kernel pipeline operations.
    /// </summary>
    internal class PipelineEvent
    {
        /// <summary>
        /// Gets or sets the event type.
        /// </summary>
        public PipelineEventType Type { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the event message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional event data.
        /// </summary>
        public Dictionary<string, object> Data { get; init; } = [];

        /// <summary>
        /// Gets or sets the stage identifier associated with the event.
        /// </summary>
        public string? StageId { get; set; }
    }
}